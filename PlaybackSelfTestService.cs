namespace LavaLinkLouieBot;

internal sealed class PlaybackSelfTestService : BackgroundService
{
    private readonly DiscordSocketClient _discordClient;
    private readonly IAudioService _audioService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlaybackSelfTestService> _logger;

    public PlaybackSelfTestService(
        DiscordSocketClient discordClient,
        IAudioService audioService,
        IConfiguration configuration,
        ILogger<PlaybackSelfTestService> logger)
    {
        _discordClient = discordClient ?? throw new ArgumentNullException(nameof(discordClient));
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string? query = _configuration["PlaybackSelfTestQuery"];
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        try
        {
            await WaitForDiscordReadyAsync(stoppingToken).ConfigureAwait(false);
            await _audioService.WaitForReadyAsync(stoppingToken).ConfigureAwait(false);

            var voiceChannel = FindVoiceChannel();
            if (voiceChannel is null)
            {
                _logger.LogWarning("Playback self-test skipped because no voice channel was found.");
                return;
            }

            _logger.LogInformation(
                "Starting playback self-test in voice channel {VoiceChannelName} ({VoiceChannelId}).",
                voiceChannel.Name,
                voiceChannel.Id);

            LavalinkTrack? track = await _audioService.Tracks
                .LoadTrackAsync(query, TrackSearchMode.YouTube)
                .ConfigureAwait(false);

            if (track is null)
            {
                _logger.LogError("Playback self-test could not load query '{Query}'.", query);
                return;
            }

            await DisconnectExistingVoiceStateAsync(voiceChannel.Guild.Id, stoppingToken).ConfigureAwait(false);

            var player = await _audioService.Players
                .JoinAsync(voiceChannel.Guild.Id, voiceChannel.Id, PlayerFactory.Vote, stoppingToken)
                .ConfigureAwait(false);

            await player.PlayAsync(track).ConfigureAwait(false);

            _logger.LogInformation(
                "Playback self-test requested track playback: {TrackTitle}.",
                track.Title);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playback self-test failed.");
        }
    }

    private async Task WaitForDiscordReadyAsync(CancellationToken cancellationToken)
    {
        if (_discordClient.ConnectionState == ConnectionState.Connected && _discordClient.Guilds.Count > 0)
        {
            return;
        }

        var readyCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task Ready()
        {
            readyCompletion.TrySetResult();
            return Task.CompletedTask;
        }

        _discordClient.Ready += Ready;
        try
        {
            if (_discordClient.ConnectionState == ConnectionState.Connected && _discordClient.Guilds.Count > 0)
            {
                return;
            }

            await readyCompletion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _discordClient.Ready -= Ready;
        }
    }

    private SocketVoiceChannel? FindVoiceChannel()
    {
        string? configuredChannelId = _configuration["PlaybackSelfTestVoiceChannelId"];
        if (ulong.TryParse(configuredChannelId, out ulong channelId))
        {
            return _discordClient.Guilds
                .SelectMany(guild => guild.VoiceChannels)
                .FirstOrDefault(channel => channel.Id == channelId);
        }

        return _discordClient.Guilds
            .SelectMany(guild => guild.VoiceChannels)
            .OrderByDescending(channel => channel.Users.Count(user => !user.IsBot))
            .ThenBy(channel => channel.Position)
            .FirstOrDefault();
    }

    private async Task DisconnectExistingVoiceStateAsync(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Clearing existing bot voice state before playback self-test.");

        await _audioService.Players.DiscordClient
            .SendVoiceUpdateAsync(guildId, null, selfDeaf: false, selfMute: false, cancellationToken)
            .ConfigureAwait(false);

        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
    }
}
