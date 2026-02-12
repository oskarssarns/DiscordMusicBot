namespace LavaLinkLouieBot.Helpers;

public sealed class PlaybackService
{
    private const string MusicBotRadio = "https://www.youtube.com/watch?v=akHAQD3o1NA";

    private readonly IAudioService _audioService;
    private readonly MusicInteractionService _interactionService;
    private readonly MusicMessageService _messageService;
    private readonly ILogger<PlaybackService> _logger;

    public PlaybackService(
        IAudioService audioService,
        MusicInteractionService interactionService,
        MusicMessageService messageService,
        ILogger<PlaybackService> logger)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _interactionService = interactionService ?? throw new ArgumentNullException(nameof(interactionService));
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ChangeSpeedAsync(SocketInteractionContext context, double speed)
    {
        if (speed is < 0.5 or > 3.0)
        {
            await _interactionService.SendInteractionMessageAsync(context, "Speed must be between 0.5 and 3.0");
            return;
        }

        var player = await _interactionService.GetPlayerAsync(context, connectToVoiceChannel: false);
        if (player is null) return;

        player.Filters.SetFilter(new TimescaleFilterOptions { Speed = (float)speed });
        await player.Filters.CommitAsync();
        await _interactionService.UpdatePlayerStatusMessageAsync(context, player);
        await _interactionService.TryDeleteOriginalResponseAsync(context);
    }

    public async Task PlayAsync(SocketInteractionContext context, string query)
    {
        var player = await _interactionService.GetPlayerAsync(context, connectToVoiceChannel: true);
        if (player is null) return;

        try
        {
            LavalinkTrack? track = query.Contains("&list=")
                ? (await _audioService.Tracks.LoadTracksAsync(query, TrackSearchMode.YouTube)).Tracks.FirstOrDefault()
                : await _audioService.Tracks.LoadTrackAsync(query, TrackSearchMode.YouTube);

            if (track is null)
            {
                await _interactionService.SendInteractionMessageAsync(context, "Failed to load track.");
                return;
            }

            await player.PlayAsync(track);
            await _interactionService.UpdatePlayerStatusMessageAsync(context, player);
            await _interactionService.TryDeleteOriginalResponseAsync(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to play query '{Query}' in guild {GuildId}.", query, context.Guild.Id);
            await _interactionService.SendInteractionMessageAsync(context, $"Error loading track: {ex.Message}");
        }
    }

    public async Task RadioAsync(SocketInteractionContext context)
    {
        var player = await _interactionService.GetPlayerAsync(context, connectToVoiceChannel: true);
        if (player is null) return;

        var track = await _audioService.Tracks.LoadTrackAsync(MusicBotRadio, TrackSearchMode.YouTube);
        if (track is null)
        {
            await _interactionService.SendInteractionMessageAsync(context, "Failed to load radio track.");
            return;
        }

        await player.PlayAsync(track);
        await _interactionService.UpdatePlayerStatusMessageAsync(context, player);
        await _interactionService.TryDeleteOriginalResponseAsync(context);
    }

    public async Task StopAsync(SocketInteractionContext context)
    {
        var player = await _interactionService.GetPlayerAsync(context, connectToVoiceChannel: false);
        if (player is null) return;

        if (player.CurrentItem is null)
        {
            await _interactionService.SendInteractionMessageAsync(context, "Nothing is currently playing.");
            return;
        }

        await player.StopAsync();
        await player.DisconnectAsync();

        await _messageService.SendOrUpdateAsync(context, "⏹ Stopped playback", new ComponentBuilder().Build());
        _messageService.Clear(context.Guild.Id);
        await _interactionService.TryDeleteOriginalResponseAsync(context);
    }

    public async Task VolumeAsync(SocketInteractionContext context, int volume)
    {
        if (volume is < 0 or > 1000)
        {
            await _interactionService.SendInteractionMessageAsync(context, "Volume out of range: 0% - 1000%!");
            return;
        }

        var player = await _interactionService.GetPlayerAsync(context, connectToVoiceChannel: false);
        if (player is null) return;

        await player.SetVolumeAsync(volume / 100f);
        await _interactionService.UpdatePlayerStatusMessageAsync(context, player);
        await _interactionService.TryDeleteOriginalResponseAsync(context);
    }
}
