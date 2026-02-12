[RequireContext(ContextType.Guild)]
public sealed class MusicModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAudioService _audioService;
    private readonly PlaylistService _playlistService;
    private readonly PlaybackService _playbackService;
    private readonly MusicMessageService _messageService;
    private readonly MusicInteractionService _interactionService;
    private readonly ILogger<MusicModule> _logger;

    public MusicModule(
        IAudioService audioService,
        PlaylistService playlistService,
        PlaybackService playbackService,
        MusicMessageService messageService,
        MusicInteractionService interactionService,
        ILogger<MusicModule> logger)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _playlistService = playlistService ?? throw new ArgumentNullException(nameof(playlistService));
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _interactionService = interactionService ?? throw new ArgumentNullException(nameof(interactionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Commands
    [SlashCommand("playlistadd", "Adds a playlist entry", runMode: RunMode.Async)]
    public async Task AddTrackToPlaylist(string playlist, string query)
    {
        try
        {
            LavalinkTrack? track = await _audioService.Tracks.LoadTrackAsync(query, TrackSearchMode.YouTube);
            if (track != null)
            {
                bool isAdded = await _playlistService.AddTrackIfMissingAsync(
                    playlist,
                    query,
                    track.Title,
                    Context.User.Username);

                if (isAdded)
                {
                    await RespondAsync($"Playlist entry added : {track.Title}");
                }
                else
                {
                    await RespondAsync("Track is already in database!");
                }
            }
            else
            {
                await RespondAsync("Failed to load track.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add track to playlist '{Playlist}' with query '{Query}'.", playlist, query);
            await RespondAsync($"An unexpected error occurred: {ex.Message}");
        }
    }

    [SlashCommand("pp", "Plays all songs from specific playlist", runMode: RunMode.Async)]
    public async Task PlayPlaylist(string playlist)
    {
        await DeferAsync(ephemeral: true);

        var playlistSongs = await _playlistService.GetShuffledPlaylistSongsAsync(playlist);

        var player = await _interactionService.GetPlayerAsync(Context, connectToVoiceChannel: true);
        if (player is null) return;

        if (!playlistSongs.Any())
        {
            await _interactionService.SendInteractionMessageAsync(Context, $"Playlist `{playlist}` has no songs.");
            return;
        }

        foreach (var track in playlistSongs)
        {
            await player.PlayAsync(track.Link!);
        }

        await _interactionService.UpdatePlayerStatusMessageAsync(Context, player, $"🔈 Playlist started: {playlistSongs[0].Playlist}");
        await _interactionService.TryDeleteOriginalResponseAsync(Context);
    }

    [SlashCommand("disconnect", "Disconnects from voice channel", runMode: RunMode.Async)]
    public async Task Disconnect()
    {
        await DeferAsync(ephemeral: true);

        var player = await _interactionService.GetPlayerAsync(Context);
        if (player is null) return;

        await player.DisconnectAsync();
        await _messageService.SendOrUpdateAsync(Context, "🔌 Disconnected.", new ComponentBuilder().Build());
        _messageService.Clear(Context.Guild.Id);
        await _interactionService.TryDeleteOriginalResponseAsync(Context);
    }

    [SlashCommand("speed", "Changes playback speed (0.5 - 3.0)", runMode: RunMode.Async)]
    public async Task ChangeSpeed(double speed)
    {
        await DeferAsync(ephemeral: true);
        await _playbackService.ChangeSpeedAsync(Context, speed);
    }

    [SlashCommand("play", "Plays music", runMode: RunMode.Async)]
    public async Task Play(string query)
    {
        await DeferAsync(ephemeral: true);
        await _playbackService.PlayAsync(Context, query);
    }

    [SlashCommand("radio", "Plays MusicBot radio", runMode: RunMode.Async)]
    public async Task Radio()
    {
        await DeferAsync(ephemeral: true);
        await _playbackService.RadioAsync(Context);
    }

    [SlashCommand("stop", "Stops the current track", runMode: RunMode.Async)]
    public async Task Stop()
    {
        await DeferAsync(ephemeral: true);
        await _playbackService.StopAsync(Context);
    }

    [SlashCommand("volume", "Sets player volume (0 - 1000%)", runMode: RunMode.Async)]
    public async Task Volume(int volume = 100)
    {
        await DeferAsync(ephemeral: true);
        await _playbackService.VolumeAsync(Context, volume);
    }
    #endregion
    #region Buttons
    [ComponentInteraction("pause_button")]
    public async Task HandlePauseButton()
    {
        await DeferAsync();

        var player = await _interactionService.GetPlayerAsync(Context, false);
        if (player is null) return;

        await player.PauseAsync();
        await _interactionService.UpdatePlayerStatusMessageAsync(Context, player);
    }

    [ComponentInteraction("resume_button")]
    public async Task HandleResumeButton()
    {
        await DeferAsync();

        var player = await _interactionService.GetPlayerAsync(Context, false);
        if (player is null) return;

        await player.ResumeAsync();
        await _interactionService.UpdatePlayerStatusMessageAsync(Context, player);
    }

    [ComponentInteraction("skip_button")]
    public async Task HandleSkipButton()
    {
        await DeferAsync();

        var player = await _interactionService.GetPlayerAsync(Context, false);
        if (player is null) return;
        await player.SkipAsync();
        await _interactionService.UpdatePlayerStatusMessageAsync(Context, player);
    }

    [ComponentInteraction("next_button")]
    public async Task HandleNextButton()
    {
        await DeferAsync();

        var player = await _interactionService.GetPlayerAsync(Context, false);
        if (player is null) return;
        await player.SkipAsync();
        await _interactionService.UpdatePlayerStatusMessageAsync(Context, player);
    }

    [ComponentInteraction("stop_button")]
    public async Task HandleStopButton()
    {
        await DeferAsync();

        var player = await _interactionService.GetPlayerAsync(Context, false);
        if (player is null) return;

        await player.StopAsync();
        await player.DisconnectAsync();
        await _messageService.SendOrUpdateAsync(Context, "⏹ Stopped playback", new ComponentBuilder().Build());
        _messageService.Clear(Context.Guild.Id);
    }

    [ComponentInteraction("repeat_button")]
    public async Task HandleRepeatButton()
    {
        await DeferAsync();

        var player = await _interactionService.GetPlayerAsync(Context, false);
        if (player is null) return;

        player.RepeatMode = player.RepeatMode == TrackRepeatMode.Track
            ? TrackRepeatMode.None
            : TrackRepeatMode.Track;

        await _interactionService.UpdatePlayerStatusMessageAsync(Context, player);
    }

    [ComponentInteraction("remove_queue_1")]
    public Task HandleRemoveQueue1Button() => HandleRemoveQueueButtonAsync(0);

    [ComponentInteraction("remove_queue_2")]
    public Task HandleRemoveQueue2Button() => HandleRemoveQueueButtonAsync(1);

    [ComponentInteraction("remove_queue_3")]
    public Task HandleRemoveQueue3Button() => HandleRemoveQueueButtonAsync(2);

    [ComponentInteraction("remove_queue_4")]
    public Task HandleRemoveQueue4Button() => HandleRemoveQueueButtonAsync(3);

    #endregion
    #region Helpers
    private async Task HandleRemoveQueueButtonAsync(int queueIndex)
    {
        await DeferAsync(ephemeral: true);

        if (!_interactionService.IsAdminUser(Context.User.Id))
        {
            await _interactionService.SendInteractionMessageAsync(Context, "Only the configured admin can remove tracks from the queue.");
            return;
        }

        var player = await _interactionService.GetPlayerAsync(Context, false);
        if (player is null) return;

        if (queueIndex < 0 || queueIndex >= player.Queue.Count)
        {
            await _interactionService.SendInteractionMessageAsync(Context, "That queue slot is empty.");
            return;
        }

        string removedTitle = player.Queue[queueIndex].Track?.Title ?? player.Queue[queueIndex].Identifier;
        await player.Queue.RemoveAtAsync(queueIndex, CancellationToken.None);
        await _interactionService.UpdatePlayerStatusMessageAsync(Context, player);
        await _interactionService.SendInteractionMessageAsync(Context, $"Removed from queue: {removedTitle}");
    }
    #endregion
}
