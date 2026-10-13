using System;
using System.Collections.Generic;
using Jellyfin.Plugin.TranslationTags.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.TranslationTags
{
    /// <summary>
    /// Translation Tags plugin entry point.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <summary>Gets the singleton instance.</summary>
        public static Plugin? Instance { get; private set; }

        /// <inheritdoc />
        public override string Name => "Translation Tags";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("c2f8b5e3-7d4a-4b9f-8e32-1a9d6b0c4f25");

        /// <inheritdoc />
        public override string Description =>
            "Bulk-tag movies by their translation type (Napisy, Lektor, Dubbing, Brak Tłumaczenia) " +
            "and auto-detect Polish audio/subtitles. Tags are stored as native Jellyfin tags so you " +
            "can filter the library by them straight away.";

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            var prefix = GetType().Namespace;
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "TranslationTags",
                    EmbeddedResourcePath = prefix + ".Configuration.configPage.html",
                    DisplayName = "Translation Tags",
                    EnableInMainMenu = true,
                    MenuIcon = "label"
                }
            };
        }
    }
}
