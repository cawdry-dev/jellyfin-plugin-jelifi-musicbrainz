using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Jellyfin.Plugin.JelifiMusicBrainz.Configuration;
using MetaBrainz.MusicBrainz;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.JelifiMusicBrainz;

/// <summary>
/// Plugin instance.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<Plugin> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="applicationHost">Instance of the <see cref="IApplicationHost"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{Plugin}"/> interface.</param>
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        IApplicationHost applicationHost,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _applicationPaths = applicationPaths;
        _logger = logger;

        // TODO: Change this to "JellyfinMusicBrainzPlugin" once we take it out of the server repo.
        Query.DefaultUserAgent.Add(new ProductInfoHeaderValue(applicationHost.Name.Replace(' ', '-'), applicationHost.ApplicationVersionString));
        Query.DefaultUserAgent.Add(new ProductInfoHeaderValue($"({applicationHost.ApplicationUserAgentAddress})"));
        Query.DelayBetweenRequests = Instance.Configuration.RateLimit;
        Query.DefaultServer = Instance.Configuration.Server;

        new HtmlModifier(applicationPaths, logger).InjectScript();
    }

    /// <inheritdoc />
    public override void OnUninstalling()
    {
        new HtmlModifier(_applicationPaths, NullLogger.Instance).RemoveScript();
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override Guid Id => new Guid("1446d25d-82be-4a1b-bc6c-0c42b1723b2f");

    /// <inheritdoc />
    public override string Name => "Jelifi MusicBrainz";

    /// <inheritdoc />
    public override string Description => "MusicBrainz metadata provider with release-type tagging (album/single/ep/live/compilation/soundtrack).";

    /// <inheritdoc />
    public override string ConfigurationFileName => "Jellyfin.Plugin.JelifiMusicBrainz.xml";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = GetType().Namespace + ".Configuration.config.html"
        };
    }
}
