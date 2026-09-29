using CommunityToolkit.Mvvm.ComponentModel;

namespace CPonline.Launcher.Core.Services;

/// <summary>Shared, observable app state (current game install, relay URL, local port, default
/// server address) - a singleton so every tab of the launcher (mod manager, play, host, join)
/// sees the same values, kept in sync with disk via <see cref="AppSettingsStore"/>.</summary>
public sealed class AppSessionState : ObservableObject
{
    private readonly AppSettingsStore _store;

    private string? _gameRoot;
    private string _relayUrl;
    private int _localPort;
    private string _defaultServerAddress;

    public AppSessionState(AppSettingsStore store)
    {
        _store = store;
        var settings = store.Load();
        _gameRoot = settings.GameRoot;
        _relayUrl = settings.RelayUrl;
        _localPort = settings.LocalPort;
        _defaultServerAddress = settings.DefaultServerAddress;
    }

    public string? GameRoot
    {
        get => _gameRoot;
        set
        {
            if (SetProperty(ref _gameRoot, value))
            {
                Persist();
            }
        }
    }

    public string RelayUrl
    {
        get => _relayUrl;
        set
        {
            if (SetProperty(ref _relayUrl, value))
            {
                Persist();
            }
        }
    }

    public int LocalPort
    {
        get => _localPort;
        set
        {
            if (SetProperty(ref _localPort, value))
            {
                Persist();
            }
        }
    }

    /// <summary>The "ip:port" a non-technical user pastes once on the Play tab; remembered for next time.</summary>
    public string DefaultServerAddress
    {
        get => _defaultServerAddress;
        set
        {
            if (SetProperty(ref _defaultServerAddress, value))
            {
                Persist();
            }
        }
    }

    private void Persist() => _store.Save(new AppSettings
    {
        GameRoot = _gameRoot,
        RelayUrl = _relayUrl,
        LocalPort = _localPort,
        DefaultServerAddress = _defaultServerAddress,
    });
}
