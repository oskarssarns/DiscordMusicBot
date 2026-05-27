using LavaLinkLouieBot;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args);
    })
    .ConfigureServices((context, services) =>
    {
        IConfiguration configuration = context.Configuration;

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<DiscordSocketClient>(provider =>
        {
            var client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates
            });
            client.Log += (msg) =>
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

                return Task.CompletedTask;
            };
            return client;
        });
        services.AddSingleton<InteractionService>(provider =>
            new InteractionService(provider.GetRequiredService<DiscordSocketClient>()));
        services.AddSingleton<IDiscordClientWrapper>(provider =>
            new DiscordClientWrapper(provider.GetRequiredService<DiscordSocketClient>()));

        services.AddSingleton<MusicMessageService>();
        services.AddSingleton<MusicInteractionService>();
        services.AddSingleton<PlaybackService>();

        services.AddLogging(x => x.AddConsole().SetMinimumLevel(LogLevel.Information));

        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<MusicDbContext>(options =>
                options.UseSqlServer(connectionString));
            services.AddScoped<PlaylistService>();
        }

        services.AddLavalink();
        services.ConfigureLavalink(options =>
        {
            var server = LavaLinkHelper.GetLavalinkServerConfiguration(configuration)
                                       .GetAwaiter()
                                       .GetResult();

            Console.WriteLine($"Selected Lavalink server: {server.BaseAddress}");
            options.BaseAddress = new Uri(server.BaseAddress!);
            options.Passphrase = server.Passphrase;
        });
        services.AddHostedService<DiscordClientHost>();
        services.AddHostedService<PlaybackSelfTestService>();
    }).UseConsoleLifetime();

var app = builder.Build();
await InitializeDatabaseAsync(app.Services);
await app.RunAsync();

static async Task InitializeDatabaseAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Database");
    var dbContext = scope.ServiceProvider.GetService<MusicDbContext>();
    if (dbContext is null)
    {
        logger.LogWarning("No database connection string configured. Playlist commands are disabled.");
        return;
    }

    await dbContext.Database.EnsureCreatedAsync();
}
