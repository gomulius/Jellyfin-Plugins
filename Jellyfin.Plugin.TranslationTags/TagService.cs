using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TranslationTags
{
    /// <summary>
    /// Reads/writes native Jellyfin tags on library items.
    /// </summary>
    public class TagService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ICollectionManager _collectionManager;
        private readonly ILogger<TagService> _logger;

        public TagService(
            ILibraryManager libraryManager,
            ICollectionManager collectionManager,
            ILogger<TagService> logger)
        {
            _libraryManager = libraryManager;
            _collectionManager = collectionManager;
            _logger = logger;
        }

        /// <summary>Returns the top-level libraries (virtual folders).</summary>
        public IReadOnlyList<(string Id, string Name)> GetLibraries()
        {
            var result = new List<(string, string)>();
            foreach (var vf in _libraryManager.GetVirtualFolders())
            {
                if (!string.IsNullOrEmpty(vf.ItemId))
                {
                    result.Add((vf.ItemId, vf.Name));
                }
            }

            return result;
        }

        /// <summary>
        /// Returns taggable items (movies &amp; series) in a library, optionally filtered
        /// by search term and/or an existing tag.
        /// </summary>
        public IReadOnlyList<BaseItem> GetItems(string? libraryId, string? search, string? filterTag, IReadOnlyCollection<string> managedTags)
        {
            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series },
                Recursive = true,
                IsVirtualItem = false,
                SearchTerm = string.IsNullOrWhiteSpace(search) ? null : search,
                OrderBy = new[] { (ItemSortBy.SortName, SortOrder.Ascending) }
            };

            if (!string.IsNullOrEmpty(libraryId) && Guid.TryParse(libraryId, out var libGuid))
            {
                query.ParentId = libGuid;
            }

            var items = _libraryManager.GetItemList(query);

            if (!string.IsNullOrWhiteSpace(filterTag))
            {
                if (string.Equals(filterTag, "__untagged__", StringComparison.Ordinal))
                {
                    items = items
                        .Where(i => !(i.Tags ?? Array.Empty<string>())
                            .Any(t => managedTags.Contains(t, StringComparer.OrdinalIgnoreCase)))
                        .ToList();
                }
                else
                {
                    items = items
                        .Where(i => (i.Tags ?? Array.Empty<string>())
                            .Contains(filterTag, StringComparer.OrdinalIgnoreCase))
                        .ToList();
                }
            }

            return items;
        }

        /// <summary>Adds the given tags to an item (idempotent) and persists.</summary>
        public Task AddTagsAsync(BaseItem item, IEnumerable<string> tags, CancellationToken ct)
        {
            var set = new List<string>(item.Tags ?? Array.Empty<string>());
            var changed = false;
            foreach (var tag in tags.Select(t => t.Trim()).Where(t => t.Length > 0))
            {
                if (!set.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    set.Add(tag);
                    changed = true;
                }
            }

            if (!changed)
            {
                return Task.CompletedTask;
            }

            item.Tags = set.ToArray();
            return SaveAsync(item, ct);
        }

        /// <summary>Removes the given tags from an item and persists.</summary>
        public Task RemoveTagsAsync(BaseItem item, IEnumerable<string> tags, CancellationToken ct)
        {
            var current = item.Tags ?? Array.Empty<string>();
            var toRemove = new HashSet<string>(tags.Select(t => t.Trim()), StringComparer.OrdinalIgnoreCase);
            var kept = current.Where(t => !toRemove.Contains(t)).ToArray();

            if (kept.Length == current.Length)
            {
                return Task.CompletedTask;
            }

            item.Tags = kept;
            return SaveAsync(item, ct);
        }

        /// <summary>Removes every tag from an item and persists.</summary>
        public Task ClearAllTagsAsync(BaseItem item, CancellationToken ct)
        {
            if ((item.Tags ?? Array.Empty<string>()).Length == 0)
            {
                return Task.CompletedTask;
            }

            item.Tags = Array.Empty<string>();
            return SaveAsync(item, ct);
        }

        /// <summary>
        /// Creates (or refills) a collection containing every item that carries the given tag.
        /// Returns the number of items placed in the collection.
        /// </summary>
        public async Task<int> CreateCollectionFromTagAsync(string tag, string collectionName, CancellationToken ct)
        {
            // Gather every movie/series carrying the tag, across all libraries.
            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series },
                Recursive = true,
                IsVirtualItem = false,
                Tags = new[] { tag }
            };

            var ids = _libraryManager.GetItemList(query)
                .Select(i => i.Id)
                .ToArray();

            if (ids.Length == 0)
            {
                return 0;
            }

            var collection = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
            {
                Name = collectionName,
                ItemIdList = ids.Select(id => id.ToString("N")).ToArray()
            }).ConfigureAwait(false);

            // CreateCollectionAsync already seeds the items on most builds, but calling
            // AddToCollectionAsync is idempotent and guarantees membership.
            try
            {
                await _collectionManager.AddToCollectionAsync(collection.Id, ids).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TranslationTags: AddToCollection follow-up failed for {Name}", collectionName);
            }

            return ids.Length;
        }

        private Task SaveAsync(BaseItem item, CancellationToken ct)
        {
            return _libraryManager.UpdateItemAsync(
                item,
                item.GetParent(),
                ItemUpdateType.MetadataEdit,
                ct);
        }
    }
}
