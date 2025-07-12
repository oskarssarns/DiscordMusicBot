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
        JObject jsonResponse = JObject.Parse(responseBody);

        List<LavalinkServer> servers = new List<LavalinkServer>();
        bool skipFirst = true;

        foreach (var doc in jsonResponse["docs"])
        {
            string text = doc["text"].ToString();
            if (text.Contains("Host :"))
            {
                if (skipFirst)
                {
                    skipFirst = false;
                    continue;
                }

                LavalinkServer server = new LavalinkServer();

                int hostStart = text.IndexOf("Host :") + 7;
                int hostEnd = text.IndexOf('\n', hostStart);
                server.Host = text.Substring(hostStart, hostEnd - hostStart).Trim();

                int portStart = text.IndexOf("Port :") + 7;
                int portEnd = text.IndexOf('\n', portStart);
                server.Port = text.Substring(portStart, portEnd - portStart).Trim();

                int passwordStart = text.IndexOf("Password :") + 11;
                int passwordEnd = text.IndexOf('\n', passwordStart);
                server.Password = text.Substring(passwordStart, passwordEnd - passwordStart).Trim();
                server.Password = RemoveQuotes(server.Password);

                int secureStart = text.IndexOf("Secure :") + 9;
                int secureEnd = text.IndexOf('\n', secureStart);
                server.Secure = text.Substring(secureStart, secureEnd - secureStart).Trim();

                Match versionMatch = Regex.Match(text, @"Version\s*(\d+\.\d+\.\d+)");
                if (versionMatch.Success)
                {
                    server.Version = versionMatch.Groups[1].Value;
                    if (Version.Parse(server.Version) >= Version.Parse("4.0.0"))
                    {
                        servers.Add(server);
                    }
                }
            }
        }

        return servers;
    }

    public static async Task<bool> IsServerOnline(LavalinkServer server)
    {
        try
        {
            using TcpClient tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(server.Host, int.Parse(server.Port));
            return tcpClient.Connected;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> CanServerPlayMusic(LavalinkServer server, string testQuery, IConfiguration configuration)
    {
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<DiscordSocketClient>();
            services.AddSingleton<IDiscordClientWrapper, DiscordClientWrapper>();
            services.AddLavalink();

            string scheme = server.Secure.ToLower() == "true" ? "https" : "http";
            string baseAddress = $"{scheme}://{server.Host}:{server.Port}";

            services.ConfigureLavalink(options =>
            {
                options.BaseAddress = new Uri(baseAddress);
                options.Passphrase = server.Password;
            });

            var serviceProvider = services.BuildServiceProvider();
            var audioService = serviceProvider.GetRequiredService<IAudioService>();

            var track = await audioService.Tracks.LoadTrackAsync(testQuery, TrackSearchMode.YouTube).ConfigureAwait(false);
            return track != null;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<List<LavalinkServer>> GetOnlineLavalinkServers(List<LavalinkServer> servers, string testQuery, IConfiguration configuration)
    {
        List<LavalinkServer> onlineServers = new List<LavalinkServer>();

        foreach (var server in servers)
        {
            if (await IsServerOnline(server) && await CanServerPlayMusic(server, testQuery, configuration))
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

    public static async Task<LavalinkServerConfig> GetLavalinkServerConfiguration(IConfiguration configuration)
    {
        List<LavaLinkLouieBot.Helpers.LavalinkServer> servers = await LavaLinkHelper.GetLavalinkServers(configuration["LavaLinkSource"]);
        List<LavaLinkLouieBot.Helpers.LavalinkServer> onlineServers = await LavaLinkHelper.GetOnlineLavalinkServers(servers, configuration["TestQuery"], configuration);
        if (onlineServers.Count > 0)
        {
            var server = onlineServers[0];
            string scheme = server.Secure.ToLower() == "true" ? "https" : "http";
            return new LavaLinkLouieBot.Helpers.LavalinkServerConfig
            {
                BaseAddress = $"{scheme}://{server.Host}:{server.Port}",
                Passphrase = server.Password
            };
        }
        else
        {
            throw new InvalidOperationException("No online servers found.");
        }
    }
}

public class LavalinkServer
{
    public string Host { get; set; }
    public string Port { get; set; }
    public string Password { get; set; }
    public string Secure { get; set; }
    public string Version { get; set; }
}

public class LavalinkServerConfig
{
    public string BaseAddress { get; set; }
    public string Passphrase { get; set; }
}