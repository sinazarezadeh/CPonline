using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPonline.Launcher.Core.Services;

namespace CPonline.Launcher.ViewModels;

/// <summary>Drives the "Play" tab - the only screen a non-technical friend should ever need.
/// Detecting the game, installing/updating mods, and launching are all done for them behind one
/// button; the only thing they're expected to type in is the server address someone gave them.</summary>
public sealed class PlayViewModel : ObservableObject
{
    private readonly ModManagerViewModel _modManager;
    private readonly AppSessionState _session;
    private readonly ILaunchOrchestrator _launchOrchestrator;

    private bool _isBusy;
    private string _statusMessage = "Enter the server address a friend gave you, then hit Play.";

    public PlayViewModel(ModManagerViewModel modManager, AppSessionState session, ILaunchOrchestrator launchOrchestrator)
    {
        _modManager = modManager;
        _session = session;
        _launchOrchestrator = launchOrchestrator;

        PlayCommand = new AsyncRelayCommand(PlayAsync, () => !IsBusy);

        // Bubble up changes from the underlying mod manager (game root, mod list, its own
        // progress) so this view model's bindings stay live without duplicating that state.
        _modManager.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(ModManagerViewModel.GameRoot):
                    OnPropertyChanged(nameof(GameRoot));
                    OnPropertyChanged(nameof(GameStatusText));
                    break;
                case nameof(ModManagerViewModel.IsInstallingAll):
                    OnPropertyChanged(nameof(IsInstallingMods));
                    break;
                case nameof(ModManagerViewModel.OverallProgressPercent):
                    OnPropertyChanged(nameof(ModsProgressPercent));
                    break;
            }
        };
        Mods.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ModsStatusText));
        foreach (var row in Mods)
        {
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(ModRowViewModel.State))
                {
                    OnPropertyChanged(nameof(ModsStatusText));
                }
            };
        }
    }

    public ObservableCollection<ModRowViewModel> Mods => _modManager.Mods;

    public string? GameRoot => _modManager.GameRoot;

    public string GameStatusText => string.IsNullOrWhiteSpace(GameRoot)
        ? "Game not found yet - click Browse below."
        : $"Game found: {GameRoot}";

    public string ModsStatusText
    {
        get
        {
            var needingWork = Mods.Count(m => m.State != ModInstallState.UpToDate);
            return needingWork == 0
                ? "Mods are up to date."
                : $"{needingWork} of {Mods.Count} mods need to be installed - Play will do this automatically.";
        }
    }

    public string ServerAddress
    {
        get => _session.DefaultServerAddress;
        set
        {
            if (_session.DefaultServerAddress == value)
            {
                return;
            }

            _session.DefaultServerAddress = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                PlayCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsInstallingMods => _modManager.IsInstallingAll;

    public double ModsProgressPercent => _modManager.OverallProgressPercent;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public IAsyncRelayCommand PlayCommand { get; }

    /// <summary>Called by the view after a folder-picker dialog returns a path.</summary>
    public bool TrySetManualGameRoot(string path) => _modManager.TrySetManualGameRoot(path);

    private async Task PlayAsync()
    {
        IsBusy = true;
        try
        {
            if (string.IsNullOrWhiteSpace(GameRoot))
            {
                StatusMessage = "Looking for Cyberpunk 2077...";
                await _modManager.DetectGameCommand.ExecuteAsync(null);
            }

            if (string.IsNullOrWhiteSpace(GameRoot))
            {
                StatusMessage = "Couldn't find Cyberpunk 2077 automatically - click Browse and select your game folder.";
                return;
            }

            if (Mods.Any(m => m.State != ModInstallState.UpToDate))
            {
                StatusMessage = "Setting up mods (only needed the first time, or after an update)...";
                await _modManager.InstallAllCommand.ExecuteAsync(null);

                if (Mods.Any(m => m.State != ModInstallState.UpToDate))
                {
                    StatusMessage = $"Mod setup failed: {_modManager.StatusMessage}";
                    return;
                }
            }

            if (!ServerAddressParser.TryParse(ServerAddress, out var host, out var port))
            {
                StatusMessage = "Enter the server address your friend gave you, like 203.0.113.5:11778.";
                return;
            }

            _launchOrchestrator.LaunchGame(GameRoot, new ConnectionTarget(host, port));
            StatusMessage = "Launching Cyberpunk 2077...";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
