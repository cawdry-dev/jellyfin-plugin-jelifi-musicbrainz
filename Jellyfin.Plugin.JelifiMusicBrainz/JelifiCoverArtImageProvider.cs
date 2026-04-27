using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JelifiMusicBrainz;

/// <summary>
/// Album cover art provider that queries Cover Art Archive, preferring
/// the release-group front cover (canonical for the album) over a specific
/// release's cover (which may be an edition-specific variant).
/// </summary>
public class JelifiCoverArtImageProvider : IRemoteImageProvider, IHasOrder
{
    private const string CoverArtArchiveBase = "https://coverartarchive.org";

    private readonly ILogger<JelifiCoverArtImageProvider> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public JelifiCoverArtImageProvider(IHttpClientFactory httpClientFactory, ILogger<JelifiCoverArtImageProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => "Jelifi MusicBrainz";

    // Lower than the default (0) so Jellyfin picks our images first.
    public int Order => -10;

    public bool Supports(BaseItem item) => item is MusicAlbum;

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => new[] { ImageType.Primary };

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(NamedClient.Default);
        return client.GetAsync(new Uri(url), cancellationToken);
    }

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var releaseGroupId = item.GetProviderId(MetadataProvider.MusicBrainzReleaseGroup);
        var releaseId = item.GetProviderId(MetadataProvider.MusicBrainzAlbum);

        var results = new List<RemoteImageInfo>();

        // Try the release group first — its front cover is the canonical one
        // and is consistent across all releases in the group.
        if (!string.IsNullOrEmpty(releaseGroupId))
        {
            results.AddRange(await FetchFrontsAsync($"release-group/{releaseGroupId}", cancellationToken).ConfigureAwait(false));
        }

        // Fall back to the specific release if the group has no art.
        if (results.Count == 0 && !string.IsNullOrEmpty(releaseId))
        {
            results.AddRange(await FetchFrontsAsync($"release/{releaseId}", cancellationToken).ConfigureAwait(false));
        }

        // Approved images first; otherwise preserve CAA order.
        return results.OrderByDescending(r => r.CommunityRating ?? 0);
    }

    private async Task<IEnumerable<RemoteImageInfo>> FetchFrontsAsync(string path, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(NamedClient.Default);
        var url = $"{CoverArtArchiveBase}/{path}";

        try
        {
            using var response = await client.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Array.Empty<RemoteImageInfo>();
            }

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<CaaResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (payload?.Images is null)
            {
                return Array.Empty<RemoteImageInfo>();
            }

            return payload.Images
                .Where(img => img.Front == true || (img.Types is not null && img.Types.Contains("Front", StringComparer.OrdinalIgnoreCase)))
                .Where(img => !string.IsNullOrWhiteSpace(img.Image))
                .Select(img => new RemoteImageInfo
                {
                    ProviderName = Name,
                    Url = img.Image!,
                    ThumbnailUrl = img.Thumbnails?.Large ?? img.Thumbnails?.Size500 ?? img.Thumbnails?.Small,
                    Type = ImageType.Primary,
                    CommunityRating = img.Approved == true ? 1 : 0
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cover Art Archive lookup failed for {Path}", path);
            return Array.Empty<RemoteImageInfo>();
        }
    }

    private sealed class CaaResponse
    {
        [JsonPropertyName("images")]
        public List<CaaImage>? Images { get; set; }
    }

    private sealed class CaaImage
    {
        [JsonPropertyName("image")]
        public string? Image { get; set; }

        [JsonPropertyName("front")]
        public bool? Front { get; set; }

        [JsonPropertyName("approved")]
        public bool? Approved { get; set; }

        [JsonPropertyName("types")]
        public List<string>? Types { get; set; }

        [JsonPropertyName("thumbnails")]
        public CaaThumbnails? Thumbnails { get; set; }
    }

    private sealed class CaaThumbnails
    {
        [JsonPropertyName("small")]
        public string? Small { get; set; }

        [JsonPropertyName("large")]
        public string? Large { get; set; }

        [JsonPropertyName("250")]
        public string? Size250 { get; set; }

        [JsonPropertyName("500")]
        public string? Size500 { get; set; }

        [JsonPropertyName("1200")]
        public string? Size1200 { get; set; }
    }
}
