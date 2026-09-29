namespace CPonline.Launcher.Core.Services;

/// <summary>Parses the single "ip:port" (or "hostname:port") text field a non-technical user
/// pastes on the Play tab - the one thing they're expected to enter.</summary>
public static class ServerAddressParser
{
    public static bool TryParse(string? address, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var trimmed = address.Trim();
        var separatorIndex = trimmed.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == trimmed.Length - 1)
        {
            return false;
        }

        var hostPart = trimmed[..separatorIndex].Trim();
        var portPart = trimmed[(separatorIndex + 1)..].Trim();

        if (hostPart.Length == 0 || !int.TryParse(portPart, out var parsedPort) || parsedPort is < 1 or > 65535)
        {
            return false;
        }

        host = hostPart;
        port = parsedPort;
        return true;
    }
}
