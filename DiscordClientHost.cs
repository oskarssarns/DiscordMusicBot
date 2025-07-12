namespace LavaLinkLouieBot;

internal sealed class DiscordClientHost : IHostedService, IDisposable
{
    private readonly DiscordSocketClient _discordSocketClient;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiscordClientHost> _logger;
    private readonly IAudioService _audioService;
    private Timer _timer;
    private TaskCompletionSource<bool> _readyTcs;

    public DiscordClientHost(
        DiscordSocketClient discordSocketClient,
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<DiscordClientHost> logger,
        IAudioService audioService)
    {
        ArgumentNullException.ThrowIfNull(discordSocketClient);
        ArgumentNullException.ThrowIfNull(interactionService);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(audioService);

        _discordSocketClient = discordSocketClient;
        _interactionService = interactionService;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _audioService = audioService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _readyTcs = new TaskCompletionSource<bool>();
        _discordSocketClient.InteractionCreated += InteractionCreated;
        _discordSocketClient.Ready += ClientReady;

        _logger.LogInformation("Starting Discord client...");
        await _discordSocketClient
            .LoginAsync(TokenType.Bot, _configuration["BotToken"])
            .ConfigureAwait(false);

        await _discordSocketClient
            .StartAsync()
            .ConfigureAwait(false);

        _logger.LogInformation("Waiting for Discord client to be ready...");
        try
        {
            await _readyTcs.Task.WaitAsync(TimeSpan.FromSeconds(180), cancellationToken);
            _logger.LogInformation("Discord client is ready.");
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Timed out while waiting for Discord client being ready.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while waiting for Discord client being ready.");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _discordSocketClient.InteractionCreated -= InteractionCreated;
        _discordSocketClient.Ready -= ClientReady;

        await _discordSocketClient
            .StopAsync()
            .ConfigureAwait(false);

        _timer?.Change(Timeout.Infinite, 0);
        _timer?.Dispose();
    }

    private Task InteractionCreated(SocketInteraction interaction)
    {
        var interactionContext = new SocketInteractionContext(_discordSocketClient, interaction);
        return _interactionService.ExecuteCommandAsync(interactionContext, _serviceProvider);
    }

    private async Task ClientReady()
    {
        _readyTcs.SetResult(true);

        await _interactionService
            .AddModulesAsync(Assembly.GetExecutingAssembly(), _serviceProvider)
            .ConfigureAwait(false);

        await _interactionService
            .RegisterCommandsToGuildAsync(ulong.Parse(_configuration["Server2"]))
            .ConfigureAwait(false);
        await _interactionService
            .RegisterCommandsToGuildAsync(ulong.Parse(_configuration["Server1"]))
            .ConfigureAwait(false);
        await _interactionService
            .RegisterCommandsToGuildAsync(ulong.Parse(_configuration["Server3"]))
            .ConfigureAwait(false);
        await _interactionService
            .RegisterCommandsToGuildAsync(ulong.Parse(_configuration["Server4"]))
            .ConfigureAwait(false);
    }

    private async Task<LavaLinkLouieBot.Helpers.LavalinkServerConfig> GetLavalinkServerConfiguration(IConfiguration configuration)
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

    public void Dispose()
    {
        _timer?.Dispose();
    }
}