using System;
using System.Collections.Generic;
using Jellyfin.Plugin.ContinuousTV.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.ContinuousTV
{
    /// <summary>
    /// The Continuous TV plugin entry point.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <summary>Gets the singleton instance of the plugin.</summary>
        public static Plugin? Instance { get; private set; }

        /// <inheritdoc />
        public override string Name => "Continuous TV";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("b1e7a4d2-6c3f-4a8e-9d21-7f0c5a9b3e14");

        /// <inheritdoc />
        public override string Description =>
            "Build a smart, never-ending playlist that mixes episodes from several series (and movies) " +
            "the way an old TV channel would \u2013 always playing the next unwatched episode and sampling " +
            "longer series proportionally more often than short ones.";

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            var prefix = GetType().Namespace;
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "ContinuousTv",
                    EmbeddedResourcePath = prefix + ".Configuration.configPage.html",
                    DisplayName = "Continuous TV",
                    EnableInMainMenu = true,
                    MenuIcon = "live_tv"
                }
            };
        }
    }
}
