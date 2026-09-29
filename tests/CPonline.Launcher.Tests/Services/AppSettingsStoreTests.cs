using CPonline.Launcher.Core.Services;
using Xunit;

namespace CPonline.Launcher.Tests.Services;

public class AppSettingsStoreTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("cponline-settings-").FullName;

    private string SettingsPath => Path.Combine(_tempDir, "settings.json");

    [Fact]
    public void Load_NoFileYet_ReturnsDefaults()
    {
        var store = new AppSettingsStore(SettingsPath);

        var settings = store.Load();

        Assert.Null(settings.GameRoot);
        Assert.Equal(11778, settings.LocalPort);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var store = new AppSettingsStore(SettingsPath);
        var settings = new AppSettings { GameRoot = @"C:\Games\Cyberpunk 2077", RelayUrl = "ws://example.invalid/session", LocalPort = 12000 };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(settings.GameRoot, loaded.GameRoot);
        Assert.Equal(settings.RelayUrl, loaded.RelayUrl);
        Assert.Equal(settings.LocalPort, loaded.LocalPort);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);
}

public class AppSessionStateTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("cponline-session-").FullName;

    private string SettingsPath => Path.Combine(_tempDir, "settings.json");

    [Fact]
    public void SettingGameRoot_PersistsToTheStore()
    {
        var store = new AppSettingsStore(SettingsPath);
        var state = new AppSessionState(store);

        state.GameRoot = @"C:\Games\Cyberpunk 2077";

        Assert.Equal(@"C:\Games\Cyberpunk 2077", store.Load().GameRoot);
    }

    [Fact]
    public void Constructor_LoadsExistingSettingsFromDisk()
    {
        var store = new AppSettingsStore(SettingsPath);
        store.Save(new AppSettings { GameRoot = @"D:\Games\Cyberpunk 2077", RelayUrl = "ws://example.invalid/session", LocalPort = 22000 });

        var state = new AppSessionState(store);

        Assert.Equal(@"D:\Games\Cyberpunk 2077", state.GameRoot);
        Assert.Equal("ws://example.invalid/session", state.RelayUrl);
        Assert.Equal(22000, state.LocalPort);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);
}
