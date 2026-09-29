using Microsoft.Win32;

namespace CPonline.Launcher.Core.Services;

/// <summary>Thin seam over Windows registry reads so install-detection orchestration can be
/// unit-tested with a fake. The real implementation throws PlatformNotSupportedException if
/// ever invoked on a non-Windows OS, same as the underlying BCL registry API.</summary>
public interface IRegistryReader
{
    string? GetStringValue(RegistryHive hive, string subKey, string valueName);
}
