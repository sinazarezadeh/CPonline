using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using CPonline.Matchmaking.Rooms;
using CPonline.Shared.Contracts;

namespace CPonline.Matchmaking.Sockets;

/// <summary>Drives the room-code signaling protocol (<see cref="MatchmakingMessage"/>) over a
/// single WebSocket connection. The message-handling logic in <see cref="Handle"/> is pure -
/// deliberately factored out of the socket read/write loop - so it's unit-testable without a
/// real socket; only the loop itself needs an integration test.</summary>
public sealed class SessionSocketHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly TimeSpan RoomTtl = TimeSpan.FromMinutes(30);

    private readonly IRoomStore _rooms;

    public SessionSocketHandler(IRoomStore rooms) => _rooms = rooms;

    public async Task HandleAsync(WebSocket socket, CancellationToken ct)
    {
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            try
            {
                var received = await ReceiveMessageAsync(socket, ct).ConfigureAwait(false);
                if (received is null)
                {
                    break; // client closed the connection
                }

                // ReceiveMessageAsync already returns a MatchmakingErrorResponse for a message it
                // couldn't parse - send that directly rather than routing it back through Handle(),
                // which would otherwise stamp it with the generic "Unsupported message type" text.
                var response = received is MatchmakingErrorResponse parseError ? parseError : Handle(received);
                await SendAsync(socket, response, ct).ConfigureAwait(false);
            }
            catch (WebSocketException)
            {
                break;
            }
            catch (IOException)
            {
                // The client dropped the connection without a clean close handshake.
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    /// <summary>Pure request -> response mapping, with no socket I/O - the part worth unit-testing directly.</summary>
    internal MatchmakingMessage Handle(MatchmakingMessage message) => message switch
    {
        CreateRoomRequest => HandleCreateRoom(),
        ReportHostAddressRequest report => HandleReportHostAddress(report),
        ResolveRoomRequest resolve => HandleResolveRoom(resolve),
        _ => new MatchmakingErrorResponse { Code = MatchmakingErrorCodes.MalformedMessage, Message = "Unsupported message type." },
    };

    private RoomCreatedResponse HandleCreateRoom()
    {
        var room = _rooms.CreateRoom(RoomTtl);
        return new RoomCreatedResponse { RoomCode = room.RoomCode, HostToken = room.HostToken, ExpiresAtUtc = room.ExpiresAtUtc };
    }

    private MatchmakingMessage HandleReportHostAddress(ReportHostAddressRequest request)
    {
        var reported = _rooms.TryReportHostAddress(request.RoomCode, request.HostToken, request.Ip, request.Port, request.Source);
        if (!reported)
        {
            return new MatchmakingErrorResponse
            {
                Code = MatchmakingErrorCodes.InvalidHostToken,
                Message = "Unknown room, expired room, or invalid host token.",
            };
        }

        return new RoomResolvedResponse
        {
            RoomCode = request.RoomCode,
            Ip = request.Ip,
            Port = request.Port,
            Source = request.Source,
        };
    }

    private MatchmakingMessage HandleResolveRoom(ResolveRoomRequest request)
    {
        var room = _rooms.Get(request.RoomCode);
        if (room is null)
        {
            return new MatchmakingErrorResponse
            {
                Code = MatchmakingErrorCodes.RoomNotFound,
                Message = $"No active room with code '{request.RoomCode}'.",
            };
        }

        if (!room.HasHostAddress)
        {
            return new MatchmakingErrorResponse
            {
                Code = MatchmakingErrorCodes.HostAddressNotReported,
                Message = "The host hasn't reported a connectable address yet - ask them to try again, or connect manually.",
            };
        }

        return new RoomResolvedResponse
        {
            RoomCode = room.RoomCode,
            Ip = room.HostIp!,
            Port = room.HostPort!.Value,
            Source = room.HostAddressSource!.Value,
        };
    }

    private static async Task<MatchmakingMessage?> ReceiveMessageAsync(WebSocket socket, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(chunk, ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, ct).ConfigureAwait(false);
                return null;
            }

            buffer.Write(chunk, 0, result.Count);
        }
        while (!result.EndOfMessage);

        buffer.Position = 0;
        try
        {
            return await JsonSerializer.DeserializeAsync<MatchmakingMessage>(buffer, JsonOptions, ct).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return new MatchmakingErrorResponse { Code = MatchmakingErrorCodes.MalformedMessage, Message = "Could not parse the message." };
        }
    }

    private static async Task SendAsync(WebSocket socket, MatchmakingMessage message, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
    }
}
