using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ContinuousTV.ScheduledTasks
{
    /// <summary>
    /// Rebuilds every smart playlist so it always resumes from the next unwatched episode.
    /// </summary>
    public class RegeneratePlaylistsTask : IScheduledTask
    {
        private readonly ContinuousPlaylistService _service;
        private readonly ILogger<RegeneratePlaylistsTask> _logger;

        public RegeneratePlaylistsTask(
            ContinuousPlaylistService service,
            ILogger<RegeneratePlaylistsTask> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => "Regenerate Continuous TV playlists";

        /// <inheritdoc />
        public string Key => "ContinuousTvRegenerate";

        /// <inheritdoc />
        public string Description => "Rebuilds the smart Continuous TV playlists so they resume at the next unwatched episode.";

        /// <inheritdoc />
        public string Category => "Continuous TV";

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var plugin = Plugin.Instance;
            if (plugin is null || !plugin.Configuration.AutoRegenerate)
            {
                progress.Report(100);
                return;
            }

            var defs = plugin.Configuration.Playlists;
            if (defs.Count == 0)
            {
                progress.Report(100);
                return;
            }

            var done = 0;
            foreach (var def in defs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await _service.GenerateAsync(def, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Continuous TV: failed to regenerate playlist {Name}", def.Name);
                }

                done++;
                progress.Report(done * 100.0 / defs.Count);
            }

            plugin.SaveConfiguration();
            progress.Report(100);
        }

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Run once a day.
            yield return new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromHours(24).Ticks
            };
        }
    }
}
