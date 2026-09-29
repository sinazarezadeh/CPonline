using System.Collections.Concurrent;
using CPonline.Shared.Contracts;

namespace CPonline.Matchmaking.Rooms;

public interface IRoomStore
{
    RoomRecord CreateRoom(TimeSpan ttl);

    /// <summary>Null if the room doesn't exist or has expired (expired rooms are pruned lazily on lookup).</summary>
    RoomRecord? Get(string roomCode);

    /// <summary>False if the room doesn't exist, has expired, or the token doesn't match its host.</summary>
    bool TryReportHostAddress(string roomCode, string hostToken, string ip, int port, HostAddressSource source);

    void RemoveExpired(DateTimeOffset now);
}

/// <summary>In-memory room registry. This relay only ever brokers small JSON control messages
/// (room create/resolve/report-address) - it's never in the path of actual game traffic - so an
/// in-memory store restarting with the process is an acceptable tradeoff for a friend-group-scale
/// signaling service.</summary>
public sealed class RoomStore : IRoomStore
{
    private readonly ConcurrentDictionary<string, RoomRecord> _rooms = new();
    private readonly Func<DateTimeOffset> _clock;

    public RoomStore() : this(() => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>Test seam: inject a fake clock to make expiry deterministic.</summary>
    internal RoomStore(Func<DateTimeOffset> clock) => _clock = clock;

    public RoomRecord CreateRoom(TimeSpan ttl)
    {
        RoomRecord record;
        do
        {
            var code = RoomCodeGenerator.Generate();
            record = new RoomRecord
            {
                RoomCode = code,
                HostToken = Guid.NewGuid().ToString("N"),
                ExpiresAtUtc = _clock() + ttl,
            };
        }
        while (!_rooms.TryAdd(record.RoomCode, record));

        return record;
    }

    public RoomRecord? Get(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var record))
        {
            return null;
        }

        if (record.ExpiresAtUtc < _clock())
        {
            _rooms.TryRemove(roomCode, out _);
            return null;
        }

        return record;
    }

    public bool TryReportHostAddress(string roomCode, string hostToken, string ip, int port, HostAddressSource source)
    {
        var record = Get(roomCode);
        if (record is null || !string.Equals(record.HostToken, hostToken, StringComparison.Ordinal))
        {
            return false;
        }

        record.HostIp = ip;
        record.HostPort = port;
        record.HostAddressSource = source;
        return true;
    }

    public void RemoveExpired(DateTimeOffset now)
    {
        foreach (var (code, record) in _rooms)
        {
            if (record.ExpiresAtUtc < now)
            {
                _rooms.TryRemove(code, out _);
            }
        }
    }
}
