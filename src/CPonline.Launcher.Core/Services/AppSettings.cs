namespace CPonline.Launcher.Core.Services;

/// <summary>Small, persisted app-level preferences: where the game is installed, and which
/// matchmaking relay to use. Kept separate from InstalledModStore, which is per-mod state.</summary>
public sealed record AppSettings
{
    public string? GameRoot { get; init; }

    public string RelayUrl { get; init; } = "ws://localhost:5080/session";

    public int LocalPort { get; init; } = 11778;
}
