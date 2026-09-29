using CPonline.Matchmaking.Rooms;
using CPonline.Matchmaking.Sockets;
using CPonline.Shared.Contracts;
using Xunit;

namespace CPonline.Matchmaking.Tests;

public class SessionSocketHandlerTests
{
    [Fact]
    public void Handle_CreateRoomRequest_ReturnsARoomCreatedResponse()
    {
        var handler = new SessionSocketHandler(new RoomStore());

        var response = handler.Handle(new CreateRoomRequest());

        var created = Assert.IsType<RoomCreatedResponse>(response);
        Assert.Equal(6, created.RoomCode.Length);
        Assert.NotEmpty(created.HostToken);
    }

    [Fact]
    public void Handle_ReportHostAddress_ThenResolve_ReturnsTheReportedAddress()
    {
        var handler = new SessionSocketHandler(new RoomStore());
        var created = (RoomCreatedResponse)handler.Handle(new CreateRoomRequest());

        var reportResponse = handler.Handle(new ReportHostAddressRequest
        {
            RoomCode = created.RoomCode,
            HostToken = created.HostToken,
            Ip = "203.0.113.9",
            Port = 11778,
            Source = HostAddressSource.Stun,
        });
        Assert.IsType<RoomResolvedResponse>(reportResponse);

        var resolved = Assert.IsType<RoomResolvedResponse>(handler.Handle(new ResolveRoomRequest { RoomCode = created.RoomCode }));

        Assert.Equal("203.0.113.9", resolved.Ip);
        Assert.Equal(11778, resolved.Port);
        Assert.Equal(HostAddressSource.Stun, resolved.Source);
    }

    [Fact]
    public void Handle_ResolveBeforeHostReportsAddress_ReturnsHostAddressNotReportedError()
    {
        var handler = new SessionSocketHandler(new RoomStore());
        var created = (RoomCreatedResponse)handler.Handle(new CreateRoomRequest());

        var response = handler.Handle(new ResolveRoomRequest { RoomCode = created.RoomCode });

        var error = Assert.IsType<MatchmakingErrorResponse>(response);
        Assert.Equal(MatchmakingErrorCodes.HostAddressNotReported, error.Code);
    }

    [Fact]
    public void Handle_ResolveUnknownRoom_ReturnsRoomNotFoundError()
    {
        var handler = new SessionSocketHandler(new RoomStore());

        var response = handler.Handle(new ResolveRoomRequest { RoomCode = "ZZZZZZ" });

        var error = Assert.IsType<MatchmakingErrorResponse>(response);
        Assert.Equal(MatchmakingErrorCodes.RoomNotFound, error.Code);
    }

    [Fact]
    public void Handle_ReportHostAddressWithWrongToken_ReturnsInvalidHostTokenError()
    {
        var handler = new SessionSocketHandler(new RoomStore());
        var created = (RoomCreatedResponse)handler.Handle(new CreateRoomRequest());

        var response = handler.Handle(new ReportHostAddressRequest
        {
            RoomCode = created.RoomCode,
            HostToken = "not-the-real-token",
            Ip = "203.0.113.9",
            Port = 11778,
            Source = HostAddressSource.Manual,
        });

        var error = Assert.IsType<MatchmakingErrorResponse>(response);
        Assert.Equal(MatchmakingErrorCodes.InvalidHostToken, error.Code);
    }
}
