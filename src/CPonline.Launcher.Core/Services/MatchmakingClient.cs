using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using CPonline.Shared.Contracts;

namespace CPonline.Launcher.Core.Services;

public sealed class MatchmakingException : Exception
{
    public string Code { get; }

    public MatchmakingException(string code, string message) : base(message) => Code = code;
}

public interface IMatchmakingClient : IAsyncDisposable
{
    Task ConnectAsync(Uri relayUri, CancellationToken ct = default);

    Task<RoomCreatedResponse> CreateRoomAsync(CancellationToken ct = default);

    Task ReportHostAddressAsync(string roomCode, string hostToken, string ip, int port, HostAddressSource source, CancellationToken ct = default);

    Task<RoomResolvedResponse> ResolveRoomAsync(string roomCode, CancellationToken ct = default);
}

/// <summary>WebSocket client for the room-code signaling protocol described in
/// <see cref="MatchmakingMessage"/>. Only ever carries small JSON control messages - the actual
/// CyberpunkMP game traffic goes directly between the two players once a room resolves.</summary>
public sealed class MatchmakingClient : IMatchmakingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private ClientWebSocket? _socket;

    public async Task ConnectAsync(Uri relayUri, CancellationToken ct = default)
    {
        var socket = new ClientWebSocket();
        await socket.ConnectAsync(relayUri, ct).ConfigureAwait(false);
        _socket = socket;
    }

    public Task<RoomCreatedResponse> CreateRoomAsync(CancellationToken ct = default) =>
        SendAndReceiveAsync<RoomCreatedResponse>(new CreateRoomRequest(), ct);

    public Task ReportHostAddressAsync(string roomCode, string hostToken, string ip, int port, HostAddressSource source, CancellationToken ct = default) =>
        SendAsync(
            new ReportHostAddressRequest { RoomCode = roomCode, HostToken = hostToken, Ip = ip, Port = port, Source = source },
            ct);

    public Task<RoomResolvedResponse> ResolveRoomAsync(string roomCode, CancellationToken ct = default) =>
        SendAndReceiveAsync<RoomResolvedResponse>(new ResolveRoomRequest { RoomCode = roomCode }, ct);

    private async Task SendAsync(MatchmakingMessage message, CancellationToken ct)
    {
        var socket = EnsureConnected();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
    }

    private async Task<TResponse> SendAndReceiveAsync<TResponse>(MatchmakingMessage message, CancellationToken ct)
        where TResponse : MatchmakingMessage
    {
        await SendAsync(message, ct).ConfigureAwait(false);
        var response = await ReceiveAsync(ct).ConfigureAwait(false);

        if (response is MatchmakingErrorResponse error)
        {
            throw new MatchmakingException(error.Code, error.Message);
        }

        return response as TResponse
            ?? throw new InvalidOperationException($"Expected a {typeof(TResponse).Name} but received {response.GetType().Name}.");
    }

    private async Task<MatchmakingMessage> ReceiveAsync(CancellationToken ct)
    {
        var socket = EnsureConnected();
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(chunk, ct).ConfigureAwait(false);
            buffer.Write(chunk, 0, result.Count);
        }
        while (!result.EndOfMessage);

        buffer.Position = 0;
        return await JsonSerializer.DeserializeAsync<MatchmakingMessage>(buffer, JsonOptions, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Received an empty matchmaking message.");
    }

    private ClientWebSocket EnsureConnected() =>
        _socket is { State: WebSocketState.Open } socket
            ? socket
            : throw new InvalidOperationException("Not connected to the matchmaking relay. Call ConnectAsync first.");

    public async ValueTask DisposeAsync()
    {
        if (_socket is null)
        {
            return;
        }

        if (_socket.State == WebSocketState.Open)
        {
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None).ConfigureAwait(false);
        }

        _socket.Dispose();
    }
}
