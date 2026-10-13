using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.ContinuousTV.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Playlists;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ContinuousTV
{
    /// <summary>
    /// Builds the ordered item list for a smart playlist and materializes it as a Jellyfin playlist.
    /// </summary>
    public class ContinuousPlaylistService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;
        private readonly IPlaylistManager _playlistManager;
        private readonly ILogger<ContinuousPlaylistService> _logger;

        public ContinuousPlaylistService(
            ILibraryManager libraryManager,
            IUserManager userManager,
            IUserDataManager userDataManager,
            IPlaylistManager playlistManager,
            ILogger<ContinuousPlaylistService> logger)
        {
            _libraryManager = libraryManager;
            _userManager = userManager;
            _userDataManager = userDataManager;
            _playlistManager = playlistManager;
            _logger = logger;
        }

        /// <summary>
        /// A source of items (one series, or the pooled movies) together with its scheduling weight.
        /// </summary>
        private sealed class Source
        {
            public required string Key { get; init; }

            // Ordered queue of item ids still to be scheduled from this source.
            public required Queue<Guid> Queue { get; init; }

            public int Weight { get; set; }

            // Running counter used by smooth weighted round-robin.
            public int Current { get; set; }
        }

        /// <summary>
        /// Computes the ordered list of item ids for the given definition.
        /// </summary>
        public IReadOnlyList<Guid> BuildOrderedItemIds(SmartPlaylistDto def)
        {
            var user = ResolveUser(def);
            if (user is null)
            {
                _logger.LogWarning("Continuous TV: no user resolved for playlist {Name}", def.Name);
                return Array.Empty<Guid>();
            }

            var sources = new List<Source>();

            foreach (var seriesIdStr in def.SeriesIds)
            {
                if (!Guid.TryParse(seriesIdStr, out var seriesId))
                {
                    continue;
                }

                if (_libraryManager.GetItemById(seriesId) is not Series series)
                {
                    continue;
                }

                var episodes = GetOrderedUnwatchedEpisodes(series, user, def.IncludeUnairedAndMissing, out var totalEpisodes);
                if (episodes.Count == 0)
                {
                    continue;
                }

                var weight = def.WeightMode switch
                {
                    WeightMode.Equal => 1,
                    WeightMode.RemainingEpisodes => episodes.Count,
                    _ => totalEpisodes
                };

                sources.Add(new Source
                {
                    Key = "series:" + seriesId,
                    Queue = new Queue<Guid>(episodes.Select(e => e.Id)),
                    Weight = Math.Max(1, weight)
                });
            }

            // Movies are pooled into a single source so they get sprinkled through the schedule.
            var movieIds = new List<Guid>();
            foreach (var movieIdStr in def.MovieIds)
            {
                if (Guid.TryParse(movieIdStr, out var movieId) && _libraryManager.GetItemById(movieId) is Video)
                {
                    movieIds.Add(movieId);
                }
            }

            if (movieIds.Count > 0)
            {
                sources.Add(new Source
                {
                    Key = "movies",
                    Queue = new Queue<Guid>(movieIds),
                    Weight = Math.Max(1, def.MovieWeight * movieIds.Count)
                });
            }

            return Interleave(sources, def.TargetItemCount);
        }

        /// <summary>
        /// Smooth weighted round-robin (the algorithm Nginx uses). Produces a nicely
        /// spread ordering where higher-weight sources appear proportionally more often
        /// instead of in long consecutive runs.
        /// </summary>
        private static IReadOnlyList<Guid> Interleave(List<Source> sources, int target)
        {
            var result = new List<Guid>();
            if (sources.Count == 0)
            {
                return result;
            }

            var totalWeight = sources.Sum(s => s.Weight);
            var hardCap = sources.Sum(s => s.Queue.Count);
            if (target <= 0 || target > hardCap)
            {
                target = hardCap;
            }

            while (result.Count < target)
            {
                var available = sources.Where(s => s.Queue.Count > 0).ToList();
                if (available.Count == 0)
                {
                    break;
                }

                Source? best = null;
                foreach (var s in available)
                {
                    s.Current += s.Weight;
                    if (best is null || s.Current > best.Current)
                    {
                        best = s;
                    }
                }

                best!.Current -= totalWeight;
                result.Add(best.Queue.Dequeue());
            }

            return result;
        }

        /// <summary>
        /// Returns episodes of a series in airing order, starting from the first unwatched one,
        /// skipping already-watched episodes earlier in the run.
        /// </summary>
        private List<Episode> GetOrderedUnwatchedEpisodes(Series series, User user, bool includeMissing, out int totalEpisodes)
        {
            var query = new InternalItemsQuery(user)
            {
                AncestorIds = new[] { series.Id },
                IncludeItemTypes = new[] { BaseItemKind.Episode },
                Recursive = true,
                OrderBy = new[]
                {
                    (ItemSortBy.AiredEpisodeOrder, SortOrder.Ascending)
                },
                IsVirtualItem = includeMissing ? null : false
            };

            var all = _libraryManager.GetItemList(query).OfType<Episode>().ToList();
            totalEpisodes = all.Count;

            // Keep only episodes at/after the first unwatched one, so we resume like a DVR.
            var unwatched = new List<Episode>();
            var started = false;
            foreach (var ep in all)
            {
                var data = _userDataManager.GetUserData(user, ep);
                var played = data?.Played ?? false;
                if (!started && played)
                {
                    continue;
                }

                started = true;
                if (!played)
                {
                    unwatched.Add(ep);
                }
            }

            return unwatched;
        }

        private User? ResolveUser(SmartPlaylistDto def)
        {
            if (!string.IsNullOrEmpty(def.UserId) && Guid.TryParse(def.UserId, out var id))
            {
                var u = _userManager.GetUserById(id);
                if (u is not null)
                {
                    return u;
                }
            }

            return _userManager.GetUsers().FirstOrDefault();
        }

        /// <summary>
        /// Generates (or regenerates) the Jellyfin playlist for a definition and returns its id.
        /// </summary>
        public async Task<string?> GenerateAsync(SmartPlaylistDto def, CancellationToken cancellationToken)
        {
            var user = ResolveUser(def);
            if (user is null)
            {
                return null;
            }

            var ids = BuildOrderedItemIds(def);
            if (ids.Count == 0)
            {
                _logger.LogInformation("Continuous TV: nothing to schedule for {Name}", def.Name);
                return null;
            }

            // Remove the previously generated playlist (best effort) so we don't pile up duplicates.
            if (!string.IsNullOrEmpty(def.GeneratedPlaylistId)
                && Guid.TryParse(def.GeneratedPlaylistId, out var oldId)
                && _libraryManager.GetItemById(oldId) is { } oldItem)
            {
                try
                {
                    _libraryManager.DeleteItem(oldItem, new DeleteOptions { DeleteFileLocation = true });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Continuous TV: could not delete old playlist {Id}", oldId);
                }
            }

            var request = new PlaylistCreationRequest
            {
                Name = def.Name,
                ItemIdList = ids.ToArray(),
                UserId = user.Id,
                MediaType = MediaType.Video
            };

            var result = await _playlistManager.CreatePlaylist(request).ConfigureAwait(false);
            def.GeneratedPlaylistId = result.Id;

            _logger.LogInformation(
                "Continuous TV: generated playlist {Name} with {Count} items (id {Id})",
                def.Name,
                ids.Count,
                result.Id);

            return result.Id;
        }
    }
}
