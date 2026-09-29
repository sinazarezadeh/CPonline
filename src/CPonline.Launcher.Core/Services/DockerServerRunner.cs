using System.ComponentModel;

namespace CPonline.Launcher.Core.Services;

public interface IDockerServerRunner
{
    Task<bool> IsDockerAvailableAsync(CancellationToken ct = default);

    /// <summary>Runs the CyberpunkMP dedicated server image, mapping the given host port to the
    /// container's default port 11778. Returns the new container ID.</summary>
    Task<string> StartServerAsync(int hostPort, CancellationToken ct = default);

    Task StopServerAsync(string containerId, CancellationToken ct = default);
}

/// <summary>Runs the CyberpunkMP dedicated server via the `docker` CLI, per its documented
/// deployment (`docker build . -tag cyberpunkmp`, `docker run -p 11778:11778 cyberpunkmp`).
/// Whether a non-Docker native run path exists is an open Milestone 0 spike item; Docker is the
/// only confirmed path today.</summary>
public sealed class DockerServerRunner : IDockerServerRunner
{
    public const string ImageName = "cyberpunkmp";
    public const int ContainerPort = 11778;

    private readonly IProcessLauncher _processLauncher;

    public DockerServerRunner(IProcessLauncher processLauncher) => _processLauncher = processLauncher;

    public async Task<bool> IsDockerAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var spec = new ProcessStartSpec("docker", ["version", "--format", "{{.Server.Version}}"]);
            var result = await _processLauncher.RunAsync(spec, ct).ConfigureAwait(false);
            return result.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            // docker CLI isn't on PATH at all.
            return false;
        }
    }

    public async Task<string> StartServerAsync(int hostPort, CancellationToken ct = default)
    {
        var spec = new ProcessStartSpec("docker", ["run", "-d", "-p", $"{hostPort}:{ContainerPort}", ImageName]);
        var result = await _processLauncher.RunAsync(spec, ct).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            if (result.StandardError.Contains("pull access denied", StringComparison.OrdinalIgnoreCase) ||
                result.StandardError.Contains("Unable to find image", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The '{ImageName}' Docker image hasn't been built yet - it isn't published anywhere public. " +
                    "Clone https://github.com/tiltedphoques/CyberpunkMP, run 'git submodule update --init', then " +
                    $"'docker build . -tag {ImageName}' from inside it (see that repo's README for details).");
            }

            throw new InvalidOperationException($"Failed to start the CyberpunkMP server container: {result.StandardError}");
        }

        return result.StandardOutput.Trim();
    }

    public async Task StopServerAsync(string containerId, CancellationToken ct = default)
    {
        var spec = new ProcessStartSpec("docker", ["stop", containerId]);
        var result = await _processLauncher.RunAsync(spec, ct).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to stop container '{containerId}': {result.StandardError}");
        }
    }
}
