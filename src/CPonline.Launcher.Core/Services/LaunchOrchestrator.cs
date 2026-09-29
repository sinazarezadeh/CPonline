namespace CPonline.Launcher.Core.Services;

public sealed record ConnectionTarget(string Ip, int Port);

public interface ILaunchOrchestrator
{
    /// <summary>The confirmed RED4ext launch-parameter hand-off: the game reads these argv
    /// flags at startup and connects on demand from the in-game menu - no config file or IPC
    /// needed. Verified against CyberpunkMP's own source (Settings.cpp / NetworkWorldSystem.cpp).</summary>
    IReadOnlyList<string> BuildArguments(ConnectionTarget target);

    /// <summary>Launches Cyberpunk2077.exe directly (not a steam:// URI, matching the official
    /// CyberpunkMP launcher's own precedent) with the connection target's argv flags. Returns
    /// the OS process ID so callers can track "game running" state.</summary>
    int LaunchGame(string gameRoot, ConnectionTarget target);
}

public sealed class LaunchOrchestrator : ILaunchOrchestrator
{
    private static readonly string[] GameExeRelativePath = ["bin", "x64", "Cyberpunk2077.exe"];

    private readonly IProcessLauncher _processLauncher;

    public LaunchOrchestrator(IProcessLauncher processLauncher) => _processLauncher = processLauncher;

    public IReadOnlyList<string> BuildArguments(ConnectionTarget target) =>
    [
        "-online",
        $"-ip={target.Ip}",
        $"-port={target.Port}",
    ];

    public int LaunchGame(string gameRoot, ConnectionTarget target)
    {
        var exePath = Path.Combine([gameRoot, .. GameExeRelativePath]);
        var spec = new ProcessStartSpec(exePath, BuildArguments(target), Path.GetDirectoryName(exePath));
        return _processLauncher.StartDetached(spec);
    }
}
