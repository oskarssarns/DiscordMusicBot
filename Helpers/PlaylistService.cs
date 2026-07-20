namespace LavaLinkLouieBot.Helpers;

public sealed class PlaylistService
{
    private readonly MusicDbContext _dbContext;
    private readonly ILogger<PlaylistService> _logger;

    public PlaylistService(MusicDbContext dbContext, ILogger<PlaylistService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> AddTrackIfMissingAsync(string playlist, string query, string title, string userAdded)
    {
        string normalizedPlaylist = playlist.Trim();
        string normalizedQuery = query.Trim();

        bool exists = await _dbContext.louie_bot_playlists
            .AnyAsync(s => s.Playlist == normalizedPlaylist && s.Link == normalizedQuery);
        if (exists)
        {
            return false;
        }

        var song = new Song
        {
            Name = title,
            Link = normalizedQuery,
            Playlist = normalizedPlaylist,
            UserAdded = userAdded,
            Created = DateTime.UtcNow
        };

        await _dbContext.louie_bot_playlists.AddAsync(song);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Added track '{Title}' to playlist '{Playlist}' by user '{UserAdded}'.",
            title,
            normalizedPlaylist,
            userAdded);

        return true;
    }

    public async Task<List<Song>> GetShuffledPlaylistSongsAsync(string playlist)
    {
        string normalizedPlaylist = playlist.Trim();

        var songs = await _dbContext.louie_bot_playlists
            .Where(s => s.Playlist == normalizedPlaylist)
            .ToListAsync();

        return songs.OrderBy(_ => Guid.NewGuid()).ToList();
    }

    public async Task<Song?> RemoveTrackAsync(string playlist, string query)
    {
        string normalizedPlaylist = playlist.Trim();
        string normalizedQuery = query.Trim();

        var song = await _dbContext.louie_bot_playlists
            .FirstOrDefaultAsync(s =>
                s.Playlist == normalizedPlaylist &&
                (s.Link == normalizedQuery || s.Name == normalizedQuery));

        if (song is null)
        {
            return null;
        }

        _dbContext.louie_bot_playlists.Remove(song);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Removed track '{Title}' from playlist '{Playlist}'.",
            song.Name,
            normalizedPlaylist);

        return song;
    }
}
