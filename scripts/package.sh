#!/usr/bin/env bash
# Builds, zips and computes md5 for the plugin.
# Usage: ./scripts/package.sh [version]
set -euo pipefail

VERSION="${1:-1.0.0.0}"
TARGET_ABI="10.10.0.0"
GUID="b5d2f3a9-3e6b-4c12-9f7d-1a5e7c0b9a42"
NAME="MusicBrainz Extended"
OVERVIEW="MusicBrainz metadata + release-type tags."
DESCRIPTION="Drop-in replacement for the built-in MusicBrainz metadata provider that also tags albums with their release-group type (album, ep, single, live, compilation, soundtrack)."
CATEGORY="Metadata"
OWNER="cawdry-dev"
PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
ARTIFACTS="$PROJECT_DIR/artifacts"
STAGING="$ARTIFACTS/staging"
ZIP_NAME="musicbrainzextended_${VERSION}.zip"

export PATH="/usr/local/share/dotnet:$PATH"

rm -rf "$ARTIFACTS"
mkdir -p "$STAGING"

dotnet publish "$PROJECT_DIR/Jellyfin.Plugin.MusicBrainzExtended" \
  -c Release \
  -o "$STAGING" \
  /p:Version="$VERSION" \
  /p:AssemblyVersion="$VERSION" \
  /p:FileVersion="$VERSION"

TIMESTAMP="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

cat > "$STAGING/meta.json" <<EOF
{
  "category": "$CATEGORY",
  "changelog": "Release $VERSION",
  "description": "$DESCRIPTION",
  "guid": "$GUID",
  "name": "$NAME",
  "overview": "$OVERVIEW",
  "owner": "$OWNER",
  "targetAbi": "$TARGET_ABI",
  "timestamp": "$TIMESTAMP",
  "version": "$VERSION"
}
EOF

# Keep only what Jellyfin needs in the zip.
cd "$STAGING"
rm -f Jellyfin.*.xml MetaBrainz.*.xml *.pdb
zip -r "$ARTIFACTS/$ZIP_NAME" \
  Jellyfin.Plugin.MusicBrainzExtended.dll \
  MetaBrainz.MusicBrainz.dll \
  meta.json \
  > /dev/null

CHECKSUM=$(md5 -q "$ARTIFACTS/$ZIP_NAME" 2>/dev/null || md5sum "$ARTIFACTS/$ZIP_NAME" | awk '{print $1}')
SIZE=$(wc -c < "$ARTIFACTS/$ZIP_NAME" | tr -d ' ')

echo ""
echo "==============================================="
echo "Built $ZIP_NAME"
echo "Size:      $SIZE bytes"
echo "MD5:       $CHECKSUM"
echo "Timestamp: $TIMESTAMP"
echo "==============================================="
echo ""
echo "Update manifest.json with:"
echo "  \"version\":   \"$VERSION\""
echo "  \"checksum\":  \"$CHECKSUM\""
echo "  \"timestamp\": \"$TIMESTAMP\""
