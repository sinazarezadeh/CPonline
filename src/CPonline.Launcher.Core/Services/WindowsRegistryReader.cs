using System.Runtime.Versioning;
using Microsoft.Win32;

namespace CPonline.Launcher.Core.Services;

/// <summary>Real Windows registry implementation of <see cref="IRegistryReader"/>.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsRegistryReader : IRegistryReader
{
    public string? GetStringValue(RegistryHive hive, string subKey, string valueName)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(subKey);
        return key?.GetValue(valueName) as string;
    }
}
