namespace LavaLinkLouieBot.Helpers;

public static class LavaLinkHelper
{
    public static async Task<List<LavalinkServer>> GetLavalinkServers(string source)
    {
        using HttpClient client = new HttpClient();

        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36");

        HttpResponseMessage response = await client.GetAsync(source);
        response.EnsureSuccessStatusCode();

        string responseBody = await response.Content.ReadAsStringAsync();
        JToken jsonResponse = JToken.Parse(responseBody);
        JArray serversJson =
            jsonResponse as JArray ??
            jsonResponse["servers"] as JArray ??
            jsonResponse["data"] as JArray ??
            new JArray();

        if (serversJson.Count == 0)
        {
            Console.WriteLine($"Lavalink source returned no server entries. Source: {source}");
        }

        var servers = new List<LavalinkServer>();

        foreach (var serverJson in serversJson.OfType<JObject>())
        {
            string host = serverJson["host"]?.ToString() ?? string.Empty;
            string port = serverJson["port"]?.ToString() ?? string.Empty;
            string password = serverJson["password"]?.ToString() ?? string.Empty;
            string secure = (serverJson["secure"]?.Value<bool>() ?? false).ToString().ToLowerInvariant();
            string version = serverJson["version"]?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(port) ||
                string.IsNullOrWhiteSpace(password))
            {
                continue;
            }

            if (!IsVersion4OrHigher(version))
            {
                continue;
            }

            servers.Add(new LavalinkServer
            {
                Host = host,
                Port = port,
                Password = password,
                Secure = secure,
                Version = version
            });

            Console.WriteLine($"Added Lavalink server: {host}:{port} (secure={secure}, version={version})");
        }

        Console.WriteLine($"Total Lavalink servers added: {servers.Count}");
        return servers;
    }

    public static async Task<bool> IsServerOnline(LavalinkServer server)
    {
        try
        {
            using TcpClient tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(server.Host!, int.Parse(server.Port!));
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(2));
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask != connectTask)
            {
                return false;
            }

            return tcpClient.Connected;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> CanServerPlayMusic(LavalinkServer server)
    {
        try
        {
            string scheme = server.Secure!.ToLower() == "true" ? "https" : "http";
            string infoUrl = $"{scheme}://{server.Host}:{server.Port}/v4/info";

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(3)
            };
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", server.Password);

            using var response = await httpClient.GetAsync(infoUrl).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<List<LavalinkServer>> GetOnlineLavalinkServers(List<LavalinkServer> servers)
    {
        List<LavalinkServer> onlineServers = new List<LavalinkServer>();

        foreach (var server in servers)
        {
            bool isOnline = await IsServerOnline(server);
            if (!isOnline)
            {
                Console.WriteLine($"Offline server: {server.Host}:{server.Port}");
                continue;
            }

            bool canAuthenticate = await CanServerPlayMusic(server);
            if (!canAuthenticate)
            {
                Console.WriteLine($"Unusable server (auth or API check failed): {server.Host}:{server.Port}");
                continue;
            }

            if (isOnline && canAuthenticate)
            {
                onlineServers.Add(server);
            }
        }

        return onlineServers;
    }

    public static string RemoveQuotes(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input.Trim('"');
    }

    private static bool IsVersion4OrHigher(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        string normalized = version.Trim().TrimStart('v', 'V');
        string majorPart = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries)[0];

        return int.TryParse(majorPart, out int majorVersion) && majorVersion >= 4;
    }

    public static async Task<LavalinkServerConfig> GetLavalinkServerConfiguration(IConfiguration configuration)
    {
        var servers = await LavaLinkHelper.GetLavalinkServers(configuration["LavaLinkSource"]!);
        var onlineServers = await LavaLinkHelper.GetOnlineLavalinkServers(servers);
        if (onlineServers.Count > 0)
        {
            int maxPingMs = configuration.GetValue("MaxLavalinkPingMs", 200);
            Console.WriteLine($"Found {onlineServers.Count} online servers.");
            LavalinkServer? server = await GetBestServerByPingThreshold(onlineServers, maxPingMs);
            if (server is null)
            {
                throw new InvalidOperationException("No pingable online servers found.");
            }

            string scheme = server.Secure!.ToLower() == "true" ? "https" : "http";
            return new LavalinkServerConfig
            {
                BaseAddress = $"{scheme}://{server.Host}:{server.Port}",
                Passphrase = server.Password
            };
        }
        else
            throw new InvalidOperationException("No online servers found.");
    }

    private static async Task<LavalinkServer?> GetBestServerByPingThreshold(List<LavalinkServer> servers, int maxPingMs)
    {
        LavalinkServer? fallbackServer = null;
        long fallbackPing = long.MaxValue;

        foreach (var server in servers)
        {
            long? ping = await MeasureTcpPingAsync(server);
            if (ping is null)
            {
                continue;
            }

            Console.WriteLine($"Server ping: {server.Host}:{server.Port} => {ping}ms");

            if (ping.Value <= maxPingMs)
            {
                Console.WriteLine($"Selected server within ping threshold ({maxPingMs}ms): {server.Host}:{server.Port} ({ping}ms)");
                return server;
            }

            if (ping.Value < fallbackPing)
            {
                fallbackPing = ping.Value;
                fallbackServer = server;
            }
        }

        if (fallbackServer is not null)
        {
            Console.WriteLine($"No server met ping threshold ({maxPingMs}ms). Falling back to lowest ping: {fallbackServer.Host}:{fallbackServer.Port} ({fallbackPing}ms)");
        }

        return fallbackServer;
    }

    private static async Task<long?> MeasureTcpPingAsync(LavalinkServer server)
    {
        try
        {
            using var tcpClient = new TcpClient();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var connectTask = tcpClient.ConnectAsync(server.Host!, int.Parse(server.Port!));
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3));
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask != connectTask || !tcpClient.Connected)
            {
                return null;
            }

            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }
        catch
        {
            return null;
        }
    }
}
