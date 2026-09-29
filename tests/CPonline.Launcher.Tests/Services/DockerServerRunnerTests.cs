using CPonline.Launcher.Core.Services;
using CPonline.Launcher.Tests.Fakes;
using Xunit;

namespace CPonline.Launcher.Tests.Services;

public class DockerServerRunnerTests
{
    [Fact]
    public async Task IsDockerAvailableAsync_ExitCodeZero_ReturnsTrue()
    {
        var processLauncher = new FakeProcessLauncher { NextRunResult = new ProcessResult(0, "27.0.0", "") };
        var runner = new DockerServerRunner(processLauncher);

        Assert.True(await runner.IsDockerAvailableAsync());
    }

    [Fact]
    public async Task IsDockerAvailableAsync_NonZeroExitCode_ReturnsFalse()
    {
        var processLauncher = new FakeProcessLauncher { NextRunResult = new ProcessResult(1, "", "error") };
        var runner = new DockerServerRunner(processLauncher);

        Assert.False(await runner.IsDockerAvailableAsync());
    }

    [Fact]
    public async Task StartServerAsync_MapsHostPortToTheContainersDefaultPort()
    {
        var processLauncher = new FakeProcessLauncher { NextRunResult = new ProcessResult(0, "container123\n", "") };
        var runner = new DockerServerRunner(processLauncher);

        var containerId = await runner.StartServerAsync(11778);

        Assert.Equal("container123", containerId);
        var spec = Assert.Single(processLauncher.RunCalls);
        Assert.Equal("docker", spec.FileName);
        Assert.Contains("11778:11778", spec.Arguments);
        Assert.Contains(DockerServerRunner.ImageName, spec.Arguments);
    }

    [Fact]
    public async Task StartServerAsync_NonZeroExitCode_ThrowsWithStdErr()
    {
        var processLauncher = new FakeProcessLauncher { NextRunResult = new ProcessResult(1, "", "no such image") };
        var runner = new DockerServerRunner(processLauncher);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StartServerAsync(11778));
        Assert.Contains("no such image", ex.Message);
    }

    [Fact]
    public async Task StartServerAsync_ImageNotBuiltYet_ThrowsAHelpfulBuildItYourselfMessage()
    {
        // The real error text docker prints when the "cyberpunkmp" image has never been built locally.
        const string dockerStdErr =
            "Unable to find image 'cyberpunkmp:latest' locally\n" +
            "docker: Error response from daemon: pull access denied for cyberpunkmp, repository does not exist or may require 'docker login'";
        var processLauncher = new FakeProcessLauncher { NextRunResult = new ProcessResult(125, "", dockerStdErr) };
        var runner = new DockerServerRunner(processLauncher);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StartServerAsync(11778));

        Assert.Contains("hasn't been built yet", ex.Message);
        Assert.Contains("tiltedphoques/CyberpunkMP", ex.Message);
        Assert.Contains("docker build . -tag cyberpunkmp", ex.Message);
    }

    [Fact]
    public async Task StopServerAsync_NonZeroExitCode_Throws()
    {
        var processLauncher = new FakeProcessLauncher { NextRunResult = new ProcessResult(1, "", "no such container") };
        var runner = new DockerServerRunner(processLauncher);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StopServerAsync("container123"));
    }
}
