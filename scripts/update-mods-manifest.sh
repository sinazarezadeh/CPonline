#!/usr/bin/env bash
# Regenerates src/CPonline.Launcher/manifests/mods.json with real, verified data:
# for each mod's "repo" entry, fetches the latest GitHub release, finds the asset matching
# assetNamePattern, downloads it, and fills in the real "tag" and "sha256".
#
# The manifest ships with placeholder tag/sha256 values (REPLACE_WITH_VERIFIED_TAG / all
# zeros) because this container has no GitHub network access to compute them - see the
# "Milestone 0 - spikes" section of the project plan. Run this script from a machine with
# normal internet access before using ModManager.InstallAsync for real installs.
#
# Requires: curl, jq, sha256sum (or shasum -a 256 on macOS).
set -euo pipefail

MANIFEST="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/src/CPonline.Launcher/manifests/mods.json"
SHA256_CMD="sha256sum"
command -v sha256sum >/dev/null 2>&1 || SHA256_CMD="shasum -a 256"

tmp_manifest="$(mktemp)"
trap 'rm -f "$tmp_manifest"' EXIT

jq -c '.[]' "$MANIFEST" | while read -r entry; do
  id=$(jq -r '.id' <<<"$entry")
  repo=$(jq -r '.repo' <<<"$entry")
  pattern=$(jq -r '.assetNamePattern' <<<"$entry")

  echo "==> $id ($repo)" >&2
  release_json=$(curl -sS -H "User-Agent: CPonline-manifest-updater" "https://api.github.com/repos/$repo/releases/latest")
  tag=$(jq -r '.tag_name' <<<"$release_json")
  asset_url=$(jq -r --arg pat "$pattern" '
    .assets[] | select(.name | test("^" + ($pat | gsub("\\*"; ".*") | gsub("\\?"; ".")) + "$"; "i")) | .browser_download_url
  ' <<<"$release_json" | head -n1)

  if [[ -z "$tag" || "$tag" == "null" || -z "$asset_url" || "$asset_url" == "null" ]]; then
    echo "    WARNING: could not resolve a matching release asset for $id - leaving placeholder values." >&2
    continue
  fi

  asset_file="$(mktemp)"
  curl -sSL -o "$asset_file" "$asset_url"
  sha256=$($SHA256_CMD "$asset_file" | awk '{print $1}')
  rm -f "$asset_file"

  echo "    tag=$tag sha256=$sha256" >&2

  jq --arg id "$id" --arg tag "$tag" --arg sha "$sha256" \
    'map(if .id == $id then .tag = $tag | .sha256 = $sha else . end)' \
    "$MANIFEST" > "$tmp_manifest"
  mv "$tmp_manifest" "$MANIFEST"
done

echo "Done. Review the diff in $MANIFEST before committing." >&2
