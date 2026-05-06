using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JelifiMusicBrainz;

/// <summary>
/// Injects the mb-type card-tagger JavaScript into the Jellyfin web client's
/// index.html file. Called directly from the Plugin constructor so it runs at
/// server startup without requiring IServerEntryPoint (removed in Jellyfin 10.10).
///
/// This is an unofficial mechanism – Jellyfin does not formally support plugins
/// modifying the web client HTML. It mirrors the approach used by community
/// plugins such as jellyfin-plugin-custom-javascript, and is idempotent: repeated
/// startups replace the previous injection rather than duplicating it.
///
/// Docker note: If Jellyfin's web files are owned by a different user than the
/// server process the write will fail. Mount the jellyfin-web directory as a
/// writable volume to fix this.
/// </summary>
public class HtmlModifier
{
    private const string StartMarker = "<!-- jelifi-mb-type-start -->";
    private const string EndMarker   = "<!-- jelifi-mb-type-end -->";

    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlModifier"/> class.
    /// </summary>
    public HtmlModifier(IApplicationPaths applicationPaths, ILogger logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    /// <summary>
    /// Injects the embedded client script into index.html (idempotent).
    /// </summary>
    public void InjectScript()
    {
        var indexPath = Path.Combine(_applicationPaths.WebPath, "index.html");

        // Always log the resolved path so it can be verified in the server log
        // even before we know whether the file exists.
        _logger.LogInformation(
            "[JelifiMB] WebPath = '{WebPath}'  →  targeting '{IndexPath}'",
            _applicationPaths.WebPath,
            indexPath);

        if (!File.Exists(indexPath))
        {
            _logger.LogWarning(
                "[JelifiMB] index.html not found at '{Path}'. " +
                "JS injection skipped. " +
                "Run: grep -n 'jelifi-mb-type' \"{Path}\" to confirm after a successful write.",
                indexPath);
            return;
        }

        _logger.LogInformation("[JelifiMB] index.html found at '{Path}', proceeding with injection.", indexPath);

        try
        {
            var js = ReadEmbeddedScript();
            var html = File.ReadAllText(indexPath);

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
    public void RemoveScript()
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
}
