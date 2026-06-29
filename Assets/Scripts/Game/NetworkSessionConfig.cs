public enum NetworkRole
{
    Offline,
    Server,
    Client,
}

public static class NetworkSessionConfig
{
    public const int DefaultPort = 27015;
    public const string DefaultAddress = "127.0.0.1";

    public static NetworkRole Role { get; private set; } = NetworkRole.Offline;
    public static string Address { get; private set; } = DefaultAddress;
    public static int Port { get; private set; } = DefaultPort;

    public static void SetOffline()
    {
        Role = NetworkRole.Offline;
        Address = DefaultAddress;
        Port = DefaultPort;
    }

    public static void SetServer(int port)
    {
        Role = NetworkRole.Server;
        Address = DefaultAddress;
        Port = ClampPort(port);
    }

    public static void SetClient(string address, int port)
    {
        Role = NetworkRole.Client;
        Address = string.IsNullOrWhiteSpace(address) ? DefaultAddress : address.Trim();
        Port = ClampPort(port);
    }

    public static string Describe()
    {
        return Role switch
        {
            NetworkRole.Server => $"Server :{Port}",
            NetworkRole.Client => $"Client {Address}:{Port}",
            _ => "Offline",
        };
    }

    private static int ClampPort(int port)
    {
        return port is >= 1 and <= 65535 ? port : DefaultPort;
    }
}
