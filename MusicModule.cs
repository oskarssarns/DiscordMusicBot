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
    [SlashCommand("playlistadd", "Adds a playlist entry ", runMode: RunMode.Async)]
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

                bool trackExists = await _guildDbContext.louie_bot_playlists.AnyAsync(s => s.Link == query).ConfigureAwait(false);

                if (!trackExists)
                {
                    await _guildDbContext.louie_bot_playlists.AddAsync(song);
                    await _guildDbContext.SaveChangesAsync().ConfigureAwait(false);
                    await RespondAsync($"Playlist entry added : {track.Title}").ConfigureAwait(false);
                }
                else
                {
                    await RespondAsync("Track is already in database, you have great taste!").ConfigureAwait(false);
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
    public async Task PlayPlayList(string playlist)
    {
        List<Song> playlistSongs = await _guildDbContext.louie_bot_playlists.Where(s => s.Playlist == playlist).ToListAsync();
        playlistSongs = playlistSongs.OrderBy(s => new Random().Next()).ToList();
        await DeferAsync().ConfigureAwait(false);
        var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);
        if (player is null) return;

        foreach (var track in playlistSongs)
        {
            var position = await player.PlayAsync(track.Link).ConfigureAwait(false);
        }
        await FollowupAsync($"🔈 Playing ♂: {playlistSongs[0].Playlist} playlist").ConfigureAwait(false);
    }

    [SlashCommand("disconnect", "Disconnects from the current voice channel connected to", runMode: RunMode.Async)]
    public async Task Disconnect()
    {
        VoteLavalinkPlayer? player = await GetPlayerAsync().ConfigureAwait(false);
        if (player is null) return;
        await player.DisconnectAsync().ConfigureAwait(false);
        await RespondAsync("Disconnected.").ConfigureAwait(false);
    }

    [SlashCommand("speed", description: "Changes the playback speed (0.5 - 3.0)", runMode: RunMode.Async)]
    public async Task ChangeSpeed(double speed)
    {
        if (speed < 0.5 || speed > 3.0) return;
        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
        if (player is null) return;
        var timescaleFilterOptions = new TimescaleFilterOptions{Speed = (float?)speed};
        player.Filters.SetFilter(timescaleFilterOptions);
        await player.Filters.CommitAsync().ConfigureAwait(false);
    }

    [SlashCommand("play", description: "Plays music", runMode: RunMode.Async)]
    public async Task Play(string query)
    {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);

        if (player is null) return;

        try
        {
            LavalinkTrack? track;
            if (query.Contains("&list="))
            {
                TrackLoadResult playlist = await _audioService.Tracks.LoadTracksAsync(query, TrackSearchMode.YouTube).ConfigureAwait(false);
                if (!playlist.Tracks.Any())
                {
                    await FollowupAsync("No results found in the playlist.").ConfigureAwait(false);
                    return;
                }

                track = playlist.Tracks.First();
                foreach (var t in playlist.Tracks.Skip(1))
                {
                    await player.Queue.AddAsync(new TrackQueueItem(new TrackReference(t)), CancellationToken.None).ConfigureAwait(false);
                }
            }
            else
            {
                track = await _audioService.Tracks.LoadTrackAsync(query, TrackSearchMode.YouTube).ConfigureAwait(false);
                if (track == null)
                {
                    await FollowupAsync("No results.").ConfigureAwait(false);
                    return;
                }
            }

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
            Console.WriteLine($"Exception occurred: {ex.Message}");
            await FollowupAsync($"Error loading track: {ex.Message}").ConfigureAwait(false);
        }
    }

    [SlashCommand("radio", description: "Plays gachi radio", runMode: RunMode.Async)]
    public async Task Radio()
    {
        string gachiRadio = "https://www.youtube.com/watch?v=akHAQD3o1NA";

        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);

        if (player is null)
        {
            await FollowupAsync("No player found!").ConfigureAwait(false);
            return;
        }

        LavalinkTrack? track = await _audioService.Tracks.LoadTrackAsync(gachiRadio, TrackSearchMode.YouTube).ConfigureAwait(false);
        int? position = await player.PlayAsync(track).ConfigureAwait(false);
        if (position == 0)
        {
            await FollowupAsync($"🔈 Playing: {track.Title}").ConfigureAwait(false);
        }
    }

    [SlashCommand("stop", description: "Stops the current track", runMode: RunMode.Async)]
    public async Task Stop()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player.CurrentItem is null || player is null) return;
        await player.StopAsync().ConfigureAwait(false);
        await player.DisconnectAsync().ConfigureAwait(false);
    }

    [SlashCommand("volume", description: "Sets the player volume (0 - 1000%)", runMode: RunMode.Async)]
    public async Task Volume(int volume = 100)
    {
        if (volume is > 1000 or < 0)
        {
            await RespondAsync("Volume out of range: 0% - 1000%!").ConfigureAwait(false);
            return;
        }

        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

        if (player is null)
        {
            return;
        }

        await player.SetVolumeAsync(volume / 100f).ConfigureAwait(false);
        await RespondAsync($"Volume updated: {volume}%").ConfigureAwait(false);
    }

    [SlashCommand("skip", description: "Skips the current track", runMode: RunMode.Async)]
    public async Task Skip()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player.CurrentItem is null || player is null) return;
        await player.SkipAsync().ConfigureAwait(false);
    }

    [SlashCommand("pause", description: "Pauses the player.", runMode: RunMode.Async)]
    public async Task PauseAsync()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player.State is PlayerState || player is null) return;
        await player.PauseAsync().ConfigureAwait(false);
    }

    [SlashCommand("resume", description: "Resumes the player.", runMode: RunMode.Async)]
    public async Task ResumeAsync()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player.State is not PlayerState.Paused || player is null) return;
        await player.ResumeAsync().ConfigureAwait(false);
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

        var message = await GetOriginalResponseAsync();
        await message.ModifyAsync(msg => msg.Components = components).ConfigureAwait(false);
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
        var message = await GetOriginalResponseAsync();
        await message.ModifyAsync(msg => msg.Components = components).ConfigureAwait(false);
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