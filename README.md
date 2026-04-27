# Jelifi MusicBrainz (Jellyfin plugin)

Drop-in replacement for Jellyfin's built-in MusicBrainz metadata provider that
also writes the **release-group type** to each album's `Tags` field.

This makes it possible for client apps to categorise an artist's discography
into Albums / EPs / Singles / Live / Compilations / Soundtracks etc. without
running their own per-album MusicBrainz lookups.

## What it adds

For every album with a MusicBrainz Release Group ID, this plugin tags the album
with one or more values from the set below (lowercased, spaces stripped):

| Tag                       | Source                                |
|---------------------------|---------------------------------------|
| `mb-type-album`           | release-group **primary** type        |
| `mb-type-single`          | release-group **primary** type        |
| `mb-type-ep`              | release-group **primary** type        |
| `mb-type-broadcast`       | release-group **primary** type        |
| `mb-type-other`           | release-group **primary** type        |
| `mb-type-live`            | release-group **secondary** type      |
| `mb-type-compilation`     | release-group **secondary** type      |
| `mb-type-soundtrack`      | release-group **secondary** type      |
| `mb-type-remix`           | release-group **secondary** type      |
| `mb-type-djmix`           | release-group **secondary** type      |
| `mb-type-mixtape/street`  | release-group **secondary** type      |
| `mb-type-demo`            | release-group **secondary** type      |

Existing tags are preserved; only `mb-type-*` tags are rewritten.

## Installation

### Option A — Plugin catalog (recommended)

1. In Jellyfin, go to *Dashboard → Plugins → Repositories → +*.
2. **Repository name**: `Jelifi MusicBrainz`
   **Repository URL**:
   `https://raw.githubusercontent.com/cawdry-dev/jellyfin-plugin-jelifi-musicbrainz/main/manifest.json`
3. Save. The plugin appears under *Catalog → Metadata*.
4. **Disable the built-in MusicBrainz** under
   *Dashboard → Libraries → Metadata downloaders → Music + Album*.
5. Install *Jelifi MusicBrainz* from the catalog. Restart Jellyfin.
6. **Enable** *Jelifi MusicBrainz* on the same metadata downloader page.
7. Run a metadata refresh on your music library.

### Option B — Manual sideload

1. Disable the built-in MusicBrainz provider as above.
2. Download `jelifimusicbrainz_<version>.zip` from the
   [releases page](https://github.com/cawdry-dev/jellyfin-plugin-jelifi-musicbrainz/releases).
3. Extract into `<jellyfin-data>/plugins/JelifiMusicBrainz_<version>/`.
4. Restart Jellyfin, enable, refresh.

## Build & package locally

```sh
./scripts/package.sh 1.0.0.0
```

Produces `artifacts/jelifimusicbrainz_1.0.0.0.zip` and prints the md5
checksum + timestamp you'd paste into `manifest.json`.

## Release

Tag a version and push:

```sh
git tag v1.0.0.0
git push origin v1.0.0.0
```

The `Release` GitHub Action builds the zip, attaches it to a GitHub release,
and updates `manifest.json` with the new version + checksum.

## Provenance

Forked from the in-tree MusicBrainz provider in
[jellyfin/jellyfin](https://github.com/jellyfin/jellyfin/tree/master/MediaBrowser.Providers/Plugins/MusicBrainz)
(GPL-2.0). Namespace renamed to `Jellyfin.Plugin.JelifiMusicBrainz`, plugin
GUID changed, and `MusicBrainzAlbumProvider` extended with release-type tagging.

## License

GPL-2.0, matching upstream Jellyfin.
