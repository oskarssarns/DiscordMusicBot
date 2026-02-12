using LavaLinkLouieBot;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((context, services) =>
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

        services.AddSingleton<MusicMessageService>();

        services.AddLogging(x => x.AddConsole().SetMinimumLevel(LogLevel.Trace));
        services.AddDbContext<MusicDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Missing connection string. Configure 'ConnectionStrings:DefaultConnection'.")));
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
    }).UseConsoleLifetime();

var app = builder.Build();
await app.RunAsync();
