using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LavaLinkLouieBot.Data;
using LavaLinkLouieBot.Models;
using Discord.Interactions;
using Discord.WebSocket;
using Lavalink4NET.Extensions;
using LavaLinkLouieBot.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((context, services) =>
    {
        IConfiguration configuration = context.Configuration;

        // Configure services
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<DiscordSocketClient>();
        services.AddSingleton<InteractionService>();
        services.AddHostedService<DiscordClientHost>();
        services.AddLogging(x => x.AddConsole().SetMinimumLevel(LogLevel.Trace));
        services.AddDbContext<GachiDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("GachiBase")));

        // Add Lavalink services and configure them
        services.AddLavalink();
        services.ConfigureLavalink(options =>
        {
            var server = GetLavalinkServerConfiguration(configuration).GetAwaiter().GetResult();
            options.BaseAddress = new Uri(server.BaseAddress);
            options.Passphrase = server.Passphrase;
        });
    })
    .UseConsoleLifetime();

var app = builder.Build();

await app.RunAsync();

// Helper method to get Lavalink server configuration
async Task<LavalinkServerConfig> GetLavalinkServerConfiguration(IConfiguration configuration)
{
    List<LavalinkServer> servers = await LavaLinkHelper.GetLavalinkServers(configuration["LavaLinkSource"]);
    List<LavalinkServer> onlineServers = await LavaLinkHelper.GetOnlineLavalinkServers(servers, configuration["TestQuery"], configuration);
    if (onlineServers.Count > 0)
    {
        var server = onlineServers[0];
        string scheme = server.Secure.ToLower() == "true" ? "https" : "http";
        return new LavalinkServerConfig
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

public class LavalinkServerConfig
{
    public string BaseAddress { get; set; }
    public string Passphrase { get; set; }
}
