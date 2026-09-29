using CPonline.Launcher.Core.Services;

namespace CPonline.Launcher.Tests.Fakes;

public sealed class FakeSteamGameLocator : ISteamGameLocator
{
    public IReadOnlyList<string> CandidateRoots { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> FindCandidateRoots() => CandidateRoots;
}

public sealed class FakeGogGameLocator : IGogGameLocator
{
    public string? InstallPath { get; set; }

    public string? FindInstallPath() => InstallPath;
}

public sealed class FakeEpicGameLocator : IEpicGameLocator
{
    public string? InstallPath { get; set; }

    public string? FindInstallPath() => InstallPath;
}
