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
# A failure on one mod (wrong repo slug, no matching asset, network hiccup) is reported and
# skipped - it never aborts the rest of the batch. When a release is found but no asset
# matches assetNamePattern, the actual asset names are printed so the pattern can be fixed
# without needing to open GitHub in a browser.
#
# Requires: curl, jq, sha256sum (or shasum -a 256 on macOS).
set -uo pipefail

MANIFEST="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/src/CPonline.Launcher/manifests/mods.json"
SHA256_CMD="sha256sum"
command -v sha256sum >/dev/null 2>&1 || SHA256_CMD="shasum -a 256"

tmp_manifest="$(mktemp)"
trap 'rm -f "$tmp_manifest"' EXIT

mod_ids=$(jq -r '.[].id' "$MANIFEST")

for id in $mod_ids; do
  entry=$(jq -c --arg id "$id" '.[] | select(.id == $id)' "$MANIFEST")
  repo=$(jq -r '.repo' <<<"$entry")
  pattern=$(jq -r '.assetNamePattern' <<<"$entry")

  echo "==> $id ($repo)" >&2

  release_json=$(curl -sS -H "User-Agent: CPonline-manifest-updater" "https://api.github.com/repos/$repo/releases/latest")
  if [[ -z "$release_json" ]]; then
    echo "    WARNING: no response fetching release info for $id - leaving placeholder values." >&2
    continue
  fi

  api_error=$(jq -r '.message // empty' <<<"$release_json" 2>/dev/null)
  if [[ -n "$api_error" ]]; then
    echo "    WARNING: GitHub API error for $repo: $api_error - the repo slug in mods.json is probably wrong. Leaving placeholder values." >&2
    continue
  fi

  tag=$(jq -r '.tag_name // empty' <<<"$release_json")
  asset_url=$(jq -r --arg pat "$pattern" '
    (.assets // [])[] | select(.name | test("^" + ($pat | gsub("\\*"; ".*") | gsub("\\?"; ".")) + "$"; "i")) | .browser_download_url
  ' <<<"$release_json" | head -n1)

  if [[ -z "$tag" || -z "$asset_url" ]]; then
    echo "    WARNING: no asset in the latest release ($tag) matches pattern '$pattern'. Leaving placeholder values." >&2
    asset_names=$(jq -r '(.assets // [])[].name' <<<"$release_json")
    if [[ -n "$asset_names" ]]; then
      echo "    Actual asset names in that release:" >&2
      echo "$asset_names" | sed 's/^/      - /' >&2
    else
      echo "    That release has no assets attached at all." >&2
    fi
    continue
  fi

  asset_file="$(mktemp)"
  if ! curl -sSL -o "$asset_file" "$asset_url"; then
    echo "    WARNING: failed to download $asset_url - leaving placeholder values." >&2
    rm -f "$asset_file"
    continue
  fi

  sha256=$($SHA256_CMD "$asset_file" | awk '{print $1}')
  rm -f "$asset_file"

  echo "    tag=$tag sha256=$sha256" >&2

  jq --arg id "$id" --arg tag "$tag" --arg sha "$sha256" \
    'map(if .id == $id then .tag = $tag | .sha256 = $sha else . end)' \
    "$MANIFEST" > "$tmp_manifest"
  mv "$tmp_manifest" "$MANIFEST"
done

echo "Done. Review the diff in $MANIFEST before committing." >&2
