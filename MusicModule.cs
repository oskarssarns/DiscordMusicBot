[RequireContext(ContextType.Guild)]
public sealed class MusicModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAudioService _audioService;
    private readonly GachiDbContext _guildDbContext;

    public MusicModule(IAudioService audioService, GachiDbContext gachiDbContext)
    {
        ArgumentNullException.ThrowIfNull(audioService);
        ArgumentNullException.ThrowIfNull(gachiDbContext);
        _audioService = audioService;
        _guildDbContext = gachiDbContext;
    }

    #region Commands
    [SlashCommand("playlistadd", "Adds a playlist entry", runMode: RunMode.Async)]
    public async Task AddTrackToPlaylist(string playlist, string query)
    {
        try
        {
            LavalinkTrack? track = await _audioService.Tracks.LoadTrackAsync(query, TrackSearchMode.YouTube).ConfigureAwait(false);
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

                if (!await _guildDbContext.louie_bot_playlists.AnyAsync(s => s.Link == query).ConfigureAwait(false))
                {
                    await _guildDbContext.louie_bot_playlists.AddAsync(song);
                    await _guildDbContext.SaveChangesAsync().ConfigureAwait(false);
                    await RespondAsync($"Playlist entry added : {track.Title}").ConfigureAwait(false);
                }
                else
                {
                    await RespondAsync("Track is already in database!").ConfigureAwait(false);
                }
            }
            else
            {
                await RespondAsync("Failed to load track.").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await RespondAsync($"An unexpected error occurred: {ex.Message}").ConfigureAwait(false);
        }
    }

    [SlashCommand("pp", "Plays all songs from specific playlist", runMode: RunMode.Async)]
    public async Task PlayPlaylist(string playlist)
    {
        var playlistSongs = await _guildDbContext.louie_bot_playlists.Where(s => s.Playlist == playlist).ToListAsync();
        playlistSongs = playlistSongs.OrderBy(s => new Random().Next()).ToList();
        await DeferAsync().ConfigureAwait(false);
        var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);
        if (player is null) return;

        foreach (var track in playlistSongs)
        {
            await player.PlayAsync(track.Link).ConfigureAwait(false);
        }
        await FollowupAsync($"🔈 Playing ♂: {playlistSongs[0].Playlist} playlist").ConfigureAwait(false);
    }

    [SlashCommand("disconnect", "Disconnects from voice channel", runMode: RunMode.Async)]
    public async Task Disconnect()
    {
        var player = await GetPlayerAsync().ConfigureAwait(false);
        if (player is null) return;
        await player.DisconnectAsync().ConfigureAwait(false);
        await RespondAsync("Disconnected.").ConfigureAwait(false);
    }

    [SlashCommand("speed", "Changes playback speed (0.5 - 3.0)", runMode: RunMode.Async)]
    public async Task ChangeSpeed(double speed)
    {
        if (speed is < 0.5 or > 3.0) return;
        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
        if (player is null) return;
        player.Filters.SetFilter(new TimescaleFilterOptions { Speed = (float?)speed });
        await player.Filters.CommitAsync().ConfigureAwait(false);
    }

    [SlashCommand("play", "Plays music", runMode: RunMode.Async)]
    public async Task Play(string query)
    {
        await DeferAsync().ConfigureAwait(false);
        var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);
        if (player is null) return;

        try
        {
            LavalinkTrack? track = query.Contains("&list=")
                ? (await _audioService.Tracks.LoadTracksAsync(query, TrackSearchMode.YouTube).ConfigureAwait(false)).Tracks.First()
                : await _audioService.Tracks.LoadTrackAsync(query, TrackSearchMode.YouTube).ConfigureAwait(false);
            if (track is null) return;

            var position = await player.PlayAsync(track).ConfigureAwait(false);
            var message = position == 0 ? $"🔈 Playing: {track.Title}" : $"🔈 Added to queue: {track.Title}";

            var builder = new ComponentBuilder()
                .WithButton("Pause", "pause_button", ButtonStyle.Primary)
                .WithButton("Skip", "skip_button", ButtonStyle.Secondary)
                .WithButton("Repeat", "repeat_button", ButtonStyle.Primary)
                .WithButton("Stop", "stop_button", ButtonStyle.Danger);

            await FollowupAsync(message, components: builder.Build()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"Error loading track: {ex.Message}").ConfigureAwait(false);
        }
    }

    [SlashCommand("radio", "Plays gachi radio", runMode: RunMode.Async)]
    public async Task Radio()
    {
        const string gachiRadio = "https://www.youtube.com/watch?v=akHAQD3o1NA";
        await DeferAsync().ConfigureAwait(false);
        var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);
        if (player is null) return;

        var track = await _audioService.Tracks.LoadTrackAsync(gachiRadio, TrackSearchMode.YouTube).ConfigureAwait(false);
        await player.PlayAsync(track).ConfigureAwait(false);
        await FollowupAsync($"🔈 Playing: {track.Title}").ConfigureAwait(false);
    }

    [SlashCommand("stop", "Stops the current track", runMode: RunMode.Async)]
    public async Task Stop()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null || player.CurrentItem is null) return;
        await player.StopAsync().ConfigureAwait(false);
        await player.DisconnectAsync().ConfigureAwait(false);
    }

    [SlashCommand("volume", "Sets player volume (0 - 1000%)", runMode: RunMode.Async)]
    public async Task Volume(int volume = 100)
    {
        if (volume is < 0 or > 1000)
        {
            await RespondAsync("Volume out of range: 0% - 1000%!").ConfigureAwait(false);
            return;
        }

        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
        if (player is null) return;

        await player.SetVolumeAsync(volume / 100f).ConfigureAwait(false);
        await RespondAsync($"Volume updated: {volume}%").ConfigureAwait(false);
    }
    #endregion
    #region Buttons
    [ComponentInteraction("pause_button")]
    public async Task HandlePauseButton()
    {
        await DeferAsync().ConfigureAwait(false);
        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
        if (player is null) return;
        await player.PauseAsync().ConfigureAwait(false);

        var components = new ComponentBuilder()
            .WithButton("Resume", "resume_button", ButtonStyle.Primary)
            .WithButton("Skip", "skip_button", ButtonStyle.Secondary)
            .WithButton("Repeat", "repeat_button", ButtonStyle.Primary)
            .WithButton("Stop", "stop_button", ButtonStyle.Danger).Build();

        await (await GetOriginalResponseAsync()).ModifyAsync(msg => msg.Components = components).ConfigureAwait(false);
    }

    [ComponentInteraction("resume_button")]
    public async Task HandleResumeButton()
    {
        await DeferAsync().ConfigureAwait(false);
        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
        if (player is null) return;

        await player.ResumeAsync().ConfigureAwait(false);

        var components = new ComponentBuilder()
            .WithButton("Pause", "pause_button", ButtonStyle.Primary)
            .WithButton("Skip", "skip_button", ButtonStyle.Secondary)
            .WithButton("Repeat", "repeat_button", ButtonStyle.Primary)
            .WithButton("Stop", "stop_button", ButtonStyle.Danger).Build();

        await (await GetOriginalResponseAsync()).ModifyAsync(msg => msg.Components = components).ConfigureAwait(false);
    }

    [ComponentInteraction("skip_button")]
    public async Task HandleSkipButton()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
        if (player is null) return;
        await player.SkipAsync().ConfigureAwait(false);
    }

    [ComponentInteraction("stop_button")]
    public async Task HandleStopButton()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
        if (player is null) return;
        await player.StopAsync().ConfigureAwait(false);
        await player.DisconnectAsync().ConfigureAwait(false);
        var components = new ComponentBuilder().RemoveComponentsOfType;
    }

    [ComponentInteraction("repeat_button")]
    public async Task HandleRepeatButton()
    {
        await DeferAsync().ConfigureAwait(false);
        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
        if (player is null) return;
        await player.ResumeAsync().ConfigureAwait(false);
        player.RepeatMode = player.RepeatMode == TrackRepeatMode.Track ? TrackRepeatMode.None : TrackRepeatMode.Track;
        new ComponentBuilder()
            .WithButton("Pause", "pause_button", ButtonStyle.Primary)
            .WithButton("Skip", "skip_button", ButtonStyle.Secondary)
            .WithButton(player.RepeatMode == TrackRepeatMode.Track ? "Stop Repeating" : "Repeat", "repeat_button", ButtonStyle.Primary)
            .WithButton("Stop", "stop_button", ButtonStyle.Danger).Build();
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