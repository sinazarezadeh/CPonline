namespace CPonline.Launcher.Core.Steam;

/// <summary>
/// Reads Steam library paths and per-library installed app IDs out of a parsed
/// libraryfolders.vdf. Pure functions over already-loaded text, so tests can hand in a
/// fixture string without needing a real Steam install.
/// </summary>
public static class SteamLibraryFolders
{
    public static IReadOnlyList<string> GetLibraryPaths(string vdfText)
    {
        var libraryFolders = VdfParser.Parse(vdfText)["libraryfolders"];
        if (libraryFolders is null)
        {
            return Array.Empty<string>();
        }

        var paths = new List<string>();
        foreach (var (_, node) in libraryFolders.Children)
        {
            if (node["path"]?.Value is { } path)
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    /// <summary>Library paths whose "apps" block lists the given Steam app ID as installed.</summary>
    public static IReadOnlyList<string> GetLibraryPathsContainingApp(string vdfText, string appId)
    {
        var libraryFolders = VdfParser.Parse(vdfText)["libraryfolders"];
        if (libraryFolders is null)
        {
            return Array.Empty<string>();
        }

        var matches = new List<string>();
        foreach (var (_, node) in libraryFolders.Children)
        {
            if (node["path"]?.Value is not { } path)
            {
                continue;
            }

            var apps = node["apps"];
            var hasApp = apps?.Children.Any(c => string.Equals(c.Key, appId, StringComparison.OrdinalIgnoreCase)) ?? false;
            if (hasApp)
            {
                matches.Add(path);
            }
        }

        return matches;
    }
}
