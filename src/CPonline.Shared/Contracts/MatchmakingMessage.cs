using System.Text.Json.Serialization;

namespace CPonline.Shared.Contracts;

/// <summary>
/// WebSocket signaling protocol between CPonline.Launcher and CPonline.Matchmaking.
/// The relay only ever carries these small control messages - never game traffic.
/// Discriminated by the "type" JSON property so both sides can (de)serialize with
/// a single System.Text.Json call against this shared base type.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CreateRoomRequest), "create_room")]
[JsonDerivedType(typeof(RoomCreatedResponse), "room_created")]
[JsonDerivedType(typeof(ReportHostAddressRequest), "report_address")]
[JsonDerivedType(typeof(ResolveRoomRequest), "resolve_room")]
[JsonDerivedType(typeof(RoomResolvedResponse), "room_resolved")]
[JsonDerivedType(typeof(MatchmakingErrorResponse), "error")]
public abstract record MatchmakingMessage;

/// <summary>Host -> relay: "give me a room code".</summary>
public sealed record CreateRoomRequest : MatchmakingMessage;

/// <summary>Relay -> host: the new room's code and the token needed to report/update its address.</summary>
public sealed record RoomCreatedResponse : MatchmakingMessage
{
    public required string RoomCode { get; init; }

    public required string HostToken { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }
}

/// <summary>Host -> relay: the address a joiner should be given for this room, once NAT setup resolved one.</summary>
public sealed record ReportHostAddressRequest : MatchmakingMessage
{
    public required string RoomCode { get; init; }

    public required string HostToken { get; init; }

    public required string Ip { get; init; }

    public required int Port { get; init; }

    public required HostAddressSource Source { get; init; }
}

/// <summary>Joiner -> relay: "resolve this room code to a host address".</summary>
public sealed record ResolveRoomRequest : MatchmakingMessage
{
    public required string RoomCode { get; init; }
}

/// <summary>Relay -> joiner: the resolved host address, once the host has reported one.</summary>
public sealed record RoomResolvedResponse : MatchmakingMessage
{
    public required string RoomCode { get; init; }

    public required string Ip { get; init; }

    public required int Port { get; init; }

    public required HostAddressSource Source { get; init; }
}

/// <summary>Relay -> either side: something went wrong (unknown code, expired room, host unreachable, ...).</summary>
public sealed record MatchmakingErrorResponse : MatchmakingMessage
{
    public required string Code { get; init; }

    public required string Message { get; init; }
}

/// <summary>How a host's address was discovered - informs the joiner's UI and retry behavior.</summary>
public enum HostAddressSource
{
    Manual,
    Upnp,
    Stun,
}
