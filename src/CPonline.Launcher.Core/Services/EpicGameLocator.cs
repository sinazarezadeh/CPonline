using System.Text.Json;

namespace CPonline.Launcher.Core.Services;

public interface IEpicGameLocator
{
    string? FindInstallPath();
}

/// <summary>Locates Cyberpunk 2077 by scanning the Epic Games Launcher's install manifests
/// (one JSON *.item file per installed game) for a matching DisplayName.</summary>
public sealed class EpicGameLocator : IEpicGameLocator
{
    public const string ManifestsDirectory = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";

    private readonly IFileSystemProbe _fileSystem;

    public EpicGameLocator(IFileSystemProbe fileSystem) => _fileSystem = fileSystem;

    public string? FindInstallPath()
    {
        foreach (var file in _fileSystem.EnumerateFiles(ManifestsDirectory, "*.item"))
        {
            string json;
            try
            {
                json = _fileSystem.ReadAllText(file);
            }
            catch (IOException)
            {
                continue;
            }

            if (TryGetInstallLocation(json, out var installLocation))
            {
                return installLocation;
            }
        }

        return null;
    }

    /// <summary>Exposed internally so the manifest-matching logic can be unit-tested against
    /// fixture JSON without touching the filesystem.</summary>
    internal static bool TryGetInstallLocation(string manifestJson, out string? installLocation)
    {
        installLocation = null;
        try
        {
            using var document = JsonDocument.Parse(manifestJson);
            var root = document.RootElement;
            var displayName = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() : null;
            var isCyberpunk = displayName is not null &&
                               displayName.Contains("Cyberpunk 2077", StringComparison.OrdinalIgnoreCase);

            if (!isCyberpunk || !root.TryGetProperty("InstallLocation", out var location))
            {
                return false;
            }

            installLocation = location.GetString();
            return installLocation is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
