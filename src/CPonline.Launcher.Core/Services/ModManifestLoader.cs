using System.Text.Json;
using CPonline.Shared.Contracts;

namespace CPonline.Launcher.Core.Services;

/// <summary>Reads the pinned mod dependency list out of manifests/mods.json - the launcher's
/// source of truth for which mods exist, which exact release tag each is pinned to, and the
/// expected SHA-256 of each pinned asset.</summary>
public static class ModManifestLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<ModManifestEntry> Load(string manifestPath)
    {
        var json = File.ReadAllText(manifestPath);
        return Parse(json);
    }

    public static IReadOnlyList<ModManifestEntry> Parse(string json) =>
        JsonSerializer.Deserialize<List<ModManifestEntry>>(json, Options)
        ?? throw new InvalidOperationException("mods.json parsed to null - expected a JSON array of mod entries.");
}
