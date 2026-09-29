using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPonline.Launcher.Core.Networking;
using CPonline.Launcher.Core.Services;
using CPonline.Shared.Contracts;

namespace CPonline.Launcher.ViewModels;

/// <summary>Drives the "Join" tab: resolve a room code through the matchmaking relay, or connect
/// directly to a manually entered ip:port, then launch the game against that target.</summary>
public sealed class JoinSessionViewModel : ObservableObject
{
    private readonly AppSessionState _session;
    private readonly ILaunchOrchestrator _launchOrchestrator;
    private readonly Func<IMatchmakingClient> _matchmakingClientFactory;

    private string _roomCodeInput = string.Empty;
    private string _manualIp = string.Empty;
    private int _manualPort = 11778;
    private bool _isBusy;
    private string _statusMessage = "Enter a room code from your host, or connect directly by IP.";

    public JoinSessionViewModel(AppSessionState session, ILaunchOrchestrator launchOrchestrator, Func<IMatchmakingClient> matchmakingClientFactory)
    {
        _session = session;
        _launchOrchestrator = launchOrchestrator;
        _matchmakingClientFactory = matchmakingClientFactory;
        _manualPort = session.LocalPort;

        JoinViaRoomCodeCommand = new AsyncRelayCommand(JoinViaRoomCodeAsync, () => !IsBusy && RoomCodeInput.Trim().Length > 0);
        JoinManuallyCommand = new RelayCommand(JoinManually, () => !IsBusy && ManualIp.Trim().Length > 0);
    }

    public string RoomCodeInput
    {
        get => _roomCodeInput;
        set
        {
            if (SetProperty(ref _roomCodeInput, value))
            {
                JoinViaRoomCodeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ManualIp
    {
        get => _manualIp;
        set
        {
            if (SetProperty(ref _manualIp, value))
            {
                JoinManuallyCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int ManualPort
    {
        get => _manualPort;
        set => SetProperty(ref _manualPort, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                JoinViaRoomCodeCommand.NotifyCanExecuteChanged();
                JoinManuallyCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public IAsyncRelayCommand JoinViaRoomCodeCommand { get; }

    public IRelayCommand JoinManuallyCommand { get; }

    private async Task JoinViaRoomCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(_session.GameRoot))
        {
            StatusMessage = "Set your Cyberpunk 2077 install folder in the Mods tab first.";
            return;
        }

        IsBusy = true;
        try
        {
            StatusMessage = "Connecting to the matchmaking relay...";
            await using var client = _matchmakingClientFactory();
            await client.ConnectAsync(new Uri(_session.RelayUrl)).ConfigureAwait(false);

            var resolved = await client.ResolveRoomAsync(RoomCodeInput.Trim().ToUpperInvariant()).ConfigureAwait(false);

            StatusMessage = $"Connecting to {resolved.Ip}:{resolved.Port}...";
            _launchOrchestrator.LaunchGame(_session.GameRoot, new ConnectionTarget(resolved.Ip, resolved.Port));
            StatusMessage = "Launching Cyberpunk 2077...";
        }
        catch (MatchmakingException ex)
        {
            StatusMessage = ex.Code switch
            {
                MatchmakingErrorCodes.RoomNotFound => "No room with that code was found - double check it with your host.",
                MatchmakingErrorCodes.HostAddressNotReported => "Your host hasn't finished setting up their connection yet - try again in a moment.",
                _ => $"Couldn't join: {ex.Message}",
            };
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't join: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void JoinManually()
    {
        if (string.IsNullOrWhiteSpace(_session.GameRoot))
        {
            StatusMessage = "Set your Cyberpunk 2077 install folder in the Mods tab first.";
            return;
        }

        _launchOrchestrator.LaunchGame(_session.GameRoot, new ConnectionTarget(ManualIp.Trim(), ManualPort));
        StatusMessage = $"Launching Cyberpunk 2077, connecting to {ManualIp}:{ManualPort}...";
    }
}
