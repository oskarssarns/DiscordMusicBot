[RequireContext(ContextType.Guild)]
public sealed class MusicModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAudioService _audioService;
    private readonly MusicDbContext _guildDbContext;
    private readonly MusicMessageService _messageService;
    private readonly IConfiguration _configuration;

    public MusicModule(
        IAudioService audioService,
        MusicDbContext guildDbContext,
        MusicMessageService messageService,
        IConfiguration configuration)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _guildDbContext = guildDbContext ?? throw new ArgumentNullException(nameof(guildDbContext));
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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
                var song = new Song
                {
                    Name = track.Title,
                    Link = query,
                    Playlist = playlist,
                    UserAdded = Context.User.Username,
                    Created = DateTime.UtcNow
                };

                if (!await _guildDbContext.louie_bot_playlists.AnyAsync(s => s.Link == query))
                {
                    await _guildDbContext.louie_bot_playlists.AddAsync(song);
                    await _guildDbContext.SaveChangesAsync();
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
            await RespondAsync($"An unexpected error occurred: {ex.Message}");
        }
    }

    [SlashCommand("pp", "Plays all songs from specific playlist", runMode: RunMode.Async)]
    public async Task PlayPlaylist(string playlist)
    {
        await DeferAsync(ephemeral: true);

        var playlistSongs = await _guildDbContext.louie_bot_playlists
            .Where(s => s.Playlist == playlist)
            .ToListAsync();

        playlistSongs = playlistSongs.OrderBy(_ => Guid.NewGuid()).ToList();

        var player = await GetPlayerAsync(connectToVoiceChannel: true);
        if (player is null) return;

        if (!playlistSongs.Any())
        {
            await SendInteractionMessageAsync($"Playlist `{playlist}` has no songs.");
            return;
        }

        foreach (var track in playlistSongs)
        {
            await player.PlayAsync(track.Link!);
        }

        await UpdatePlayerStatusMessageAsync(player, $"🔈 Playlist started: {playlistSongs[0].Playlist}");
        await TryDeleteOriginalResponseAsync();
    }

    [SlashCommand("disconnect", "Disconnects from voice channel", runMode: RunMode.Async)]
    public async Task Disconnect()
    {
        await DeferAsync(ephemeral: true);

        var player = await GetPlayerAsync();
        if (player is null) return;

        await player.DisconnectAsync();
        await _messageService.SendOrUpdateAsync(Context, "🔌 Disconnected.", new ComponentBuilder().Build());
        _messageService.Clear(Context.Guild.Id);
        await TryDeleteOriginalResponseAsync();
    }

    [SlashCommand("speed", "Changes playback speed (0.5 - 3.0)", runMode: RunMode.Async)]
    public async Task ChangeSpeed(double speed)
    {
        await DeferAsync(ephemeral: true);

        if (speed is < 0.5 or > 3.0)
        {
            await SendInteractionMessageAsync("Speed must be between 0.5 and 3.0");
            return;
        }

        var player = await GetPlayerAsync(false);
        if (player is null) return;

        player.Filters.SetFilter(new TimescaleFilterOptions { Speed = (float)speed });
        await player.Filters.CommitAsync();
        await UpdatePlayerStatusMessageAsync(player);
        await TryDeleteOriginalResponseAsync();
    }

    [SlashCommand("play", "Plays music", runMode: RunMode.Async)]
    public async Task Play(string query)
    {
        await DeferAsync(ephemeral: true);

        var player = await GetPlayerAsync(connectToVoiceChannel: true);
        if (player is null) return;

        try
        {
            LavalinkTrack? track = query.Contains("&list=")
                ? (await _audioService.Tracks.LoadTracksAsync(query, TrackSearchMode.YouTube)).Tracks.First()
                : await _audioService.Tracks.LoadTrackAsync(query, TrackSearchMode.YouTube);

            if (track is null) return;

            await player.PlayAsync(track);
            await UpdatePlayerStatusMessageAsync(player);
            await TryDeleteOriginalResponseAsync();
        }
        catch (Exception ex)
        {
            await SendInteractionMessageAsync($"Error loading track: {ex.Message}");
        }
    }

    [SlashCommand("radio", "Plays MusicBot radio", runMode: RunMode.Async)]
    public async Task Radio()
    {
        const string musicBotRadio = "https://www.youtube.com/watch?v=akHAQD3o1NA";
        await DeferAsync(ephemeral: true);

        var player = await GetPlayerAsync(connectToVoiceChannel: true);
        if (player is null) return;

        var track = await _audioService.Tracks.LoadTrackAsync(musicBotRadio, TrackSearchMode.YouTube);
        if (track is null)
        {
            await SendInteractionMessageAsync("Failed to load radio track.");
            return;
        }

        await player.PlayAsync(track);
        await UpdatePlayerStatusMessageAsync(player);
        await TryDeleteOriginalResponseAsync();
    }

    [SlashCommand("stop", "Stops the current track", runMode: RunMode.Async)]
    public async Task Stop()
    {
        await DeferAsync(ephemeral: true);

        var player = await GetPlayerAsync(false);
        if (player is null) return;
        if (player.CurrentItem is null)
        {
            await SendInteractionMessageAsync("Nothing is currently playing.");
            return;
        }

        await player.StopAsync();
        await player.DisconnectAsync();

        await _messageService.SendOrUpdateAsync(Context, "⏹ Stopped playback", new ComponentBuilder().Build());
        _messageService.Clear(Context.Guild.Id);
        await TryDeleteOriginalResponseAsync();
    }

    [SlashCommand("volume", "Sets player volume (0 - 1000%)", runMode: RunMode.Async)]
    public async Task Volume(int volume = 100)
    {
        await DeferAsync(ephemeral: true);

        if (volume is < 0 or > 1000)
        {
            await SendInteractionMessageAsync("Volume out of range: 0% - 1000%!");
            return;
        }

        var player = await GetPlayerAsync(false);
        if (player is null) return;

        await player.SetVolumeAsync(volume / 100f);
        await UpdatePlayerStatusMessageAsync(player);
        await TryDeleteOriginalResponseAsync();
    }
    #endregion
    #region Buttons
    [ComponentInteraction("pause_button")]
    public async Task HandlePauseButton()
    {
        await DeferAsync();

        var player = await GetPlayerAsync(false);
        if (player is null) return;

        await player.PauseAsync();
        await UpdatePlayerStatusMessageAsync(player);
    }

    [ComponentInteraction("resume_button")]
    public async Task HandleResumeButton()
    {
        await DeferAsync();

        var player = await GetPlayerAsync(false);
        if (player is null) return;

        await player.ResumeAsync();
        await UpdatePlayerStatusMessageAsync(player);
    }

    [ComponentInteraction("skip_button")]
    public async Task HandleSkipButton()
    {
        await DeferAsync();

        var player = await GetPlayerAsync(false);
        if (player is null) return;
        await player.SkipAsync();
        await UpdatePlayerStatusMessageAsync(player);
    }

    [ComponentInteraction("next_button")]
    public async Task HandleNextButton()
    {
        await DeferAsync();

        var player = await GetPlayerAsync(false);
        if (player is null) return;
        await player.SkipAsync();
        await UpdatePlayerStatusMessageAsync(player);
    }

    [ComponentInteraction("stop_button")]
    public async Task HandleStopButton()
    {
        await DeferAsync();

        var player = await GetPlayerAsync(false);
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

        var player = await GetPlayerAsync(false);
        if (player is null) return;

        player.RepeatMode = player.RepeatMode == TrackRepeatMode.Track
            ? TrackRepeatMode.None
            : TrackRepeatMode.Track;

        await UpdatePlayerStatusMessageAsync(player);
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
    private async ValueTask<VoteLavalinkPlayer?> GetPlayerAsync(bool connectToVoiceChannel = true)
    {
        var retrieveOptions = new PlayerRetrieveOptions(
            ChannelBehavior: connectToVoiceChannel ? PlayerChannelBehavior.Join : PlayerChannelBehavior.None);

        var result = await _audioService.Players
            .RetrieveAsync(Context, playerFactory: PlayerFactory.Vote, retrieveOptions)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            var errorMessage = result.Status switch
            {
                PlayerRetrieveStatus.UserNotInVoiceChannel => "You are not connected to a voice channel.",
                PlayerRetrieveStatus.BotNotConnected => "The bot is currently not connected.",
                _ => "Unknown error.",
            };

            await SendInteractionMessageAsync(errorMessage).ConfigureAwait(false);
            return null;
        }

        return result.Player;
    }

    private Task SendInteractionMessageAsync(string message)
    {
        if (Context.Interaction.HasResponded)
        {
            return FollowupAsync(message, ephemeral: true);
        }

        return RespondAsync(message, ephemeral: true);
    }

    private async Task TryDeleteOriginalResponseAsync()
    {
        if (!Context.Interaction.HasResponded)
        {
            return;
        }

        try
        {
            await DeleteOriginalResponseAsync();
        }
        catch
        {
            // Ignore deletion errors; they should not break command flow.
        }
    }

    private async Task UpdatePlayerStatusMessageAsync(VoteLavalinkPlayer player, string? header = null)
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

        await _messageService.SendOrUpdateAsync(Context, content, components);
    }

    private async Task HandleRemoveQueueButtonAsync(int queueIndex)
    {
        await DeferAsync(ephemeral: true);

        if (!IsAdminUser(Context.User.Id))
        {
            await SendInteractionMessageAsync("Only the configured admin can remove tracks from the queue.");
            return;
        }

        var player = await GetPlayerAsync(false);
        if (player is null) return;

        if (queueIndex < 0 || queueIndex >= player.Queue.Count)
        {
            await SendInteractionMessageAsync("That queue slot is empty.");
            return;
        }

        string removedTitle = player.Queue[queueIndex].Track?.Title ?? player.Queue[queueIndex].Identifier;
        await player.Queue.RemoveAtAsync(queueIndex, CancellationToken.None);
        await UpdatePlayerStatusMessageAsync(player);
        await SendInteractionMessageAsync($"Removed from queue: {removedTitle}");
    }

    private bool IsAdminConfigured() => GetAdminUserId() != 0;

    private bool IsAdminUser(ulong userId)
    {
        ulong adminUserId = GetAdminUserId();
        return adminUserId != 0 && userId == adminUserId;
    }

    private ulong GetAdminUserId()
    {
        return _configuration.GetValue<ulong>("AdminUserId");
    }
    #endregion
}
