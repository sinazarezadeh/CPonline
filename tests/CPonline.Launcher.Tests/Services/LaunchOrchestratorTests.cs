using CPonline.Launcher.Core.Services;
using CPonline.Launcher.Tests.Fakes;
using Xunit;

namespace CPonline.Launcher.Tests.Services;

public class LaunchOrchestratorTests
{
    [Fact]
    public void BuildArguments_MatchesConfirmedRed4extLaunchParameterContract()
    {
        var orchestrator = new LaunchOrchestrator(new FakeProcessLauncher());

        var args = orchestrator.BuildArguments(new ConnectionTarget("203.0.113.5", 11778));

        Assert.Equal(["-online", "-ip=203.0.113.5", "-port=11778"], args);
    }

    [Fact]
    public void LaunchGame_StartsTheExeDirectly_NotASteamUri()
    {
        var processLauncher = new FakeProcessLauncher();
        var orchestrator = new LaunchOrchestrator(processLauncher);
        var gameRoot = Path.Combine("C", "Games", "Cyberpunk 2077");

        orchestrator.LaunchGame(gameRoot, new ConnectionTarget("127.0.0.1", 11778));

        var spec = Assert.Single(processLauncher.DetachedStarts);
        Assert.Equal(Path.Combine(gameRoot, "bin", "x64", "Cyberpunk2077.exe"), spec.FileName);
        Assert.Equal(["-online", "-ip=127.0.0.1", "-port=11778"], spec.Arguments);
        Assert.DoesNotContain("steam:", spec.FileName);
    }

    [Fact]
    public void LaunchGame_ReturnsTheProcessIdFromTheLauncher()
    {
        var processLauncher = new FakeProcessLauncher { NextProcessId = 4242 };
        var orchestrator = new LaunchOrchestrator(processLauncher);

        var pid = orchestrator.LaunchGame("C:\\Games\\Cyberpunk 2077", new ConnectionTarget("127.0.0.1", 11778));

        Assert.Equal(4242, pid);
    }
}
