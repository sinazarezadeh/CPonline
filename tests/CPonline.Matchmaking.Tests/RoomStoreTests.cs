using CPonline.Matchmaking.Rooms;
using CPonline.Shared.Contracts;
using Xunit;

namespace CPonline.Matchmaking.Tests;

public class RoomStoreTests
{
    [Fact]
    public void CreateRoom_ReturnsARoomWithAFreshCodeAndToken()
    {
        var store = new RoomStore();

        var room = store.CreateRoom(TimeSpan.FromMinutes(30));

        Assert.Equal(RoomCodeGenerator.Length, room.RoomCode.Length);
        Assert.NotEmpty(room.HostToken);
    }

    [Fact]
    public void Get_ReturnsTheRoomJustCreated()
    {
        var store = new RoomStore();
        var room = store.CreateRoom(TimeSpan.FromMinutes(30));

        var fetched = store.Get(room.RoomCode);

        Assert.NotNull(fetched);
        Assert.Equal(room.RoomCode, fetched!.RoomCode);
    }

    [Fact]
    public void Get_UnknownCode_ReturnsNull()
    {
        var store = new RoomStore();

        Assert.Null(store.Get("ZZZZZZ"));
    }

    [Fact]
    public void Get_ExpiredRoom_ReturnsNullAndPrunesIt()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var store = new RoomStore(() => now);
        var room = store.CreateRoom(TimeSpan.FromMinutes(1));

        now = now.AddMinutes(2); // advance past expiry

        Assert.Null(store.Get(room.RoomCode));
    }

    [Fact]
    public void TryReportHostAddress_CorrectToken_UpdatesTheRoomAndReturnsTrue()
    {
        var store = new RoomStore();
        var room = store.CreateRoom(TimeSpan.FromMinutes(30));

        var ok = store.TryReportHostAddress(room.RoomCode, room.HostToken, "203.0.113.5", 11778, HostAddressSource.Upnp);

        Assert.True(ok);
        var fetched = store.Get(room.RoomCode)!;
        Assert.Equal("203.0.113.5", fetched.HostIp);
        Assert.Equal(11778, fetched.HostPort);
        Assert.Equal(HostAddressSource.Upnp, fetched.HostAddressSource);
        Assert.True(fetched.HasHostAddress);
    }

    [Fact]
    public void TryReportHostAddress_WrongToken_ReturnsFalseAndDoesNotUpdate()
    {
        var store = new RoomStore();
        var room = store.CreateRoom(TimeSpan.FromMinutes(30));

        var ok = store.TryReportHostAddress(room.RoomCode, "wrong-token", "203.0.113.5", 11778, HostAddressSource.Stun);

        Assert.False(ok);
        Assert.False(store.Get(room.RoomCode)!.HasHostAddress);
    }

    [Fact]
    public void TryReportHostAddress_UnknownRoom_ReturnsFalse()
    {
        var store = new RoomStore();

        Assert.False(store.TryReportHostAddress("ZZZZZZ", "any-token", "203.0.113.5", 11778, HostAddressSource.Manual));
    }

    [Fact]
    public void RemoveExpired_PrunesOnlyRoomsPastTheGivenTime()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var store = new RoomStore(() => now);
        var shortLived = store.CreateRoom(TimeSpan.FromMinutes(1));
        var longLived = store.CreateRoom(TimeSpan.FromMinutes(60));

        store.RemoveExpired(now.AddMinutes(5));

        Assert.Null(store.Get(shortLived.RoomCode));
        Assert.NotNull(store.Get(longLived.RoomCode));
    }

    [Fact]
    public void CreateRoom_NeverCollidesWithAnExistingCode()
    {
        var store = new RoomStore();
        var codes = new HashSet<string>();

        for (var i = 0; i < 200; i++)
        {
            codes.Add(store.CreateRoom(TimeSpan.FromMinutes(30)).RoomCode);
        }

        Assert.Equal(200, codes.Count);
    }
}
