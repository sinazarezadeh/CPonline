namespace CPonline.Launcher.Tests.Fakes;

/// <summary>Invokes its callback inline on whatever thread calls Report(). The real
/// <see cref="System.Progress{T}"/> always posts through a SynchronizationContext (falling back
/// to ThreadPool.QueueUserWorkItem when none is captured, e.g. in a plain xUnit test), so
/// asserting on captured values right after an awaited call is a race with System.Progress -
/// this type is deterministic instead, for testing ordering/values without that flakiness.</summary>
public sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> _onReport;

    public SynchronousProgress(Action<T> onReport) => _onReport = onReport;

    public void Report(T value) => _onReport(value);
}
