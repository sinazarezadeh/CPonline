using CommunityToolkit.Mvvm.ComponentModel;
using CPonline.Launcher.Core.Services;
using CPonline.Shared.Contracts;

namespace CPonline.Launcher.ViewModels;

public sealed class ModRowViewModel : ObservableObject
{
    private ModInstallState _state = ModInstallState.NotInstalled;
    private bool _isBusy;

    public required ModManifestEntry Entry { get; init; }

    public string DisplayName => Entry.DisplayName;

    public string Tag => Entry.Tag;

    public ModInstallState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }
}
