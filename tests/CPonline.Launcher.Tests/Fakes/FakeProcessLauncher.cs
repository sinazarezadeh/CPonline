using CPonline.Launcher.Core.Services;

namespace CPonline.Launcher.Tests.Fakes;

public sealed class FakeProcessLauncher : IProcessLauncher
{
    public List<ProcessStartSpec> DetachedStarts { get; } = new();

    public List<ProcessStartSpec> RunCalls { get; } = new();

    public int NextProcessId { get; set; } = 1234;

    public ProcessResult NextRunResult { get; set; } = new(0, string.Empty, string.Empty);

    public bool RunningState { get; set; } = true;

    public int StartDetached(ProcessStartSpec spec)
    {
        DetachedStarts.Add(spec);
        return NextProcessId;
    }

    public Task<ProcessResult> RunAsync(ProcessStartSpec spec, CancellationToken ct = default)
    {
        RunCalls.Add(spec);
        return Task.FromResult(NextRunResult);
    }

    public bool IsRunning(int processId) => RunningState;
}
