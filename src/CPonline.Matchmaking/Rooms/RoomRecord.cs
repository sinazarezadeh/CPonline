using CPonline.Shared.Contracts;

namespace CPonline.Matchmaking.Rooms;

/// <summary>Server-side state for one room: who owns it, when it expires, and the host address
/// once the host's launcher has reported one (after UPnP/STUN discovery, or manual entry).</summary>
public sealed class RoomRecord
{
    public required string RoomCode { get; init; }

    public required string HostToken { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public string? HostIp { get; set; }

    public int? HostPort { get; set; }

    public HostAddressSource? HostAddressSource { get; set; }

    public bool HasHostAddress => HostIp is not null && HostPort is not null && HostAddressSource is not null;
}
