using CPonline.Launcher.Core.Services;
using Xunit;

namespace CPonline.Launcher.Tests.Services;

public class ModManifestLoaderTests
{
    private const string SampleJson = """
        [
          {
            "id": "codeware",
            "displayName": "Codeware",
            "repo": "psiberx/cp2077-codeware",
            "tag": "v1.7.0",
            "assetNamePattern": "codeware-*.zip",
            "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcd",
            "installTargets": ["red4ext/plugins/Codeware"]
          }
        ]
        """;

    [Fact]
    public void Parse_ReadsAllFieldsFromJson()
    {
        var entries = ModManifestLoader.Parse(SampleJson);

        var entry = Assert.Single(entries);
        Assert.Equal("codeware", entry.Id);
        Assert.Equal("psiberx/cp2077-codeware", entry.Repo);
        Assert.Equal("v1.7.0", entry.Tag);
        Assert.Equal("codeware-*.zip", entry.AssetNamePattern);
        Assert.Equal("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcd", entry.Sha256);
        Assert.Equal(["red4ext/plugins/Codeware"], entry.InstallTargets);
    }

    [Fact]
    public void Load_ReadsFromDisk()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, SampleJson);
            var entries = ModManifestLoader.Load(tempFile);
            Assert.Single(entries);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Parse_TheShippedManifest_IsWellFormed()
    {
        var manifestPath = FindShippedManifest();
        var entries = ModManifestLoader.Load(manifestPath);

        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Id)));
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Repo)));
        Assert.All(entries, e => Assert.NotEmpty(e.InstallTargets));

        // Every id must be unique - ModManager/InstalledModStore key by it.
        var duplicateIds = entries.GroupBy(e => e.Id).Where(g => g.Count() > 1).Select(g => g.Key);
        Assert.Empty(duplicateIds);
    }

    private static string FindShippedManifest()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "src", "CPonline.Launcher", "manifests", "mods.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        throw new FileNotFoundException("Could not locate src/CPonline.Launcher/manifests/mods.json from the test output directory.");
    }
}
