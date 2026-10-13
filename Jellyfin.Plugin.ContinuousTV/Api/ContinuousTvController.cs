using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ContinuousTV.Configuration;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ContinuousTV.Api
{
    /// <summary>
    /// Lightweight DTO describing a library item for the dashboard pickers.
    /// </summary>
    public class LibraryItemDto
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int? ProductionYear { get; set; }

        public int EpisodeCount { get; set; }
    }

    /// <summary>
    /// Admin API backing the Continuous TV dashboard page.
    /// </summary>
    [ApiController]
    [Authorize(Policy = Policies.RequiresElevation)]
    [Route("Plugins/ContinuousTv")]
    [Produces("application/json")]
    public class ContinuousTvController : ControllerBase
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly ContinuousPlaylistService _service;

        public ContinuousTvController(
            ILibraryManager libraryManager,
            IUserManager userManager,
            ContinuousPlaylistService service)
        {
            _libraryManager = libraryManager;
            _userManager = userManager;
            _service = service;
        }

        private static PluginConfiguration Config => Plugin.Instance!.Configuration;

        /// <summary>Lists Jellyfin users so the UI can choose which account the playlist belongs to.</summary>
        [HttpGet("Users")]
        public ActionResult<IEnumerable<LibraryItemDto>> GetUsers()
        {
            var users = _userManager.GetUsers()
                .Select(u => new LibraryItemDto { Id = u.Id.ToString("N"), Name = u.Username })
                .OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase);
            return Ok(users);
        }

        /// <summary>Lists all series in the library, optionally filtered by a search term.</summary>
        [HttpGet("Series")]
        public ActionResult<IEnumerable<LibraryItemDto>> GetSeries([FromQuery] string? search)
        {
            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Series },
                Recursive = true,
                SearchTerm = string.IsNullOrWhiteSpace(search) ? null : search
            };

            var items = _libraryManager.GetItemList(query)
                .Select(i => new LibraryItemDto
                {
                    Id = i.Id.ToString("N"),
                    Name = i.Name,
                    ProductionYear = i.ProductionYear
                })
                .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase);

            return Ok(items);
        }

        /// <summary>Lists all movies in the library, optionally filtered by a search term.</summary>
        [HttpGet("Movies")]
        public ActionResult<IEnumerable<LibraryItemDto>> GetMovies([FromQuery] string? search)
        {
            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                Recursive = true,
                SearchTerm = string.IsNullOrWhiteSpace(search) ? null : search
            };

            var items = _libraryManager.GetItemList(query)
                .Select(i => new LibraryItemDto
                {
                    Id = i.Id.ToString("N"),
                    Name = i.Name,
                    ProductionYear = i.ProductionYear
                })
                .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase);

            return Ok(items);
        }

        /// <summary>Returns all saved smart playlist definitions.</summary>
        [HttpGet("Playlists")]
        public ActionResult<IEnumerable<SmartPlaylistDto>> GetPlaylists()
        {
            return Ok(Config.Playlists);
        }

        /// <summary>Creates or updates a smart playlist definition.</summary>
        [HttpPost("Playlists")]
        public ActionResult<SmartPlaylistDto> SavePlaylist([FromBody] SmartPlaylistDto dto)
        {
            if (dto is null)
            {
                return BadRequest();
            }

            var config = Config;
            var existing = config.Playlists.FirstOrDefault(p => p.Id == dto.Id);
            if (existing is null)
            {
                if (string.IsNullOrEmpty(dto.Id))
                {
                    dto.Id = Guid.NewGuid().ToString("N");
                }

                config.Playlists.Add(dto);
            }
            else
            {
                dto.GeneratedPlaylistId = existing.GeneratedPlaylistId;
                config.Playlists[config.Playlists.IndexOf(existing)] = dto;
            }

            Plugin.Instance!.SaveConfiguration();
            return Ok(dto);
        }

        /// <summary>Deletes a smart playlist definition.</summary>
        [HttpDelete("Playlists/{id}")]
        public ActionResult DeletePlaylist([FromRoute] string id)
        {
            var config = Config;
            var existing = config.Playlists.FirstOrDefault(p => p.Id == id);
            if (existing is null)
            {
                return NotFound();
            }

            config.Playlists.Remove(existing);
            Plugin.Instance!.SaveConfiguration();
            return NoContent();
        }

        /// <summary>Generates (materializes) the Jellyfin playlist for a definition now.</summary>
        [HttpPost("Playlists/{id}/Generate")]
        public async Task<ActionResult> Generate([FromRoute] string id, CancellationToken cancellationToken)
        {
            var def = Config.Playlists.FirstOrDefault(p => p.Id == id);
            if (def is null)
            {
                return NotFound();
            }

            var playlistId = await _service.GenerateAsync(def, cancellationToken).ConfigureAwait(false);
            Plugin.Instance!.SaveConfiguration();

            if (playlistId is null)
            {
                return StatusCode(StatusCodes.Status204NoContent);
            }

            return Ok(new { playlistId });
        }

        /// <summary>Previews the ordered item names that would be generated, without creating a playlist.</summary>
        [HttpGet("Playlists/{id}/Preview")]
        public ActionResult<IEnumerable<string>> Preview([FromRoute] string id)
        {
            var def = Config.Playlists.FirstOrDefault(p => p.Id == id);
            if (def is null)
            {
                return NotFound();
            }

            var names = _service.BuildOrderedItemIds(def)
                .Select(itemId => _libraryManager.GetItemById(itemId))
                .Where(item => item is not null)
                .Select(item =>
                {
                    if (item is MediaBrowser.Controller.Entities.TV.Episode ep)
                    {
                        var s = ep.ParentIndexNumber?.ToString("D2") ?? "??";
                        var e = ep.IndexNumber?.ToString("D2") ?? "??";
                        return $"{ep.SeriesName} - S{s}E{e} - {ep.Name}";
                    }

                    return item!.Name;
                });

            return Ok(names);
        }
    }
}
