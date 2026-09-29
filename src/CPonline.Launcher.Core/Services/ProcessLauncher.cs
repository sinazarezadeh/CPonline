using System.Diagnostics;

namespace CPonline.Launcher.Core.Services;

/// <summary>Real implementation of <see cref="IProcessLauncher"/> over <see cref="Process"/>.</summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    public int StartDetached(ProcessStartSpec spec)
    {
        var startInfo = BuildStartInfo(spec, redirectOutput: false);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process '{spec.FileName}'.");
        return process.Id;
    }

    public async Task<ProcessResult> RunAsync(ProcessStartSpec spec, CancellationToken ct = default)
    {
        var startInfo = BuildStartInfo(spec, redirectOutput: true);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process '{spec.FileName}'.");

        var stdOutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        return new ProcessResult(process.ExitCode, await stdOutTask, await stdErrTask);
    }

    public bool IsRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static ProcessStartInfo BuildStartInfo(ProcessStartSpec spec, bool redirectOutput)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            WorkingDirectory = spec.WorkingDirectory ?? string.Empty,
            UseShellExecute = false,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput,
        };

        foreach (var argument in spec.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
