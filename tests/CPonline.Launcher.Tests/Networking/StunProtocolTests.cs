using System.Net;
using CPonline.Launcher.Core.Networking;
using Xunit;

namespace CPonline.Launcher.Tests.Networking;

public class StunProtocolTests
{
    [Fact]
    public void BuildBindingRequest_HasCorrectHeaderMagicCookieAndTransactionId()
    {
        var transactionId = Enumerable.Range(0, 12).Select(i => (byte)i).ToArray();

        var packet = StunProtocol.BuildBindingRequest(transactionId);

        Assert.Equal(20, packet.Length);
        Assert.Equal(new byte[] { 0x00, 0x01 }, packet[0..2]); // Binding Request
        Assert.Equal(new byte[] { 0x00, 0x00 }, packet[2..4]); // message length: no attributes
        Assert.Equal(new byte[] { 0x21, 0x12, 0xA4, 0x42 }, packet[4..8]); // magic cookie
        Assert.Equal(transactionId, packet[8..20]);
    }

    [Fact]
    public void BuildBindingRequest_WrongTransactionIdLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => StunProtocol.BuildBindingRequest(new byte[10]));
    }

    [Fact]
    public void TryParseMappedAddress_XorMappedAddress_ParsesTheCorrectIPv4EndPoint()
    {
        var transactionId = RandomTransactionId();
        var response = BuildXorMappedAddressResponse(transactionId, IPAddress.Parse("203.0.113.7"), 54321);

        var ok = StunProtocol.TryParseMappedAddress(response, transactionId, out var endpoint);

        Assert.True(ok);
        Assert.Equal(IPAddress.Parse("203.0.113.7"), endpoint!.Address);
        Assert.Equal(54321, endpoint.Port);
    }

    [Fact]
    public void TryParseMappedAddress_MismatchedTransactionId_ReturnsFalse()
    {
        var transactionId = RandomTransactionId();
        var response = BuildXorMappedAddressResponse(transactionId, IPAddress.Parse("203.0.113.7"), 54321);

        var ok = StunProtocol.TryParseMappedAddress(response, RandomTransactionId(), out var endpoint);

        Assert.False(ok);
        Assert.Null(endpoint);
    }

    [Fact]
    public void TryParseMappedAddress_NotASuccessResponse_ReturnsFalse()
    {
        var transactionId = RandomTransactionId();
        var request = StunProtocol.BuildBindingRequest(transactionId);

        var ok = StunProtocol.TryParseMappedAddress(request, transactionId, out var endpoint);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseMappedAddress_TruncatedBuffer_ReturnsFalse()
    {
        var ok = StunProtocol.TryParseMappedAddress(new byte[10], new byte[12], out var endpoint);

        Assert.False(ok);
        Assert.Null(endpoint);
    }

    [Fact]
    public void TryParseMappedAddress_WrongMagicCookie_ReturnsFalse()
    {
        var transactionId = RandomTransactionId();
        var response = BuildXorMappedAddressResponse(transactionId, IPAddress.Parse("203.0.113.7"), 54321);
        response[4] = 0x00; // corrupt the magic cookie

        var ok = StunProtocol.TryParseMappedAddress(response, transactionId, out var endpoint);

        Assert.False(ok);
    }

    private static byte[] RandomTransactionId()
    {
        var id = new byte[12];
        Random.Shared.NextBytes(id);
        return id;
    }

    /// <summary>Hand-builds a Binding Success Response containing a single XOR-MAPPED-ADDRESS
    /// attribute, independently of <see cref="StunProtocol"/>'s own encoding, so the parser is
    /// verified against RFC 5389's wire format rather than against itself.</summary>
    private static byte[] BuildXorMappedAddressResponse(byte[] transactionId, IPAddress address, int port)
    {
        const uint magicCookie = 0x2112A442u;
        var addressBytes = address.GetAddressBytes();
        var addressValue = ((uint)addressBytes[0] << 24) | ((uint)addressBytes[1] << 16) | ((uint)addressBytes[2] << 8) | addressBytes[3];

        var xPort = (ushort)(port ^ (magicCookie >> 16));
        var xAddress = addressValue ^ magicCookie;

        var attributeValue = new byte[]
        {
            0x00, 0x01, // reserved, family = IPv4
            (byte)(xPort >> 8), (byte)xPort,
            (byte)(xAddress >> 24), (byte)(xAddress >> 16), (byte)(xAddress >> 8), (byte)xAddress,
        };

        var packet = new byte[20 + 4 + attributeValue.Length];
        packet[0] = 0x01;
        packet[1] = 0x01; // Binding Success Response
        var attributesLength = (ushort)(4 + attributeValue.Length);
        packet[2] = (byte)(attributesLength >> 8);
        packet[3] = (byte)attributesLength;
        packet[4] = 0x21;
        packet[5] = 0x12;
        packet[6] = 0xA4;
        packet[7] = 0x42;
        Buffer.BlockCopy(transactionId, 0, packet, 8, 12);

        packet[20] = 0x00;
        packet[21] = 0x20; // XOR-MAPPED-ADDRESS attribute type
        packet[22] = 0x00;
        packet[23] = (byte)attributeValue.Length;
        Buffer.BlockCopy(attributeValue, 0, packet, 24, attributeValue.Length);

        return packet;
    }
}
