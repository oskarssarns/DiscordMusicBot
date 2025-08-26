using LavaLinkLouieBot;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices(async (context, services) =>
    {
        IConfiguration configuration = context.Configuration;

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<DiscordSocketClient>(provider =>
        {
            var client = new DiscordSocketClient();
            client.Log += async (msg) =>
            {
                provider.GetRequiredService<ILogger<DiscordSocketClient>>().Log(
                    msg.Severity switch
                    {
                        LogSeverity.Critical => LogLevel.Critical,
                        LogSeverity.Error => LogLevel.Error,
                        LogSeverity.Warning => LogLevel.Warning,
                        LogSeverity.Info => LogLevel.Information,
                        LogSeverity.Verbose => LogLevel.Debug,
                        LogSeverity.Debug => LogLevel.Trace,
                        _ => LogLevel.Information
                    },
                    msg.Exception,
                    msg.Message);
            };
            return client;
        });
        services.AddSingleton<InteractionService>(provider =>
            new InteractionService(provider.GetRequiredService<DiscordSocketClient>()));
        services.AddSingleton<IDiscordClientWrapper>(provider =>
            new DiscordClientWrapper(provider.GetRequiredService<DiscordSocketClient>()));
        services.AddLogging(x => x.AddConsole().SetMinimumLevel(LogLevel.Trace));
        services.AddDbContext<GachiDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("GachiBase")));
        services.AddLavalink();
        services.ConfigureLavalink(options =>
        {
            var server = Task.Run(() => GetLavalinkServerConfiguration(configuration)).GetAwaiter().GetResult();
            options.BaseAddress = new Uri(server.BaseAddress!);
            options.Passphrase = server.Passphrase;
        });
        services.AddHostedService<DiscordClientHost>();
    }).UseConsoleLifetime();

var app = builder.Build();
await app.RunAsync();

async Task<LavaLinkLouieBot.Helpers.LavalinkServerConfig> GetLavalinkServerConfiguration(IConfiguration configuration)
{
    var servers = await LavaLinkHelper.GetLavalinkServers(configuration["LavaLinkSource"]!);
    var onlineServers = await LavaLinkHelper.GetOnlineLavalinkServers(servers, configuration["TestQuery"]!, configuration);
    if (onlineServers.Count > 0)
    {
        var server = onlineServers[0];
        string scheme = server.Secure!.ToLower() == "true" ? "https" : "http";
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