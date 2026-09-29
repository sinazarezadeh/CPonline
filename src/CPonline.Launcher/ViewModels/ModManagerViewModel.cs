using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CPonline.Launcher.Core.Services;

namespace CPonline.Launcher.ViewModels;

/// <summary>Drives the "Mods" tab: detect/browse for the game install, and install/update the
/// pinned mod dependencies from <c>manifests/mods.json</c> into it.</summary>
public sealed class ModManagerViewModel : ObservableObject
{
    private readonly IModManager _modManager;
    private readonly InstalledModStore _installedStore;
    private readonly IGameInstallLocator _installLocator;
    private readonly AppSessionState _session;

    private string _statusMessage = "Detect your Cyberpunk 2077 install, or browse for it, to get started.";

    public ModManagerViewModel(
        IModManager modManager,
        InstalledModStore installedStore,
        IGameInstallLocator installLocator,
        AppSessionState session,
        string manifestPath)
    {
        _modManager = modManager;
        _installedStore = installedStore;
        _installLocator = installLocator;
        _session = session;

        foreach (var entry in ModManifestLoader.Load(manifestPath))
        {
            Mods.Add(new ModRowViewModel { Entry = entry });
        }

        DetectGameCommand = new AsyncRelayCommand(DetectGameAsync);
        InstallAllCommand = new AsyncRelayCommand(InstallAllAsync, () => !string.IsNullOrWhiteSpace(GameRoot));
        InstallModCommand = new AsyncRelayCommand<ModRowViewModel?>(InstallModAsync);

        RefreshStates();
    }

    public ObservableCollection<ModRowViewModel> Mods { get; } = new();

    public string? GameRoot
    {
        get => _session.GameRoot;
        set
        {
            if (_session.GameRoot == value)
            {
                return;
            }

            _session.GameRoot = value;
            OnPropertyChanged();
            InstallAllCommand.NotifyCanExecuteChanged();
            RefreshStates();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public IAsyncRelayCommand DetectGameCommand { get; }

    public IAsyncRelayCommand InstallAllCommand { get; }

    public IAsyncRelayCommand<ModRowViewModel?> InstallModCommand { get; }

    /// <summary>Called by the view after a folder-picker dialog returns a path.</summary>
    public bool TrySetManualGameRoot(string path)
    {
        if (!_installLocator.ValidateManualPath(path))
        {
            StatusMessage = $"'{path}' doesn't look like a Cyberpunk 2077 install " +
                             "(bin\\x64\\Cyberpunk2077.exe wasn't found there).";
            return false;
        }

        GameRoot = path;
        StatusMessage = $"Using: {path}";
        return true;
    }

    private Task DetectGameAsync()
    {
        var result = _installLocator.Locate();
        if (result is not null)
        {
            GameRoot = result.RootPath;
            StatusMessage = $"Found via {result.Source}: {result.RootPath}";
        }
        else
        {
            StatusMessage = "Couldn't auto-detect Cyberpunk 2077 (checked Steam, GOG, Epic). Browse for it manually.";
        }

        return Task.CompletedTask;
    }

    private void RefreshStates()
    {
        var installed = _installedStore.Load();
        foreach (var row in Mods)
        {
            installed.TryGetValue(row.Entry.Id, out var record);
            row.State = _modManager.GetState(row.Entry, record);
        }
    }

    private async Task InstallAllAsync()
    {
        foreach (var row in Mods)
        {
            await InstallModAsync(row).ConfigureAwait(false);
        }
    }

    private async Task InstallModAsync(ModRowViewModel? row)
    {
        if (row is null || string.IsNullOrWhiteSpace(GameRoot))
        {
            return;
        }

        row.IsBusy = true;
        try
        {
            var record = await _modManager.InstallAsync(row.Entry, GameRoot).ConfigureAwait(false);
            _installedStore.Upsert(record);
            row.State = ModInstallState.UpToDate;
            StatusMessage = $"Installed {row.DisplayName} ({row.Entry.Tag}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to install {row.DisplayName}: {ex.Message}";
        }
        finally
        {
            row.IsBusy = false;
        }
    }
}
