using CommunityToolkit.Mvvm.ComponentModel;
using CPonline.Launcher.Core.Services;

namespace CPonline.Launcher.ViewModels;

/// <summary>Drives the "Settings" tab: the matchmaking relay to use and the local UDP port
/// CyberpunkMP listens on. Both are read/written straight through to <see cref="AppSessionState"/>,
/// which persists them to disk on every change.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppSessionState _session;

    public SettingsViewModel(AppSessionState session) => _session = session;

    public string RelayUrl
    {
        get => _session.RelayUrl;
        set
        {
            if (_session.RelayUrl == value)
            {
                return;
            }

            _session.RelayUrl = value;
            OnPropertyChanged();
        }
    }

    public int LocalPort
    {
        get => _session.LocalPort;
        set
        {
            if (_session.LocalPort == value)
            {
                return;
            }

            _session.LocalPort = value;
            OnPropertyChanged();
        }
    }
}
