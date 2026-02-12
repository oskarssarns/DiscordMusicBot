namespace LavaLinkLouieBot.Helpers;

public sealed class MusicInteractionService
{
    private readonly IAudioService _audioService;
    private readonly MusicMessageService _messageService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MusicInteractionService> _logger;

    public MusicInteractionService(
        IAudioService audioService,
        MusicMessageService messageService,
        IConfiguration configuration,
        ILogger<MusicInteractionService> logger)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<VoteLavalinkPlayer?> GetPlayerAsync(
        SocketInteractionContext context,
        bool connectToVoiceChannel = true)
    {
        var retrieveOptions = new PlayerRetrieveOptions(
            ChannelBehavior: connectToVoiceChannel ? PlayerChannelBehavior.Join : PlayerChannelBehavior.None);

        var result = await _audioService.Players
            .RetrieveAsync(context, playerFactory: PlayerFactory.Vote, retrieveOptions)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            var errorMessage = result.Status switch
            {
                PlayerRetrieveStatus.UserNotInVoiceChannel => "You are not connected to a voice channel.",
                PlayerRetrieveStatus.BotNotConnected => "The bot is currently not connected.",
                _ => "Unknown error.",
            };

            await SendInteractionMessageAsync(context, errorMessage).ConfigureAwait(false);
            return null;
        }

        return result.Player;
    }

    public Task SendInteractionMessageAsync(SocketInteractionContext context, string message)
    {
        if (context.Interaction.HasResponded)
        {
            return context.Interaction.FollowupAsync(message, ephemeral: true);
        }

        return context.Interaction.RespondAsync(message, ephemeral: true);
    }

    public async Task TryDeleteOriginalResponseAsync(SocketInteractionContext context)
    {
        if (!context.Interaction.HasResponded)
        {
            return;
        }

        try
        {
            await context.Interaction.DeleteOriginalResponseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Failed to delete original interaction response for guild {GuildId} user {UserId}.",
                context.Guild.Id,
                context.User.Id);
        }
    }

    public async Task UpdatePlayerStatusMessageAsync(
        SocketInteractionContext context,
        VoteLavalinkPlayer player,
        string? header = null)
    {
        bool showQueueRemoveButtons = IsAdminConfigured();
        int upcomingCount = Math.Min(4, player.Queue.Count);
        string content = MusicStatusBuilder.BuildStatusContent(
            player,
            header,
            showQueueRemoveHints: showQueueRemoveButtons);

        var components = MusicControlsBuilder.BuildControls(
            isPaused: player.State == PlayerState.Paused,
            isRepeating: player.RepeatMode == TrackRepeatMode.Track,
            upcomingCount: upcomingCount,
            showQueueRemoveButtons: showQueueRemoveButtons);

        await _messageService.SendOrUpdateAsync(context, content, components);
    }

    public bool IsAdminUser(ulong userId)
    {
        ulong adminUserId = _configuration.GetValue<ulong>("AdminUserId");
        return adminUserId != 0 && userId == adminUserId;
    }

    public bool IsAdminConfigured() => _configuration.GetValue<ulong>("AdminUserId") != 0;
}
