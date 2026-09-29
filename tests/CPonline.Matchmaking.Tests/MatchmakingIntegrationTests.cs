using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using CPonline.Shared.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CPonline.Matchmaking.Tests;

/// <summary>End-to-end tests against the real ASP.NET Core pipeline (in-memory TestServer,
/// no real sockets) - verifies the actual wire protocol, not just <see cref="Sockets.SessionSocketHandler.Handle"/>'s logic.</summary>
public class MatchmakingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly WebApplicationFactory<Program> _factory;

    public MatchmakingIntegrationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task FullRoomLifecycle_CreateReportResolve_WorksOverARealWebSocket()
    {
        using var socket = await ConnectAsync();

        var created = await SendAndReceiveAsync<RoomCreatedResponse>(socket, new CreateRoomRequest());
        Assert.Equal(6, created.RoomCode.Length);

        var reported = await SendAndReceiveAsync<RoomResolvedResponse>(socket, new ReportHostAddressRequest
        {
            RoomCode = created.RoomCode,
            HostToken = created.HostToken,
            Ip = "198.51.100.20",
            Port = 11778,
            Source = HostAddressSource.Upnp,
        });
        Assert.Equal("198.51.100.20", reported.Ip);

        var resolved = await SendAndReceiveAsync<RoomResolvedResponse>(socket, new ResolveRoomRequest { RoomCode = created.RoomCode });
        Assert.Equal("198.51.100.20", resolved.Ip);
        Assert.Equal(11778, resolved.Port);
        Assert.Equal(HostAddressSource.Upnp, resolved.Source);
    }

    [Fact]
    public async Task ResolveUnknownRoom_ReturnsAnErrorMessage_OverTheWire()
    {
        using var socket = await ConnectAsync();

        var error = await SendAndReceiveAsync<MatchmakingErrorResponse>(socket, new ResolveRoomRequest { RoomCode = "ZZZZZZ" });

        Assert.Equal(MatchmakingErrorCodes.RoomNotFound, error.Code);
    }

    [Fact]
    public async Task DifferentConnections_ShareTheSameRoomRegistry()
    {
        using var hostSocket = await ConnectAsync();
        using var joinerSocket = await ConnectAsync();

        var created = await SendAndReceiveAsync<RoomCreatedResponse>(hostSocket, new CreateRoomRequest());
        await SendAndReceiveAsync<RoomResolvedResponse>(hostSocket, new ReportHostAddressRequest
        {
            RoomCode = created.RoomCode,
            HostToken = created.HostToken,
            Ip = "198.51.100.30",
            Port = 11778,
            Source = HostAddressSource.Stun,
        });

        // The joiner never created the room, and never learned the host token - it only has the code.
        var resolved = await SendAndReceiveAsync<RoomResolvedResponse>(joinerSocket, new ResolveRoomRequest { RoomCode = created.RoomCode });

        Assert.Equal("198.51.100.30", resolved.Ip);
    }

    [Fact]
    public async Task ReportHostAddress_WithNamedEnumOnTheWire_RoundTripsCorrectly()
    {
        // Regression test: HostAddressSource used to (de)serialize as a raw number with no
        // JsonStringEnumConverter configured, so a client sending the named form ("Upnp") - as
        // any hand-written or non-generated client reasonably would - got back a confusing
        // "Unsupported message type" error instead of a real one. Exercises the raw wire format
        // directly (not through MatchmakingClient) so it actually proves the JSON contract.
        using var socket = await ConnectAsync();

        await socket.SendAsync("""{"type":"create_room"}"""u8.ToArray(), WebSocketMessageType.Text, true, CancellationToken.None);
        var created = await ReceiveRawAsync<RoomCreatedResponse>(socket);

        var reportJson = $$"""
            {"type":"report_address","roomCode":"{{created.RoomCode}}","hostToken":"{{created.HostToken}}","ip":"198.51.100.40","port":11778,"source":"Upnp"}
            """;
        await socket.SendAsync(System.Text.Encoding.UTF8.GetBytes(reportJson), WebSocketMessageType.Text, true, CancellationToken.None);

        var reported = await ReceiveRawAsync<RoomResolvedResponse>(socket);
        Assert.Equal("198.51.100.40", reported.Ip);
        Assert.Equal(HostAddressSource.Upnp, reported.Source);
    }

    [Fact]
    public async Task MalformedMessage_ReturnsItsOwnErrorText_NotTheGenericUnsupportedTypeText()
    {
        using var socket = await ConnectAsync();

        // A well-formed discriminator but a value that can't bind (bad enum) used to get masked
        // by Handle()'s generic "Unsupported message type" text - assert the real message survives.
        const string badEnumJson = """{"type":"report_address","roomCode":"AAAAAA","hostToken":"x","ip":"1.2.3.4","port":1,"source":"NotARealSource"}""";
        await socket.SendAsync(System.Text.Encoding.UTF8.GetBytes(badEnumJson), WebSocketMessageType.Text, true, CancellationToken.None);

        var error = await ReceiveRawAsync<MatchmakingErrorResponse>(socket);
        Assert.Equal(MatchmakingErrorCodes.MalformedMessage, error.Code);
        Assert.Contains("parse", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<TResponse> ReceiveRawAsync<TResponse>(WebSocket socket) where TResponse : MatchmakingMessage
    {
        var buffer = new byte[4096];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        var response = JsonSerializer.Deserialize<MatchmakingMessage>(buffer.AsSpan(0, result.Count), JsonOptions);
        return Assert.IsType<TResponse>(response);
    }

    private async Task<WebSocket> ConnectAsync()
    {
        var client = _factory.Server.CreateWebSocketClient();
        return await client.ConnectAsync(new Uri(_factory.Server.BaseAddress, "session"), CancellationToken.None);
    }

    private static async Task<TResponse> SendAndReceiveAsync<TResponse>(WebSocket socket, MatchmakingMessage message)
        where TResponse : MatchmakingMessage
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

        var buffer = new byte[4096];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        var response = JsonSerializer.Deserialize<MatchmakingMessage>(buffer.AsSpan(0, result.Count), JsonOptions);
        return Assert.IsType<TResponse>(response);
    }
}
