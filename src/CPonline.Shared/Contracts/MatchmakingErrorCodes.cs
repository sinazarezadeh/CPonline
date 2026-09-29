namespace CPonline.Shared.Contracts;

/// <summary>Well-known <see cref="MatchmakingErrorResponse.Code"/> values, shared so client and
/// server never drift on the string literals used to distinguish error handling.</summary>
public static class MatchmakingErrorCodes
{
    public const string RoomNotFound = "room_not_found";
    public const string RoomExpired = "room_expired";
    public const string HostAddressNotReported = "host_address_not_reported";
    public const string InvalidHostToken = "invalid_host_token";
    public const string MalformedMessage = "malformed_message";
}
