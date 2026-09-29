using System.Net;

namespace CPonline.Launcher.Core.Networking;

/// <summary>
/// Minimal RFC 5389 STUN binding-request packet building and response parsing - just enough to
/// learn a UDP socket's public ip:port mapping. Pure byte manipulation, no sockets, so it's
/// fully unit-testable with synthetic request/response buffers.
/// </summary>
public static class StunProtocol
{
    public const uint MagicCookie = 0x2112A442u;

    private const ushort BindingRequestType = 0x0001;
    private const ushort BindingSuccessResponseType = 0x0101;
    private const ushort MappedAddressAttribute = 0x0001;
    private const ushort XorMappedAddressAttribute = 0x0020;

    public static byte[] BuildBindingRequest(byte[] transactionId)
    {
        if (transactionId.Length != 12)
        {
            throw new ArgumentException("STUN transaction ID must be 12 bytes.", nameof(transactionId));
        }

        var packet = new byte[20];
        WriteUInt16BigEndian(packet, 0, BindingRequestType);
        WriteUInt16BigEndian(packet, 2, 0); // message length: no attributes in the request
        WriteUInt32BigEndian(packet, 4, MagicCookie);
        Buffer.BlockCopy(transactionId, 0, packet, 8, 12);
        return packet;
    }

    /// <summary>Parses a Binding Success Response, preferring XOR-MAPPED-ADDRESS (RFC 5389) and
    /// falling back to the older plain MAPPED-ADDRESS some servers still send.</summary>
    public static bool TryParseMappedAddress(ReadOnlySpan<byte> response, byte[] transactionId, out IPEndPoint? endpoint)
    {
        endpoint = null;
        if (response.Length < 20)
        {
            return false;
        }

        if (ReadUInt16BigEndian(response, 0) != BindingSuccessResponseType)
        {
            return false;
        }

        var messageLength = ReadUInt16BigEndian(response, 2);
        if (ReadUInt32BigEndian(response, 4) != MagicCookie)
        {
            return false;
        }

        if (!response.Slice(8, 12).SequenceEqual(transactionId))
        {
            return false;
        }

        var attributesEnd = Math.Min(20 + messageLength, response.Length);
        var offset = 20;
        IPEndPoint? plainMapped = null;

        while (offset + 4 <= attributesEnd)
        {
            var attrType = ReadUInt16BigEndian(response, offset);
            var attrLength = ReadUInt16BigEndian(response, offset + 2);
            var valueStart = offset + 4;
            if (valueStart + attrLength > response.Length)
            {
                break;
            }

            var value = response.Slice(valueStart, attrLength);
            if (attrType == XorMappedAddressAttribute && TryParseXorMappedAddressValue(value, transactionId, out endpoint))
            {
                return true;
            }

            if (attrType == MappedAddressAttribute && TryParseMappedAddressValue(value, out plainMapped))
            {
                // keep scanning - prefer XOR-MAPPED-ADDRESS if a later attribute has it
            }

            var padded = (attrLength + 3) / 4 * 4;
            offset = valueStart + padded;
        }

        if (plainMapped is not null)
        {
            endpoint = plainMapped;
            return true;
        }

        return false;
    }

    private static bool TryParseXorMappedAddressValue(ReadOnlySpan<byte> value, byte[] transactionId, out IPEndPoint? endpoint)
    {
        endpoint = null;
        if (value.Length < 8)
        {
            return false;
        }

        var family = value[1];
        var xPort = ReadUInt16BigEndian(value, 2);
        var port = (ushort)(xPort ^ (MagicCookie >> 16));

        if (family == 0x01) // IPv4
        {
            var xAddress = ReadUInt32BigEndian(value, 4);
            var address = xAddress ^ MagicCookie;
            var addressBytes = new byte[4];
            WriteUInt32BigEndian(addressBytes, 0, address);
            endpoint = new IPEndPoint(new IPAddress(addressBytes), port);
            return true;
        }

        if (family == 0x02 && value.Length >= 20) // IPv6
        {
            var xorKey = new byte[16];
            WriteUInt32BigEndian(xorKey, 0, MagicCookie);
            Buffer.BlockCopy(transactionId, 0, xorKey, 4, 12);

            var addressBytes = new byte[16];
            for (var i = 0; i < 16; i++)
            {
                addressBytes[i] = (byte)(value[4 + i] ^ xorKey[i]);
            }

            endpoint = new IPEndPoint(new IPAddress(addressBytes), port);
            return true;
        }

        return false;
    }

    private static bool TryParseMappedAddressValue(ReadOnlySpan<byte> value, out IPEndPoint? endpoint)
    {
        endpoint = null;
        if (value.Length < 8 || value[1] != 0x01) // only IPv4 supported for the legacy attribute
        {
            return false;
        }

        var port = ReadUInt16BigEndian(value, 2);
        endpoint = new IPEndPoint(new IPAddress(value.Slice(4, 4).ToArray()), port);
        return true;
    }

    private static ushort ReadUInt16BigEndian(ReadOnlySpan<byte> buffer, int offset) =>
        (ushort)((buffer[offset] << 8) | buffer[offset + 1]);

    private static uint ReadUInt32BigEndian(ReadOnlySpan<byte> buffer, int offset) =>
        ((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16) | ((uint)buffer[offset + 2] << 8) | buffer[offset + 3];

    private static void WriteUInt16BigEndian(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)value;
    }

    private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
