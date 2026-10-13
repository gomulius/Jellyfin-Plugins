using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ContinuousTV.Configuration
{
    /// <summary>
    /// How the number of episodes drawn from each series is weighted.
    /// </summary>
    public enum WeightMode
    {
        /// <summary>Weight by the total number of episodes in the series.</summary>
        TotalEpisodes = 0,

        /// <summary>Weight by the number of remaining (unwatched) episodes.</summary>
        RemainingEpisodes = 1,

        /// <summary>Every selected series is treated equally.</summary>
        Equal = 2
    }

    /// <summary>
    /// Definition of a single smart "continuous TV" playlist.
    /// </summary>
    public class SmartPlaylistDto
    {
        public SmartPlaylistDto()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "Continuous TV";
            SeriesIds = new List<string>();
            MovieIds = new List<string>();
            TargetItemCount = 40;
            WeightMode = WeightMode.TotalEpisodes;
            MovieWeight = 2;
            IncludeUnairedAndMissing = false;
        }

        /// <summary>Gets or sets the stable identifier of this definition.</summary>
        public string Id { get; set; }

        /// <summary>Gets or sets the display name of the generated Jellyfin playlist.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the Jellyfin user (by id) that owns the playlist and whose watch state is used.</summary>
        public string? UserId { get; set; }

        /// <summary>Gets or sets the selected series ids.</summary>
        public List<string> SeriesIds { get; set; }

        /// <summary>Gets or sets the selected movie ids.</summary>
        public List<string> MovieIds { get; set; }

        /// <summary>Gets or sets how many items to put into the generated playlist.</summary>
        public int TargetItemCount { get; set; }

        /// <summary>Gets or sets the weighting strategy.</summary>
        public WeightMode WeightMode { get; set; }

        /// <summary>Gets or sets the relative weight each movie carries against series episode counts.</summary>
        public int MovieWeight { get; set; }

        /// <summary>Gets or sets a value indicating whether unaired/missing episodes are considered.</summary>
        public bool IncludeUnairedAndMissing { get; set; }

        /// <summary>Gets or sets the id of the last generated Jellyfin playlist (so it can be replaced).</summary>
        public string? GeneratedPlaylistId { get; set; }
    }

    /// <summary>
    /// Plugin configuration root.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            Playlists = new List<SmartPlaylistDto>();
            AutoRegenerate = true;
        }

        /// <summary>Gets or sets the saved smart playlist definitions.</summary>
        public List<SmartPlaylistDto> Playlists { get; set; }

        /// <summary>Gets or sets a value indicating whether the scheduled task rebuilds playlists automatically.</summary>
        public bool AutoRegenerate { get; set; }
    }
}
