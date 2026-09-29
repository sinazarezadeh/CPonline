namespace CPonline.Launcher.Core.Services;

public sealed record ProcessStartSpec(string FileName, IReadOnlyList<string> Arguments, string? WorkingDirectory = null);

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>Thin seam over <see cref="System.Diagnostics.Process"/> so orchestration logic
/// (which exe, which args, in what order) can be unit-tested without ever spawning a real
/// process. Used for both the fire-and-forget game launch and the synchronous "docker version"
/// / "docker run" CLI calls.</summary>
public interface IProcessLauncher
{
    /// <summary>Starts a process without waiting for it to exit (game launch, docker run -d). Returns the OS process ID.</summary>
    int StartDetached(ProcessStartSpec spec);

    /// <summary>Starts a process and waits for it to exit, capturing output (docker version, docker build).</summary>
    Task<ProcessResult> RunAsync(ProcessStartSpec spec, CancellationToken ct = default);

    bool IsRunning(int processId);
}
