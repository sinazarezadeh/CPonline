namespace CPonline.Launcher.ViewModels;

/// <summary>Root view model - just groups the tabs' view models for MainWindow's DataContext.</summary>
public sealed class MainViewModel
{
    public MainViewModel(
        PlayViewModel play,
        ModManagerViewModel modManager,
        HostSessionViewModel hostSession,
        JoinSessionViewModel joinSession,
        SettingsViewModel settings)
    {
        Play = play;
        ModManager = modManager;
        HostSession = hostSession;
        JoinSession = joinSession;
        Settings = settings;
    }

    public PlayViewModel Play { get; }

    public ModManagerViewModel ModManager { get; }

    public HostSessionViewModel HostSession { get; }

    public JoinSessionViewModel JoinSession { get; }

    public SettingsViewModel Settings { get; }
}
