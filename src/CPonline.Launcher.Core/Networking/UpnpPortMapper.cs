using System.Net;
using Open.Nat;

namespace CPonline.Launcher.Core.Networking;

public interface IUpnpPortMapper
{
    /// <summary>Asks the LAN gateway to forward the given UDP port to this machine. Returns the
    /// gateway's external IP on success, or null if no UPnP/NAT-PMP-capable gateway responded in
    /// time - common when UPnP is disabled on the router - so callers should fall back to STUN.</summary>
    Task<IPAddress?> TryMapPortAsync(int port, string description, CancellationToken ct = default);

    Task TryRemoveMappingAsync(int port, CancellationToken ct = default);
}

/// <summary>Real UPnP/NAT-PMP implementation over the Open.Nat library.</summary>
public sealed class UpnpPortMapper : IUpnpPortMapper
{
    private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(5);

    public async Task<IPAddress?> TryMapPortAsync(int port, string description, CancellationToken ct = default)
    {
        try
        {
            var device = await DiscoverDeviceAsync().ConfigureAwait(false);
            if (device is null)
            {
                return null;
            }

            await device.CreatePortMapAsync(new Mapping(Protocol.Udp, port, port, description)).ConfigureAwait(false);
            return await device.GetExternalIPAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is NatDeviceNotFoundException or MappingException)
        {
            return null;
        }
    }

    public async Task TryRemoveMappingAsync(int port, CancellationToken ct = default)
    {
        try
        {
            var device = await DiscoverDeviceAsync().ConfigureAwait(false);
            if (device is null)
            {
                return;
            }

            await device.DeletePortMapAsync(new Mapping(Protocol.Udp, port, port)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is NatDeviceNotFoundException or MappingException)
        {
            // best-effort cleanup only - the mapping will also expire on its own
        }
    }

    private static async Task<NatDevice?> DiscoverDeviceAsync()
    {
        try
        {
            var discoverer = new NatDiscoverer();
            using var discoveryCts = new CancellationTokenSource(DiscoveryTimeout);
            return await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, discoveryCts).ConfigureAwait(false);
        }
        catch (NatDeviceNotFoundException)
        {
            return null;
        }
    }
}
