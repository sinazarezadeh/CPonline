using System.Net.Http;
using System.Windows;
using CPonline.Launcher.Core.Networking;
using CPonline.Launcher.Core.Services;
using CPonline.Launcher.ViewModels;
using CPonline.Launcher.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CPonline.Launcher;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // Persisted state
        services.AddSingleton(_ => new InstalledModStore(InstalledModStore.DefaultStorePath()));
        services.AddSingleton(_ => new AppSettingsStore(AppSettingsStore.DefaultPath()));
        services.AddSingleton<AppSessionState>();

        // Game install detection
        services.AddSingleton<IFileSystemProbe, FileSystemProbe>();
        services.AddSingleton<IRegistryReader, WindowsRegistryReader>();
        services.AddSingleton<ISteamGameLocator, SteamGameLocator>();
        services.AddSingleton<IGogGameLocator, GogGameLocator>();
        services.AddSingleton<IEpicGameLocator, EpicGameLocator>();
        services.AddSingleton<IGameInstallLocator, GameInstallLocator>();

        // Mod install/update
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IGitHubReleaseClient, GitHubReleaseClient>();
        services.AddSingleton<IModManager, ModManager>();

        // Launch + local server
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        services.AddSingleton<ILaunchOrchestrator, LaunchOrchestrator>();
        services.AddSingleton<IDockerServerRunner, DockerServerRunner>();

        // NAT traversal
        services.AddSingleton<IUpnpPortMapper, UpnpPortMapper>();
        services.AddSingleton<IStunClient, StunClient>();
        services.AddSingleton<INatTraversalService, NatTraversalService>();

        // Matchmaking - transient because each Host/Join session opens its own socket.
        services.AddTransient<IMatchmakingClient, MatchmakingClient>();
        services.AddSingleton<Func<IMatchmakingClient>>(sp => sp.GetRequiredService<IMatchmakingClient>);

        // View models
        services.AddSingleton(sp => new ModManagerViewModel(
            sp.GetRequiredService<IModManager>(),
            sp.GetRequiredService<InstalledModStore>(),
            sp.GetRequiredService<IGameInstallLocator>(),
            sp.GetRequiredService<AppSessionState>(),
            Path.Combine(AppContext.BaseDirectory, "manifests", "mods.json")));
        services.AddSingleton<HostSessionViewModel>();
        services.AddSingleton<JoinSessionViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
    }
}
