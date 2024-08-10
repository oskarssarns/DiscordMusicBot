using Discord;
using Discord.Interactions;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Filters;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Players.Vote;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using LavaLinkLouieBot.Data;
using LavaLinkLouieBot.Models;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

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
    /// <summary>
    /// Adds link to users playlist.
    /// </summary>
    /// <param name="query">Track query</param>
    /// <returns>a task that represents the asynchronous operation</returns>
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

    /// <summary>
    ///     Plays all songs from specific playlist.
    /// </summary>
    /// <param name="playlist">Track query</param>
    /// <returns>a task that represents the asynchronous operation</returns>
    [SlashCommand("pp", "Plays all songs from specific playlist", runMode: RunMode.Async)]
    public async Task PlayPlayList(string playlist)
    {
        List<Song> playlistSongs = await _guildDbContext.louie_bot_playlists.Where(s => s.Playlist == playlist).ToListAsync();
        playlistSongs = playlistSongs.OrderBy(s => new Random().Next()).ToList();

        await DeferAsync().ConfigureAwait(false);
        var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);

        if (player is null)
        {
            await FollowupAsync("No player found!").ConfigureAwait(false);
            return;
        }

        foreach (var track in playlistSongs)
        {
            var position = await player.PlayAsync(track.Link).ConfigureAwait(false);
        }
        await FollowupAsync($"🔈 Playing ♂: {playlistSongs[0].Playlist} playlist").ConfigureAwait(false);
    }

    /// <summary>
    ///     Disconnects from the current voice channel connected to asynchronously.
    /// </summary>
    /// <returns>a task that represents the asynchronous operation</returns>
    [SlashCommand("disconnect", "Disconnects from the current voice channel connected to", runMode: RunMode.Async)]
    public async Task Disconnect()
    {
        VoteLavalinkPlayer? player = await GetPlayerAsync().ConfigureAwait(false);
        if (player is null) return;
        await player.DisconnectAsync().ConfigureAwait(false);
        await RespondAsync("Disconnected.").ConfigureAwait(false);
    }

    /// <summary>
    /// Changes the playback speed of the current track.
    /// </summary>
    /// <param name="speed">The playback speed (0.5 - 3.0)</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("speed", description: "Changes the playback speed (0.5 - 3.0)", runMode: RunMode.Async)]
    public async Task ChangeSpeed(double speed)
    {
        if (speed < 0.5 || speed > 3.0)
        {
            await RespondAsync("Speed out of range: 0.5 - 3.0!").ConfigureAwait(false);
            return;
        }

        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

        if (player is null)
        {
            await RespondAsync("No player found!").ConfigureAwait(false);
            return;
        }

        var timescaleFilterOptions = new TimescaleFilterOptions
        {
            Speed = (float?)speed
        };

        player.Filters.SetFilter(timescaleFilterOptions);
        await player.Filters.CommitAsync().ConfigureAwait(false);
        await RespondAsync($"Playback speed set to {speed}x.").ConfigureAwait(false);
    }

    /// <summary>
    ///     Plays music asynchronously.
    /// </summary>
    /// <param name="query">the search query</param>
    /// <returns>a task that represents the asynchronous operation</returns>
    [SlashCommand("play", description: "Plays music", runMode: RunMode.Async)]
    public async Task Play(string query)
    {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);

        if (player is null)
        {
            await FollowupAsync("No player found!").ConfigureAwait(false);
            return;
        }

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

    /// <summary>
    ///     Plays gachi radio.
    /// </summary>
    /// <returns>a task that represents the asynchronous operation</returns>
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

    /// <summary>
    ///     Shows the track position asynchronously.
    /// </summary>
    /// <returns>a task that represents the asynchronous operation</returns>
    [SlashCommand("position", description: "Shows the track position", runMode: RunMode.Async)]
    public async Task Position()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

        if (player is null)
        {
            return;
        }

        if (player.CurrentItem is null)
        {
            await RespondAsync("Nothing playing!").ConfigureAwait(false);
            return;
        }

        await RespondAsync($"Position: {player.Position?.Position} / {player.CurrentTrack.Duration}.").ConfigureAwait(false);
    }

    /// <summary>
    ///     Stops the current track asynchronously.
    /// </summary>
    /// <returns>a task that represents the asynchronous operation</returns>
    [SlashCommand("stop", description: "Stops the current track", runMode: RunMode.Async)]
    public async Task Stop()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.CurrentItem is null)
        {
            await RespondAsync("Nothing playing!").ConfigureAwait(false);
            return;
        }

        await player.StopAsync().ConfigureAwait(false);
        await player.DisconnectAsync().ConfigureAwait(false);
        await RespondAsync("Stopped playing.").ConfigureAwait(false);
    }

    /// <summary>
    ///     Updates the player volume asynchronously.
    /// </summary>
    /// <param name="volume">the volume (1 - 1000)</param>
    /// <returns>a task that represents the asynchronous operation</returns>
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

        if (player is null)
        {
            return;
        }

        if (player.CurrentItem is null)
        {
            await RespondAsync("Nothing playing!").ConfigureAwait(false);
            return;
        }

        await player.SkipAsync().ConfigureAwait(false);

        var track = player.CurrentItem;

        if (track is not null)
        {
            await RespondAsync($"Skipped. Now playing: {track.Track!.Title}").ConfigureAwait(false);
        }
        else
        {
            await RespondAsync("Skipped. Stopped playing because the queue is now empty.").ConfigureAwait(false);
        }
    }

    [SlashCommand("pause", description: "Pauses the player.", runMode: RunMode.Async)]
    public async Task PauseAsync()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.State is PlayerState.Paused)
        {
            await RespondAsync("Player is already paused.").ConfigureAwait(false);
            return;
        }

        await player.PauseAsync().ConfigureAwait(false);
        await RespondAsync("Paused.").ConfigureAwait(false);
    }

    [SlashCommand("resume", description: "Resumes the player.", runMode: RunMode.Async)]
    public async Task ResumeAsync()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.State is not PlayerState.Paused)
        {
            await RespondAsync("Player is not paused.").ConfigureAwait(false);
            return;
        }

        await player.ResumeAsync().ConfigureAwait(false);
        await RespondAsync("Resumed.").ConfigureAwait(false);
    }
    #endregion
    #region Buttons
    [ComponentInteraction("pause_button")]
    public async Task HandlePauseButton()
    {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

        if (player is null)
        {
            //await ModifyOriginalResponseAsync(msg => msg.Content = "No player found!").ConfigureAwait(false);
            return;
        }

        await player.PauseAsync().ConfigureAwait(false);

        new ComponentBuilder()
            .WithButton("Resume", "resume_button", ButtonStyle.Primary)
            .WithButton("Skip", "skip_button", ButtonStyle.Secondary)
            .WithButton("Repeat", "repeat_button", ButtonStyle.Primary)
            .WithButton("Stop", "stop_button", ButtonStyle.Danger).Build();

        //await ModifyOriginalResponseAsync(msg =>
        //{
        //    msg.Content = "Paused.";
        //    msg.Components = builder.Build();
        //}).ConfigureAwait(false);
    }

    [ComponentInteraction("resume_button")]
    public async Task HandleResumeButton()
    {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

        if (player is null)
        {
            //await ModifyOriginalResponseAsync(msg => msg.Content = "No player found!").ConfigureAwait(false);
            return;
        }

        await player.ResumeAsync().ConfigureAwait(false);

        new ComponentBuilder()
            .WithButton("Pause", "pause_button", ButtonStyle.Primary)
            .WithButton("Skip", "skip_button", ButtonStyle.Secondary)
            .WithButton("Repeat", "repeat_button", ButtonStyle.Primary)
            .WithButton("Stop", "stop_button", ButtonStyle.Danger).Build();
        //await ModifyOriginalResponseAsync(msg =>
        //{
        //    msg.Content = "Resumed.";
        //    msg.Components = builder.Build();
        //}).ConfigureAwait(false);
    }

    [ComponentInteraction("skip_button")]
    public async Task HandleSkipButton()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

        if (player is null)
        {
            await RespondAsync("No player found!").ConfigureAwait(false);
            return;
        }

        await player.SkipAsync().ConfigureAwait(false);

        await RespondAsync("Skipped the current track.").ConfigureAwait(false);
    }

    [ComponentInteraction("stop_button")]
    public async Task HandleStopButton()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

        if (player is null)
        {
            await RespondAsync("No player found!").ConfigureAwait(false);
            return;
        }

        await player.StopAsync().ConfigureAwait(false);

        await RespondAsync("Stopped the player.").ConfigureAwait(false);
    }

    [ComponentInteraction("repeat_button")]
    public async Task HandleRepeatButton()
    {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

        if (player is null)
        {
            //await ModifyOriginalResponseAsync(msg => msg.Content = "No player found!").ConfigureAwait(false);
            return;
        }

        player.RepeatMode = player.RepeatMode == TrackRepeatMode.Track ? TrackRepeatMode.None : TrackRepeatMode.Track;
        new ComponentBuilder()
            .WithButton("Pause", "pause_button", ButtonStyle.Primary)
            .WithButton("Skip", "skip_button", ButtonStyle.Secondary)
            .WithButton(player.RepeatMode == TrackRepeatMode.Track ? "Stop Repeating" : "Repeat", "repeat_button", ButtonStyle.Primary)
            .WithButton("Stop", "stop_button", ButtonStyle.Danger).Build();
        
        //var repeatStatus = player.RepeatMode == TrackRepeatMode.Track ? "Repeat mode enabled." : "Repeat mode disabled.";
        //await ModifyOriginalResponseAsync(msg =>
        //{
        //    msg.Content = repeatStatus;
        //    msg.Components = builder.Build();
        //}).ConfigureAwait(false);
    }
    #endregion
    #region Helpers
    /// <summary>
    ///     Gets the guild player asynchronously.
    /// </summary>
    /// <param name="connectToVoiceChannel">
    ///     a value indicating whether to connect to a voice channel
    /// </param>
    /// <returns>
    ///     a task that represents the asynchronous operation. The task result is the lavalink player.
    /// </returns>
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