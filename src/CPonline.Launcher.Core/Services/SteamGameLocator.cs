using System.Runtime.Versioning;
using CPonline.Launcher.Core.Steam;
using Microsoft.Win32;

namespace CPonline.Launcher.Core.Services;

public interface ISteamGameLocator
{
    /// <summary>Candidate "Cyberpunk 2077" install roots across every Steam library that
    /// reports the app installed. Empty if Steam isn't found or the app isn't in any library.</summary>
    IReadOnlyList<string> FindCandidateRoots();
}

/// <summary>Locates Cyberpunk 2077 via Steam's own library metadata: the registry points at
/// Steam's install path, and steamapps/libraryfolders.vdf lists every library plus which
/// app IDs are installed in each.</summary>
[SupportedOSPlatform("windows")]
public sealed class SteamGameLocator : ISteamGameLocator
{
    /// <summary>Cyberpunk 2077's Steam app ID (store.steampowered.com/app/1091500).</summary>
    public const string CyberpunkAppId = "1091500";

    private readonly IRegistryReader _registry;
    private readonly IFileSystemProbe _fileSystem;

    public SteamGameLocator(IRegistryReader registry, IFileSystemProbe fileSystem)
    {
        _registry = registry;
        _fileSystem = fileSystem;
    }

    public IReadOnlyList<string> FindCandidateRoots()
    {
        var steamPath = _registry.GetStringValue(RegistryHive.CurrentUser, @"SOFTWARE\Valve\Steam", "SteamPath");
        if (string.IsNullOrWhiteSpace(steamPath))
        {
            return Array.Empty<string>();
        }

        var libraryFoldersVdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!_fileSystem.FileExists(libraryFoldersVdf))
        {
            return Array.Empty<string>();
        }

        var vdfText = _fileSystem.ReadAllText(libraryFoldersVdf);
        var libraries = SteamLibraryFolders.GetLibraryPathsContainingApp(vdfText, CyberpunkAppId);
        return libraries.Select(lib => Path.Combine(lib, "steamapps", "common", "Cyberpunk 2077")).ToList();
    }
}
