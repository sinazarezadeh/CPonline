using CommunityToolkit.Mvvm.ComponentModel;
using CPonline.Launcher.Core.Services;
using CPonline.Shared.Contracts;

namespace CPonline.Launcher.ViewModels;

public sealed class ModRowViewModel : ObservableObject
{
    private ModInstallState _state = ModInstallState.NotInstalled;
    private bool _isBusy;
    private double _progressPercent;
    private bool _isIndeterminate;
    private string _progressText = string.Empty;

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

    /// <summary>0-100. Meaningless while <see cref="IsIndeterminate"/> is true.</summary>
    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set => SetProperty(ref _isIndeterminate, value);
    }

    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    /// <summary>Called from an <see cref="IProgress{T}"/> callback (always marshaled back onto
    /// the UI thread by <see cref="Progress{T}"/> when constructed there) to update the row's
    /// bindable progress state for one <see cref="ModManager.InstallAsync"/> phase.</summary>
    public void UpdateProgress(ModInstallProgress progress)
    {
        IsIndeterminate = progress.PercentComplete is null;
        ProgressPercent = (progress.PercentComplete ?? 0) * 100;
        ProgressText = progress.Phase switch
        {
            ModInstallPhase.Downloading => $"Downloading {ProgressPercent:0}%",
            ModInstallPhase.Verifying => "Verifying checksum...",
            ModInstallPhase.Extracting => $"Extracting {ProgressPercent:0}%",
            _ => string.Empty,
        };
    }

    public void ResetProgress()
    {
        ProgressPercent = 0;
        IsIndeterminate = false;
        ProgressText = string.Empty;
    }
}
