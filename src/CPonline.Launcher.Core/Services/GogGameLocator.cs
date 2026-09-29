using System.Runtime.Versioning;
using Microsoft.Win32;

namespace CPonline.Launcher.Core.Services;

public interface IGogGameLocator
{
    string? FindInstallPath();
}

/// <summary>Locates Cyberpunk 2077 via GOG Galaxy's per-game registry key.</summary>
[SupportedOSPlatform("windows")]
public sealed class GogGameLocator : IGogGameLocator
{
    /// <summary>
    /// GOG's numeric game ID for Cyberpunk 2077. GOG product IDs are not always stable across
    /// re-releases/editions - confirm this against a real GOG install (Milestone 0 spike in the
    /// project plan) before relying on it for anything beyond a best-effort auto-detect.
    /// </summary>
    public const string CyberpunkGogGameId = "1423049311";

    private readonly IRegistryReader _registry;

    public GogGameLocator(IRegistryReader registry) => _registry = registry;

    public string? FindInstallPath() =>
        _registry.GetStringValue(
            RegistryHive.LocalMachine,
            $@"SOFTWARE\WOW6432Node\GOG.com\Games\{CyberpunkGogGameId}",
            "path");
}
