namespace CPonline.Shared.Contracts;

/// <summary>
/// One pinned mod dependency entry from manifests/mods.json. This is the launcher's
/// source of truth for "correctly installed" - always resolved against the mod's own
/// GitHub Releases at install time, never redistributed in this repo.
/// </summary>
public sealed record ModManifestEntry
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>GitHub "owner/repo" the release asset is pulled from.</summary>
    public required string Repo { get; init; }

    /// <summary>Exact release tag to pin to - never "latest".</summary>
    public required string Tag { get; init; }

    /// <summary>Glob-style pattern used to pick the right asset off the release.</summary>
    public required string AssetNamePattern { get; init; }

    /// <summary>SHA-256 of the pinned asset, lowercase hex, computed once against the pinned tag.</summary>
    public required string Sha256 { get; init; }

    /// <summary>Install targets relative to the game root, e.g. "red4ext/plugins/TweakXL".</summary>
    public required IReadOnlyList<string> InstallTargets { get; init; }
}
