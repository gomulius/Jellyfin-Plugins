using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TranslationTags.Configuration
{
    /// <summary>
    /// Plugin configuration.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            PredefinedTags = new List<string> { "Napisy", "Lektor", "Dubbing", "Brak Tłumaczenia" };
        }

        /// <summary>Gets or sets the quick-pick tags shown in the UI.</summary>
        public List<string> PredefinedTags { get; set; }
    }
}
