using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace CPonline.Launcher.Core.Networking;

public interface IStunClient
{
    /// <summary>Sends a STUN binding request from the given local UDP port to a handful of
    /// public STUN servers and returns the first resolved public ip:port mapping, or null if
    /// none responded in time (e.g. symmetric NAT/CGNAT, or outbound UDP blocked).</summary>
    Task<IPEndPoint?> TryDiscoverPublicEndpointAsync(int localPort, CancellationToken ct = default);
}

/// <summary>Real UDP implementation of <see cref="IStunClient"/>. GameNetworkingSockets (the
/// CyberpunkMP transport) is UDP-based, which is exactly what STUN resolves for.</summary>
public sealed class StunClient : IStunClient
{
    private static readonly (string Host, int Port)[] DefaultStunServers =
    [
        ("stun.l.google.com", 19302),
        ("stun1.l.google.com", 19302),
    ];

    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(2);

    public async Task<IPEndPoint?> TryDiscoverPublicEndpointAsync(int localPort, CancellationToken ct = default)
    {
        using var udpClient = new UdpClient(localPort);
        var transactionId = RandomNumberGenerator.GetBytes(12);
        var request = StunProtocol.BuildBindingRequest(transactionId);

        foreach (var (host, port) in DefaultStunServers)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
                if (addresses.Length == 0)
                {
                    continue;
                }

                var remote = new IPEndPoint(addresses[0], port);
                await udpClient.SendAsync(request, remote, ct).ConfigureAwait(false);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(ResponseTimeout);
                var result = await udpClient.ReceiveAsync(timeoutCts.Token).ConfigureAwait(false);

                if (StunProtocol.TryParseMappedAddress(result.Buffer, transactionId, out var endpoint))
                {
                    return endpoint;
                }
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException && !ct.IsCancellationRequested)
            {
                // this STUN server didn't answer in time - try the next one
            }
        }

        return null;
    }
}
