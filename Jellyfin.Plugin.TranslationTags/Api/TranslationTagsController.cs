using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TranslationTags.Configuration;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.TranslationTags.Api
{
    /// <summary>A selectable library.</summary>
    public class LibraryDto
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>An item (movie/series) shown as a poster card.</summary>
    public class ItemDto
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int? ProductionYear { get; set; }

        public string Type { get; set; } = string.Empty;

        public string[] Tags { get; set; } = Array.Empty<string>();

        /// <summary>Primary image tag (for building the poster URL), or null if none.</summary>
        public string? PrimaryImageTag { get; set; }
    }

    /// <summary>Request body for bulk tag operations.</summary>
    public class BulkTagRequest
    {
        public List<string> ItemIds { get; set; } = new();

        public List<string> Tags { get; set; } = new();
    }

    /// <summary>Request body for creating a collection from a tag.</summary>
    public class CollectionRequest
    {
        public string Tag { get; set; } = string.Empty;

        public string? Name { get; set; }
    }

    /// <summary>
    /// Admin API backing the Translation Tags dashboard.
    /// </summary>
    [ApiController]
    [Authorize(Policy = Policies.RequiresElevation)]
    [Route("Plugins/TranslationTags")]
    [Produces("application/json")]
    public class TranslationTagsController : ControllerBase
    {
        private readonly ILibraryManager _libraryManager;
        private readonly TagService _service;

        public TranslationTagsController(ILibraryManager libraryManager, TagService service)
        {
            _libraryManager = libraryManager;
            _service = service;
        }

        private static PluginConfiguration Config => Plugin.Instance!.Configuration;

        private List<string> ManagedTags()
        {
            // XmlSerializer appends to the list initialised in the configuration
            // constructor, which can produce duplicates. De-dupe defensively.
            return Config.PredefinedTags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Returns the predefined / managed tags for the UI.</summary>
        [HttpGet("Tags")]
        public ActionResult<IEnumerable<string>> GetTags()
        {
            return Ok(ManagedTags());
        }

        /// <summary>Returns the selectable libraries.</summary>
        [HttpGet("Libraries")]
        public ActionResult<IEnumerable<LibraryDto>> GetLibraries()
        {
            var libs = _service.GetLibraries()
                .Select(l => new LibraryDto { Id = l.Id, Name = l.Name })
                .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase);
            return Ok(libs);
        }

        /// <summary>Lists items (posters) in a library with their current tags.</summary>
        [HttpGet("Items")]
        public ActionResult<IEnumerable<ItemDto>> GetItems(
            [FromQuery] string? libraryId,
            [FromQuery] string? search,
            [FromQuery] string? filterTag)
        {
            var managed = ManagedTags();
            var items = _service.GetItems(libraryId, search, filterTag, managed);

            var rows = items.Select(i => new ItemDto
            {
                Id = i.Id.ToString("N"),
                Name = i.Name,
                ProductionYear = i.ProductionYear,
                Type = i.GetType().Name,
                Tags = i.Tags ?? Array.Empty<string>(),
                PrimaryImageTag = GetPrimaryImageTag(i)
            });

            return Ok(rows.ToList());
        }

        private static string? GetPrimaryImageTag(BaseItem item)
        {
            try
            {
                var img = item.GetImageInfo(ImageType.Primary, 0);
                if (img is null)
                {
                    return null;
                }

                // A short deterministic token is enough for the client to form the URL;
                // the exact value only needs to change when the image changes.
                return (img.DateModified.Ticks ^ (img.Path?.GetHashCode() ?? 0))
                    .ToString("x", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Adds the given tags to all listed items.</summary>
        [HttpPost("Add")]
        public async Task<ActionResult> Add([FromBody] BulkTagRequest request, CancellationToken ct)
        {
            var count = 0;
            foreach (var item in Resolve(request.ItemIds))
            {
                await _service.AddTagsAsync(item, request.Tags, ct).ConfigureAwait(false);
                count++;
            }

            return Ok(new { updated = count });
        }

        /// <summary>Removes the given tags from all listed items.</summary>
        [HttpPost("Remove")]
        public async Task<ActionResult> Remove([FromBody] BulkTagRequest request, CancellationToken ct)
        {
            var count = 0;
            foreach (var item in Resolve(request.ItemIds))
            {
                await _service.RemoveTagsAsync(item, request.Tags, ct).ConfigureAwait(false);
                count++;
            }

            return Ok(new { updated = count });
        }

        /// <summary>Removes ALL tags from the listed items.</summary>
        [HttpPost("ClearAll")]
        public async Task<ActionResult> ClearAll([FromBody] BulkTagRequest request, CancellationToken ct)
        {
            var count = 0;
            foreach (var item in Resolve(request.ItemIds))
            {
                await _service.ClearAllTagsAsync(item, ct).ConfigureAwait(false);
                count++;
            }

            return Ok(new { updated = count });
        }

        /// <summary>Creates a collection containing every item that carries the given tag.</summary>
        [HttpPost("CreateCollection")]
        public async Task<ActionResult> CreateCollection([FromBody] CollectionRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Tag))
            {
                return BadRequest("Tag is required.");
            }

            var name = string.IsNullOrWhiteSpace(request.Name) ? request.Tag.Trim() : request.Name.Trim();
            var count = await _service.CreateCollectionFromTagAsync(request.Tag.Trim(), name, ct).ConfigureAwait(false);

            return Ok(new { created = count, name });
        }

        private IEnumerable<BaseItem> Resolve(IEnumerable<string> ids)
        {
            foreach (var id in ids)
            {
                if (Guid.TryParse(id, out var guid) && _libraryManager.GetItemById(guid) is { } item)
                {
                    yield return item;
                }
            }
        }
    }
}
