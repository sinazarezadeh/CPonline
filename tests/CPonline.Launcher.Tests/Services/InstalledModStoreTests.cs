using CPonline.Launcher.Core.Services;
using CPonline.Shared.Contracts;
using Xunit;

namespace CPonline.Launcher.Tests.Services;

public class InstalledModStoreTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("cponline-tests-").FullName;

    private string StorePath => Path.Combine(_tempDir, "installed.json");

    [Fact]
    public void Load_NoFileYet_ReturnsEmpty()
    {
        var store = new InstalledModStore(StorePath);

        Assert.Empty(store.Load());
    }

    [Fact]
    public void Upsert_ThenLoad_RoundTripsTheRecord()
    {
        var store = new InstalledModStore(StorePath);
        var record = new InstalledModRecord
        {
            ModId = "codeware",
            Tag = "v1.7.0",
            Sha256 = "abc123",
            InstalledAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Files = ["C:\\Game\\red4ext\\plugins\\Codeware\\Codeware.dll"],
        };

        store.Upsert(record);
        var loaded = store.Load();

        var actual = Assert.Single(loaded).Value;
        Assert.Equal(record.ModId, actual.ModId);
        Assert.Equal(record.Tag, actual.Tag);
        Assert.Equal(record.Sha256, actual.Sha256);
        Assert.Equal(record.Files, actual.Files);
    }

    [Fact]
    public void Upsert_ExistingModId_OverwritesInPlace()
    {
        var store = new InstalledModStore(StorePath);
        store.Upsert(new InstalledModRecord { ModId = "codeware", Tag = "v1.0", Sha256 = "old", InstalledAtUtc = DateTimeOffset.UtcNow, Files = [] });
        store.Upsert(new InstalledModRecord { ModId = "codeware", Tag = "v2.0", Sha256 = "new", InstalledAtUtc = DateTimeOffset.UtcNow, Files = [] });

        var loaded = store.Load();

        Assert.Single(loaded);
        Assert.Equal("v2.0", loaded["codeware"].Tag);
    }

    [Fact]
    public void Remove_DeletesOnlyTheGivenMod()
    {
        var store = new InstalledModStore(StorePath);
        store.Upsert(new InstalledModRecord { ModId = "codeware", Tag = "v1.0", Sha256 = "a", InstalledAtUtc = DateTimeOffset.UtcNow, Files = [] });
        store.Upsert(new InstalledModRecord { ModId = "tweak-xl", Tag = "v1.0", Sha256 = "b", InstalledAtUtc = DateTimeOffset.UtcNow, Files = [] });

        store.Remove("codeware");
        var loaded = store.Load();

        Assert.Single(loaded);
        Assert.True(loaded.ContainsKey("tweak-xl"));
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);
}
