using System.Text.Json;
using CPonline.Shared.Contracts;

namespace CPonline.Launcher.Core.Services;

/// <summary>Persists per-mod install state (%LOCALAPPDATA%\CPonline\installed.json on Windows)
/// so <see cref="ModInstallStateEvaluator"/> can tell "needs update" apart from "already
/// installed" without re-downloading, and so uninstall/reinstall can remove exactly the files
/// a mod wrote.</summary>
public sealed class InstalledModStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _storePath;

    public InstalledModStore(string storePath) => _storePath = storePath;

    public static string DefaultStorePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CPonline", "installed.json");

    public IReadOnlyDictionary<string, InstalledModRecord> Load()
    {
        if (!File.Exists(_storePath))
        {
            return new Dictionary<string, InstalledModRecord>();
        }

        var json = File.ReadAllText(_storePath);
        return Parse(json);
    }

    public static IReadOnlyDictionary<string, InstalledModRecord> Parse(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, InstalledModRecord>>(json, Options)
        ?? new Dictionary<string, InstalledModRecord>();

    public void Save(IReadOnlyDictionary<string, InstalledModRecord> records)
    {
        var directory = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_storePath, JsonSerializer.Serialize(records, Options));
    }

    public void Upsert(InstalledModRecord record)
    {
        var current = new Dictionary<string, InstalledModRecord>(Load());
        current[record.ModId] = record;
        Save(current);
    }

    public void Remove(string modId)
    {
        var current = new Dictionary<string, InstalledModRecord>(Load());
        current.Remove(modId);
        Save(current);
    }
}
