namespace CPonline.Launcher.ViewModels;

/// <summary>Root view model - just groups the four tabs' view models for MainWindow's DataContext.</summary>
public sealed class MainViewModel
{
    public MainViewModel(
        ModManagerViewModel modManager,
        HostSessionViewModel hostSession,
        JoinSessionViewModel joinSession,
        SettingsViewModel settings)
    {
        ModManager = modManager;
        HostSession = hostSession;
        JoinSession = joinSession;
        Settings = settings;
    }

    public ModManagerViewModel ModManager { get; }

    public HostSessionViewModel HostSession { get; }

    public JoinSessionViewModel JoinSession { get; }

    public SettingsViewModel Settings { get; }
}
