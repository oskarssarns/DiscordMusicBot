using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET;
using LavaLinkLouieBot.Data;
using LavaLinkLouieBot.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Lavalink4NET.Extensions;
using Discord.WebSocket;
using Lavalink4NET.Clients;
using Lavalink4NET.DiscordNet;

namespace LavaLinkLouieBot.Helpers
{
    public static class LavaLinkHelper
    {
        public async static Task<List<LavalinkServer>> GetLavalinkServers(string source)
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
                        if (Version.Parse(server.Version) >= new Version(4, 0, 0))
                        {
                            servers.Add(server);
                        }
                    }
                }
            }

            return servers;
        }

        public async static Task<bool> IsServerOnline(LavalinkServer server)
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

        public async static Task<bool> CanServerPlayMusic(LavalinkServer server, string testQuery, IConfiguration configuration)
        {
            var audioService = CreateAudioServiceForServer(server, configuration);

            try
            {
                var track = await audioService.Tracks.LoadTrackAsync(testQuery, TrackSearchMode.YouTube).ConfigureAwait(false);
                return track != null;
            }
            catch
            {
                return false;
            }
        }

        public async static Task<List<LavalinkServer>> GetOnlineLavalinkServers(List<LavalinkServer> servers, string testQuery, IConfiguration configuration)
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

        public static async Task SaveServersAsync(IEnumerable<LavalinkServer> servers, GachiDbContext dbContext)
        {
            foreach (var server in servers)
            {
                server.Password = RemoveQuotes(server.Password);

                var existingServer = await dbContext.lavalink_servers
                    .FirstOrDefaultAsync(s => s.Host == server.Host && s.Port == server.Port);

                if (existingServer != null)
                {
                    existingServer.Password = server.Password;
                    existingServer.Secure = server.Secure;
                    existingServer.Version = server.Version;

                    dbContext.lavalink_servers.Update(existingServer);
                }
                else
                {
                    dbContext.lavalink_servers.Add(server);
                }
            }

            await dbContext.SaveChangesAsync();
        }

        public static string RemoveQuotes(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return input.Trim('"');
        }

        public static IAudioService CreateAudioServiceForServer(LavalinkServer server, IConfiguration configuration)
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

            var discordClient = serviceProvider.GetRequiredService<DiscordSocketClient>();
            discordClient.LoginAsync(Discord.TokenType.Bot, configuration["Discord:Token"]).Wait();
            discordClient.StartAsync().Wait();

            return serviceProvider.GetRequiredService<IAudioService>();
        }
    }
}
