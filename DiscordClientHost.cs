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
    private readonly SemaphoreSlim _readyLock = new(1, 1);
    private bool _modulesAdded;
    private bool _commandsRegistered;

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

    private async Task InteractionCreated(SocketInteraction interaction)
    {
        var interactionContext = new SocketInteractionContext(_discordSocketClient, interaction);

        try
        {
            var result = await _interactionService
                .ExecuteCommandAsync(interactionContext, _serviceProvider)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                return;
            }

            _logger.LogWarning(
                "Interaction {InteractionName} failed: {Error} {ErrorReason}",
                GetInteractionName(interaction),
                result.Error,
                result.ErrorReason);

            await TrySendInteractionErrorAsync(
                interaction,
                "That command could not be completed. Please try again.")
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception while executing interaction {InteractionName}.",
                GetInteractionName(interaction));

            await TrySendInteractionErrorAsync(
                interaction,
                "Something went wrong while handling that command. Please try again.")
                .ConfigureAwait(false);
        }
    }

    private async Task ClientReady()
    {
        await _readyLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_modulesAdded)
            {
                await _interactionService
                    .AddModulesAsync(Assembly.GetExecutingAssembly(), _serviceProvider)
                    .ConfigureAwait(false);
                _modulesAdded = true;
            }

            if (!_commandsRegistered)
            {
                await _interactionService.RegisterCommandsGloballyAsync().ConfigureAwait(false);
                _commandsRegistered = true;
                _logger.LogInformation("Discord interaction commands registered.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Discord interaction commands.");
        }
        finally
        {
            _readyLock.Release();
        }
    }

    private async Task TrySendInteractionErrorAsync(SocketInteraction interaction, string message)
    {
        try
        {
            if (interaction.HasResponded)
            {
                if (interaction.Type == InteractionType.ApplicationCommand)
                {
                    await interaction.ModifyOriginalResponseAsync(properties =>
                    {
                        properties.Content = message;
                        properties.Components = new ComponentBuilder().Build();
                    }).ConfigureAwait(false);
                    return;
                }

                await interaction.FollowupAsync(message, ephemeral: true).ConfigureAwait(false);
                return;
            }

            await interaction.RespondAsync(message, ephemeral: true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send interaction error response for interaction {InteractionId}.", interaction.Id);
        }
    }

    private static string GetInteractionName(SocketInteraction interaction)
    {
        return interaction switch
        {
            SocketSlashCommand command => command.CommandName,
            SocketMessageComponent component => component.Data.CustomId,
            _ => interaction.Id.ToString()
        };
    }

    private Task OnTrackStarted(object sender, Lavalink4NET.Events.Players.TrackStartedEventArgs eventArgs)
    {
        _logger.LogInformation(
            "Track started in guild {GuildId}: {TrackTitle}",
            eventArgs.Player.GuildId,
            eventArgs.Player.CurrentTrack?.Title ?? "unknown track");

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
            int upcomingCount = Math.Min(4, votePlayer.Queue.Count);
            string content = MusicStatusBuilder.BuildStatusContent(
                votePlayer,
                showQueueRemoveHints: upcomingCount > 0);
            var components = MusicControlsBuilder.BuildControls(
                isPaused: votePlayer.State == PlayerState.Paused,
                isRepeating: votePlayer.RepeatMode == TrackRepeatMode.Track,
                upcomingCount: upcomingCount);

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
