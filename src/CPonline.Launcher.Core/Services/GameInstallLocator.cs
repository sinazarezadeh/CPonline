namespace CPonline.Launcher.Core.Services;

public enum GameInstallSource
{
    Steam,
    Gog,
    Epic,
    Manual,
}

public sealed record GameInstallResult(string RootPath, GameInstallSource Source);

public interface IGameInstallLocator
{
    /// <summary>Tries Steam, then GOG, then Epic, returning the first root whose
    /// bin\x64\Cyberpunk2077.exe actually exists. Null if none of them found anything valid -
    /// the caller should fall back to a manual folder picker plus <see cref="ValidateManualPath"/>.</summary>
    GameInstallResult? Locate();

    bool ValidateManualPath(string candidateRoot);
}

/// <summary>Orchestrates the per-storefront locators in priority order and validates each
/// candidate the same way regardless of source, so "is this really a Cyberpunk 2077 install"
/// has exactly one definition.</summary>
public sealed class GameInstallLocator : IGameInstallLocator
{
    private static readonly string[] GameExeRelativePath = ["bin", "x64", "Cyberpunk2077.exe"];

    private readonly ISteamGameLocator _steam;
    private readonly IGogGameLocator _gog;
    private readonly IEpicGameLocator _epic;
    private readonly IFileSystemProbe _fileSystem;

    public GameInstallLocator(ISteamGameLocator steam, IGogGameLocator gog, IEpicGameLocator epic, IFileSystemProbe fileSystem)
    {
        _steam = steam;
        _gog = gog;
        _epic = epic;
        _fileSystem = fileSystem;
    }

    public GameInstallResult? Locate()
    {
        foreach (var candidate in _steam.FindCandidateRoots())
        {
            if (ValidateManualPath(candidate))
            {
                return new GameInstallResult(candidate, GameInstallSource.Steam);
            }
        }

        var gogPath = _gog.FindInstallPath();
        if (gogPath is not null && ValidateManualPath(gogPath))
        {
            return new GameInstallResult(gogPath, GameInstallSource.Gog);
        }

        var epicPath = _epic.FindInstallPath();
        if (epicPath is not null && ValidateManualPath(epicPath))
        {
            return new GameInstallResult(epicPath, GameInstallSource.Epic);
        }

        return null;
    }

    public bool ValidateManualPath(string candidateRoot)
    {
        if (string.IsNullOrWhiteSpace(candidateRoot))
        {
            return false;
        }

        var exePath = Path.Combine([candidateRoot, .. GameExeRelativePath]);
        return _fileSystem.FileExists(exePath);
    }
}
