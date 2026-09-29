using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPonline.Launcher.Core.Networking;
using CPonline.Launcher.Core.Services;

namespace CPonline.Launcher.ViewModels;

/// <summary>Drives the "Host" tab: optionally starts a local CyberpunkMP server, creates a
/// matchmaking room, tries UPnP/STUN to make it reachable, and launches the game.</summary>
public sealed class HostSessionViewModel : ObservableObject, IDisposable
{
    private readonly AppSessionState _session;
    private readonly ILaunchOrchestrator _launchOrchestrator;
    private readonly IDockerServerRunner _dockerRunner;
    private readonly INatTraversalService _natTraversal;
    private readonly Func<IMatchmakingClient> _matchmakingClientFactory;

    private IMatchmakingClient? _matchmakingClient;
    private string? _hostToken;
    private string? _containerId;

    private bool _useLocalDockerServer = true;
    private bool _isBusy;
    private string? _roomCode;
    private string? _discoveredAddress;
    private string _statusMessage = "Set your game install in the Mods tab, then start hosting.";

    public HostSessionViewModel(
        AppSessionState session,
        ILaunchOrchestrator launchOrchestrator,
        IDockerServerRunner dockerRunner,
        INatTraversalService natTraversal,
        Func<IMatchmakingClient> matchmakingClientFactory)
    {
        _session = session;
        _launchOrchestrator = launchOrchestrator;
        _dockerRunner = dockerRunner;
        _natTraversal = natTraversal;
        _matchmakingClientFactory = matchmakingClientFactory;

        StartHostingCommand = new AsyncRelayCommand(StartHostingAsync, () => !IsBusy);
        LaunchGameCommand = new RelayCommand(LaunchGame);
        StopHostingCommand = new AsyncRelayCommand(StopHostingAsync, () => RoomCode is not null);
    }

    public bool UseLocalDockerServer
    {
        get => _useLocalDockerServer;
        set => SetProperty(ref _useLocalDockerServer, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                StartHostingCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? RoomCode
    {
        get => _roomCode;
        set
        {
            if (SetProperty(ref _roomCode, value))
            {
                StopHostingCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? DiscoveredAddress
    {
        get => _discoveredAddress;
        set => SetProperty(ref _discoveredAddress, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public IAsyncRelayCommand StartHostingCommand { get; }

    public IRelayCommand LaunchGameCommand { get; }

    public IAsyncRelayCommand StopHostingCommand { get; }

    private async Task StartHostingAsync()
    {
        if (string.IsNullOrWhiteSpace(_session.GameRoot))
        {
            StatusMessage = "Set your Cyberpunk 2077 install folder in the Mods tab first.";
            return;
        }

        IsBusy = true;
        try
        {
            if (UseLocalDockerServer)
            {
                StatusMessage = "Checking for Docker...";
                if (!await _dockerRunner.IsDockerAvailableAsync().ConfigureAwait(false))
                {
                    StatusMessage = "Docker isn't available. Install Docker Desktop, or turn off " +
                                     "'run local server' and host the CyberpunkMP server yourself.";
                    return;
                }

                StatusMessage = "Starting the CyberpunkMP server...";
                _containerId = await _dockerRunner.StartServerAsync(_session.LocalPort).ConfigureAwait(false);
            }

            StatusMessage = "Connecting to the matchmaking relay...";
            _matchmakingClient = _matchmakingClientFactory();
            await _matchmakingClient.ConnectAsync(new Uri(_session.RelayUrl)).ConfigureAwait(false);

            var room = await _matchmakingClient.CreateRoomAsync().ConfigureAwait(false);
            RoomCode = room.RoomCode;
            _hostToken = room.HostToken;

            StatusMessage = "Trying to open your connection (UPnP/STUN)...";
            var discovered = await _natTraversal.DiscoverAsync(_session.LocalPort).ConfigureAwait(false);
            if (discovered is not null)
            {
                DiscoveredAddress = $"{discovered.Address}:{discovered.Port} ({discovered.Source})";
                await _matchmakingClient.ReportHostAddressAsync(
                    RoomCode, _hostToken, discovered.Address.ToString(), discovered.Port, discovered.Source).ConfigureAwait(false);
                StatusMessage = $"Room {RoomCode} is ready - share the code with your friend.";
            }
            else
            {
                DiscoveredAddress = null;
                StatusMessage = $"Room {RoomCode} created, but automatic NAT setup failed. " +
                                 $"Forward UDP port {_session.LocalPort} on your router, then share the code.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start hosting: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LaunchGame()
    {
        if (string.IsNullOrWhiteSpace(_session.GameRoot))
        {
            StatusMessage = "Set your Cyberpunk 2077 install folder first.";
            return;
        }

        _launchOrchestrator.LaunchGame(_session.GameRoot, new ConnectionTarget("127.0.0.1", _session.LocalPort));
        StatusMessage = "Launching Cyberpunk 2077...";
    }

    private async Task StopHostingAsync()
    {
        if (_containerId is not null)
        {
            try
            {
                await _dockerRunner.StopServerAsync(_containerId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Couldn't stop the local server cleanly: {ex.Message}";
            }

            _containerId = null;
        }

        if (_matchmakingClient is not null)
        {
            await _matchmakingClient.DisposeAsync().ConfigureAwait(false);
            _matchmakingClient = null;
        }

        RoomCode = null;
        DiscoveredAddress = null;
        _hostToken = null;
        StatusMessage = "Stopped hosting.";
    }

    public void Dispose()
    {
        if (_matchmakingClient is IAsyncDisposable disposable)
        {
            _ = disposable.DisposeAsync();
        }
    }
}
