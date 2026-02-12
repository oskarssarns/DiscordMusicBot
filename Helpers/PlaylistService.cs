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
        bool exists = await _dbContext.louie_bot_playlists.AnyAsync(s => s.Link == query);
        if (exists)
        {
            return false;
        }

        var song = new Song
        {
            Name = title,
            Link = query,
            Playlist = playlist,
            UserAdded = userAdded,
            Created = DateTime.UtcNow
        };

        await _dbContext.louie_bot_playlists.AddAsync(song);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Added track '{Title}' to playlist '{Playlist}' by user '{UserAdded}'.",
            title,
            playlist,
            userAdded);

        return true;
    }

    public async Task<List<Song>> GetShuffledPlaylistSongsAsync(string playlist)
    {
        var songs = await _dbContext.louie_bot_playlists
            .Where(s => s.Playlist == playlist)
            .ToListAsync();

        return songs.OrderBy(_ => Guid.NewGuid()).ToList();
    }
}
