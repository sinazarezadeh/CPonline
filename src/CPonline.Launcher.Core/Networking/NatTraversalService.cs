using System.Net;
using CPonline.Shared.Contracts;

namespace CPonline.Launcher.Core.Networking;

public sealed record NatDiscoveryResult(IPAddress Address, int Port, HostAddressSource Source);

public interface INatTraversalService
{
    /// <summary>Tries UPnP/NAT-PMP port forwarding first, falling back to a STUN-resolved public
    /// endpoint if the gateway doesn't support it. Null means neither worked - the host will
    /// need to forward the port manually, or the joiner falls back to direct-connect.</summary>
    Task<NatDiscoveryResult?> DiscoverAsync(int localPort, CancellationToken ct = default);
}

public sealed class NatTraversalService : INatTraversalService
{
    private readonly IUpnpPortMapper _upnp;
    private readonly IStunClient _stun;

    public NatTraversalService(IUpnpPortMapper upnp, IStunClient stun)
    {
        _upnp = upnp;
        _stun = stun;
    }

    public async Task<NatDiscoveryResult?> DiscoverAsync(int localPort, CancellationToken ct = default)
    {
        var externalIp = await _upnp.TryMapPortAsync(localPort, "CPonline CyberpunkMP", ct).ConfigureAwait(false);
        if (externalIp is not null)
        {
            return new NatDiscoveryResult(externalIp, localPort, HostAddressSource.Upnp);
        }

        var endpoint = await _stun.TryDiscoverPublicEndpointAsync(localPort, ct).ConfigureAwait(false);
        return endpoint is not null
            ? new NatDiscoveryResult(endpoint.Address, endpoint.Port, HostAddressSource.Stun)
            : null;
    }
}
