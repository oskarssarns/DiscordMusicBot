[RequireContext(ContextType.Guild)]
public sealed class MusicModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAudioService _audioService;
    private readonly MusicDbContext _guildDbContext;
    private readonly MusicMessageService _messageService;

    public MusicModule(IAudioService audioService, MusicDbContext guildDbContext, MusicMessageService messageService)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _guildDbContext = guildDbContext ?? throw new ArgumentNullException(nameof(guildDbContext));
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
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
        var playlistSongs = await _guildDbContext.louie_bot_playlists
            .Where(s => s.Playlist == playlist)
            .ToListAsync();

        playlistSongs = playlistSongs.OrderBy(_ => Guid.NewGuid()).ToList();

        await DeferAsync();
        var player = await GetPlayerAsync(connectToVoiceChannel: true);
        if (player is null || !playlistSongs.Any()) return;

        foreach (var track in playlistSongs)
        {
            await player.PlayAsync(track.Link!);
        }

        var components = MusicControlsBuilder.BuildControls(isPaused: false, isRepeating: false);
        await _messageService.SendOrUpdateAsync(Context, $"🔈 Playing ♂: {playlistSongs[0].Playlist} playlist", components);
    }

    [SlashCommand("disconnect", "Disconnects from voice channel", runMode: RunMode.Async)]
    public async Task Disconnect()
    {
        var player = await GetPlayerAsync();
        if (player is null) return;

        await player.DisconnectAsync();
        await _messageService.SendOrUpdateAsync(Context, "🔌 Disconnected.", new ComponentBuilder().Build());
        _messageService.Clear(Context.Guild.Id);
    }

    [SlashCommand("speed", "Changes playback speed (0.5 - 3.0)", runMode: RunMode.Async)]
    public async Task ChangeSpeed(double speed)
    {
        if (speed is < 0.5 or > 3.0)
        {
            await RespondAsync("Speed must be between 0.5 and 3.0");
            return;
        }

        var player = await GetPlayerAsync(false);
        if (player is null) return;

        player.Filters.SetFilter(new TimescaleFilterOptions { Speed = (float)speed });
        await player.Filters.CommitAsync();
        await RespondAsync($"Playback speed set to {speed:0.0}x");
    }

    [SlashCommand("play", "Plays music", runMode: RunMode.Async)]
    public async Task Play(string query)
    {
        await DeferAsync();

        var player = await GetPlayerAsync(connectToVoiceChannel: true);
        if (player is null) return;

        try
        {
            LavalinkTrack? track = query.Contains("&list=")
                ? (await _audioService.Tracks.LoadTracksAsync(query, TrackSearchMode.YouTube)).Tracks.First()
                : await _audioService.Tracks.LoadTrackAsync(query, TrackSearchMode.YouTube);

            if (track is null) return;

            var position = await player.PlayAsync(track);
            var message = position == 0 ? $"🔈 Playing: {track.Title}" : $"🔈 Added to queue: {track.Title}";

            var components = MusicControlsBuilder.BuildControls(isPaused: false, isRepeating: false);
            await _messageService.SendOrUpdateAsync(Context, message, components);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"Error loading track: {ex.Message}");
        }
    }

    [SlashCommand("radio", "Plays gachi radio", runMode: RunMode.Async)]
    public async Task Radio()
    {
        const string gachiRadio = "https://www.youtube.com/watch?v=akHAQD3o1NA";
        await DeferAsync();

        var player = await GetPlayerAsync(connectToVoiceChannel: true);
        if (player is null) return;

        var track = await _audioService.Tracks.LoadTrackAsync(gachiRadio, TrackSearchMode.YouTube);
        if (track is null) return;

        await player.PlayAsync(track);
        var components = MusicControlsBuilder.BuildControls(isPaused: false, isRepeating: false);
        await _messageService.SendOrUpdateAsync(Context, $"🔈 Playing: {track.Title}", components);
    }

    [SlashCommand("stop", "Stops the current track", runMode: RunMode.Async)]
    public async Task Stop()
    {
        var player = await GetPlayerAsync(false);
        if (player is null || player.CurrentItem is null) return;

        await player.StopAsync();
        await player.DisconnectAsync();

        await _messageService.SendOrUpdateAsync(Context, "⏹ Stopped playback", new ComponentBuilder().Build());
        _messageService.Clear(Context.Guild.Id);
    }

    [SlashCommand("volume", "Sets player volume (0 - 1000%)", runMode: RunMode.Async)]
    public async Task Volume(int volume = 100)
    {
        if (volume is < 0 or > 1000)
        {
            await RespondAsync("Volume out of range: 0% - 1000%!");
            return;
        }

        var player = await GetPlayerAsync(false);
        if (player is null) return;

        await player.SetVolumeAsync(volume / 100f);
        await RespondAsync($"Volume updated: {volume}%");
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
        var components = MusicControlsBuilder.BuildControls(isPaused: true, isRepeating: player.RepeatMode == TrackRepeatMode.Track);

        await (await GetOriginalResponseAsync()).ModifyAsync(msg => msg.Components = components);
    }

    [ComponentInteraction("resume_button")]
    public async Task HandleResumeButton()
    {
        await DeferAsync();

        var player = await GetPlayerAsync(false);
        if (player is null) return;

        await player.ResumeAsync();
        var components = MusicControlsBuilder.BuildControls(isPaused: false, isRepeating: player.RepeatMode == TrackRepeatMode.Track);

        await (await GetOriginalResponseAsync()).ModifyAsync(msg => msg.Components = components);
    }

    [ComponentInteraction("skip_button")]
    public async Task HandleSkipButton()
    {
        var player = await GetPlayerAsync(false);
        if (player is null) return;
        await player.SkipAsync();
    }

    [ComponentInteraction("stop_button")]
    public async Task HandleStopButton()
    {
        var player = await GetPlayerAsync(false);
        if (player is null) return;

        await player.StopAsync();
        await player.DisconnectAsync();

        var msg = await GetOriginalResponseAsync();

        if (msg != null)
        {
            await msg.ModifyAsync(m => m.Components = new ComponentBuilder().Build());
        }
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

        var components = MusicControlsBuilder.BuildControls(
            isPaused: player.State == PlayerState.Paused,
            isRepeating: player.RepeatMode == TrackRepeatMode.Track);

        await (await GetOriginalResponseAsync()).ModifyAsync(msg => msg.Components = components);
    }

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

            await FollowupAsync(errorMessage).ConfigureAwait(false);
            return null;
        }

        return result.Player;
    }
    #endregion
}