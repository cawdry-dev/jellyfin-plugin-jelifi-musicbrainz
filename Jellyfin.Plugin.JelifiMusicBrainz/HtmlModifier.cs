using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JelifiMusicBrainz;

/// <summary>
/// Runs at server startup and injects the mb-type card-tagger JavaScript into
/// the Jellyfin web client's index.html file.
///
/// This is an unofficial mechanism – Jellyfin does not formally support server
/// plugins modifying the web client HTML. It mirrors the approach used by other
/// community plugins (jellyfin-plugin-custom-javascript, Jellyfin-JavaScript-Injector)
/// and is idempotent: repeated startups replace the previous injection rather
/// than duplicating it.
///
/// Docker note: If Jellyfin's web files are owned by a different user than the
/// server process, the write will fail with UnauthorizedAccessException. Mount
/// the web directory as a writable volume to fix this.
/// </summary>
public class HtmlModifier : IServerEntryPoint
{
    // HTML comments used as sentinels to locate and replace our injection block.
    private const string StartMarker = "<!-- jelifi-mb-type-start -->";
    private const string EndMarker   = "<!-- jelifi-mb-type-end -->";

    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<HtmlModifier> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlModifier"/> class.
    /// </summary>
    public HtmlModifier(IApplicationPaths applicationPaths, ILogger<HtmlModifier> logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task RunAsync()
    {
        InjectScript();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Injects the embedded client script into index.html (idempotent).
    /// </summary>
    internal void InjectScript()
    {
        var indexPath = Path.Combine(_applicationPaths.WebPath, "index.html");

        if (!File.Exists(indexPath))
        {
            _logger.LogWarning(
                "[JelifiMB] index.html not found at {Path}. " +
                "JS injection skipped (headless server or non-standard web path?).",
                indexPath);
            return;
        }

        try
        {
            var js = ReadEmbeddedScript();
            var html = File.ReadAllText(indexPath);

            // Strip any previous injection so this is always idempotent.
            html = StripInjection(html);

            var block = $"\n{StartMarker}\n<script>\n{js}\n</script>\n{EndMarker}\n";
            html = html.Replace("</body>", block + "</body>", StringComparison.OrdinalIgnoreCase);

            File.WriteAllText(indexPath, html);
            _logger.LogInformation("[JelifiMB] Injected mb-type card-tagger into {Path}.", indexPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(
                ex,
                "[JelifiMB] Permission denied writing to {Path}. " +
                "If running in Docker, mount the jellyfin-web directory as a writable volume.",
                indexPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JelifiMB] Failed to inject script into {Path}.", indexPath);
        }
    }

    /// <summary>
    /// Removes the injected script block from index.html. Called on plugin uninstall.
    /// </summary>
    internal void RemoveScript()
    {
        var indexPath = Path.Combine(_applicationPaths.WebPath, "index.html");

        if (!File.Exists(indexPath))
        {
            return;
        }

        try
        {
            var html = File.ReadAllText(indexPath);
            var cleaned = StripInjection(html);
            if (!string.Equals(cleaned, html, StringComparison.Ordinal))
            {
                File.WriteAllText(indexPath, cleaned);
                _logger.LogInformation("[JelifiMB] Removed mb-type card-tagger from {Path}.", indexPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[JelifiMB] Could not remove injected script from {Path}.", indexPath);
        }
    }

    private static string StripInjection(string html)
    {
        return Regex.Replace(
            html,
            @"\n?" + Regex.Escape(StartMarker) + @".*?" + Regex.Escape(EndMarker) + @"\n?",
            string.Empty,
            RegexOptions.Singleline);
    }

    private static string ReadEmbeddedScript()
    {
        const string ResourceName = "Jellyfin.Plugin.JelifiMusicBrainz.Web.mb-type-tagger.js";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
