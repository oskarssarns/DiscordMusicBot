namespace LavaLinkLouieBot.Services;

public sealed class PlaybackSourceService
{
    private readonly Dictionary<ulong, string> _playlistByGuild = new();

    public void SetPlaylist(ulong guildId, string playlist)
    {
        _playlistByGuild[guildId] = playlist.Trim();
    }

    public string? GetPlaylist(ulong guildId)
    {
        return _playlistByGuild.TryGetValue(guildId, out string? playlist)
            ? playlist
            : null;
    }

    public void Clear(ulong guildId)
    {
        _playlistByGuild.Remove(guildId);
    }
}
