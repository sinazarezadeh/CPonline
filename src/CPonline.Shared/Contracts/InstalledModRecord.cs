namespace CPonline.Shared.Contracts;

/// <summary>
/// Persisted per-mod install state, stored in %LOCALAPPDATA%/CPonline/installed.json.
/// Lets the launcher tell "needs update" apart from "already installed" without
/// re-downloading, and lets uninstall/reinstall remove exactly the files it wrote.
/// </summary>
public sealed record InstalledModRecord
{
    public required string ModId { get; init; }

    public required string Tag { get; init; }

    public required string Sha256 { get; init; }

    public required DateTimeOffset InstalledAtUtc { get; init; }

    /// <summary>Absolute paths of every file this mod wrote, for precise removal.</summary>
    public required IReadOnlyList<string> Files { get; init; }
}
