namespace LavaLinkLouieBot;

internal sealed class DiscordClientHost : IHostedService
{
    private readonly DiscordSocketClient _discordSocketClient;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiscordClientHost> _logger;

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
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _discordSocketClient.InteractionCreated += InteractionCreated;
        _discordSocketClient.Ready += ClientReady;
        _logger.LogInformation("Starting Discord client...");
        await _discordSocketClient.LoginAsync(TokenType.Bot, _configuration["BotToken"]).ConfigureAwait(false);
        await _discordSocketClient.StartAsync().ConfigureAwait(false);
        _logger.LogInformation("Waiting for Discord client to be ready...");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _discordSocketClient.InteractionCreated -= InteractionCreated;
        _discordSocketClient.Ready -= ClientReady;
        await _discordSocketClient.StopAsync().ConfigureAwait(false);
    }

    private Task InteractionCreated(SocketInteraction interaction)
    {
        var interactionContext = new SocketInteractionContext(_discordSocketClient, interaction);
        return _interactionService.ExecuteCommandAsync(interactionContext, _serviceProvider);
    }

    private async Task ClientReady()
    {
        await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), _serviceProvider).ConfigureAwait(false);

        var servers = _configuration.GetSection("Servers").Get<string[]>();

        foreach (var serverId in servers!)
        {
            if (ulong.TryParse(serverId, out var guildId))
                await _interactionService.RegisterCommandsToGuildAsync(guildId).ConfigureAwait(false);
        }
    }
}