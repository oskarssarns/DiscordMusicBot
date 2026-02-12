namespace LavaLinkLouieBot;

internal sealed class DiscordClientHost : IHostedService
{
    private readonly DiscordSocketClient _discordSocketClient;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiscordClientHost> _logger;
    private readonly IAudioService _audioService;
    private readonly MusicMessageService _musicMessageService;

    public DiscordClientHost(
        DiscordSocketClient discordSocketClient,
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<DiscordClientHost> logger,
        IAudioService audioService,
        MusicMessageService musicMessageService)
    {
        ArgumentNullException.ThrowIfNull(discordSocketClient);
        ArgumentNullException.ThrowIfNull(interactionService);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(audioService);
        ArgumentNullException.ThrowIfNull(musicMessageService);

        _discordSocketClient = discordSocketClient;
        _interactionService = interactionService;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _audioService = audioService;
        _musicMessageService = musicMessageService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _discordSocketClient.InteractionCreated += InteractionCreated;
        _discordSocketClient.Ready += ClientReady;
        _audioService.TrackStarted += OnTrackStarted;
        _audioService.TrackEnded += OnTrackEnded;
        _logger.LogInformation("Starting Discord client...");
        await _discordSocketClient.LoginAsync(TokenType.Bot, _configuration["BotToken"]).ConfigureAwait(false);
        await _discordSocketClient.StartAsync().ConfigureAwait(false);
        _logger.LogInformation("Waiting for Discord client to be ready...");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _discordSocketClient.InteractionCreated -= InteractionCreated;
        _discordSocketClient.Ready -= ClientReady;
        _audioService.TrackStarted -= OnTrackStarted;
        _audioService.TrackEnded -= OnTrackEnded;
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

    private Task OnTrackStarted(object sender, Lavalink4NET.Events.Players.TrackStartedEventArgs eventArgs)
    {
        return UpdateGuildPlayerMessageAsync(eventArgs.Player);
    }

    private async Task OnTrackEnded(object sender, Lavalink4NET.Events.Players.TrackEndedEventArgs eventArgs)
    {
        if (!eventArgs.MayStartNext)
        {
            await UpdateGuildPlayerMessageAsync(eventArgs.Player).ConfigureAwait(false);
        }
    }

    private async Task UpdateGuildPlayerMessageAsync(ILavalinkPlayer player)
    {
        if (player is not VoteLavalinkPlayer votePlayer)
        {
            return;
        }

        try
        {
            bool showQueueRemoveButtons = _configuration.GetValue<ulong>("AdminUserId") != 0;
            int upcomingCount = Math.Min(4, votePlayer.Queue.Count);
            string content = MusicStatusBuilder.BuildStatusContent(
                votePlayer,
                showQueueRemoveHints: showQueueRemoveButtons);
            var components = MusicControlsBuilder.BuildControls(
                isPaused: votePlayer.State == PlayerState.Paused,
                isRepeating: votePlayer.RepeatMode == TrackRepeatMode.Track,
                upcomingCount: upcomingCount,
                showQueueRemoveButtons: showQueueRemoveButtons);

            await _musicMessageService.UpdateByGuildAsync(
                _discordSocketClient,
                votePlayer.GuildId,
                content,
                components).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to update guild player status message for guild {GuildId}.", player.GuildId);
        }
    }
}
