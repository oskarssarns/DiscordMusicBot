using LavaLinkLouieBot.Data;
using LavaLinkLouieBot.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LavaLinkLouieBot.Tests;

public sealed class PlaylistServiceTests
{
    [Fact]
    public async Task AddTrackIfMissingAsync_AllowsSameTrackInDifferentPlaylists()
    {
        await using var database = await CreateDatabaseAsync();
        var service = CreateService(database.Context);

        bool firstAdd = await service.AddTrackIfMissingAsync("radio", "https://youtu.be/a", "Track A", "user");
        bool duplicateAdd = await service.AddTrackIfMissingAsync("radio", "https://youtu.be/a", "Track A", "user");
        bool otherPlaylistAdd = await service.AddTrackIfMissingAsync("favorites", "https://youtu.be/a", "Track A", "user");

        Assert.True(firstAdd);
        Assert.False(duplicateAdd);
        Assert.True(otherPlaylistAdd);
    }

    [Fact]
    public async Task GetShuffledPlaylistSongsAsync_ReturnsOnlyRequestedPlaylist()
    {
        await using var database = await CreateDatabaseAsync();
        var service = CreateService(database.Context);

        await service.AddTrackIfMissingAsync("radio", "https://youtu.be/a", "Track A", "user");
        await service.AddTrackIfMissingAsync("radio", "https://youtu.be/b", "Track B", "user");
        await service.AddTrackIfMissingAsync("favorites", "https://youtu.be/c", "Track C", "user");

        var songs = await service.GetShuffledPlaylistSongsAsync("radio");

        Assert.Equal(2, songs.Count);
        Assert.All(songs, song => Assert.Equal("radio", song.Playlist));
        Assert.Contains(songs, song => song.Link == "https://youtu.be/a");
        Assert.Contains(songs, song => song.Link == "https://youtu.be/b");
    }

    [Fact]
    public async Task RemoveTrackAsync_RemovesTrackByLinkFromRequestedPlaylist()
    {
        await using var database = await CreateDatabaseAsync();
        var service = CreateService(database.Context);

        await service.AddTrackIfMissingAsync("radio", "https://youtu.be/a", "Track A", "user");
        await service.AddTrackIfMissingAsync("favorites", "https://youtu.be/a", "Track A", "user");

        var removedTrack = await service.RemoveTrackAsync("radio", "https://youtu.be/a");

        Assert.NotNull(removedTrack);
        Assert.Equal("Track A", removedTrack.Name);

        var radioSongs = await service.GetShuffledPlaylistSongsAsync("radio");
        var favoriteSongs = await service.GetShuffledPlaylistSongsAsync("favorites");

        Assert.Empty(radioSongs);
        Assert.Single(favoriteSongs);
    }

    [Fact]
    public async Task RemoveTrackAsync_RemovesTrackByExactTitle()
    {
        await using var database = await CreateDatabaseAsync();
        var service = CreateService(database.Context);

        await service.AddTrackIfMissingAsync("radio", "https://youtu.be/a", "Track A", "user");

        var removedTrack = await service.RemoveTrackAsync("radio", "Track A");

        Assert.NotNull(removedTrack);
        Assert.Empty(await service.GetShuffledPlaylistSongsAsync("radio"));
    }

    [Fact]
    public async Task RemoveTrackAsync_ReturnsNull_WhenTrackDoesNotExist()
    {
        await using var database = await CreateDatabaseAsync();
        var service = CreateService(database.Context);

        await service.AddTrackIfMissingAsync("radio", "https://youtu.be/a", "Track A", "user");

        var removedTrack = await service.RemoveTrackAsync("radio", "missing");

        Assert.Null(removedTrack);
        Assert.Single(await service.GetShuffledPlaylistSongsAsync("radio"));
    }

    private static PlaylistService CreateService(MusicDbContext context)
    {
        return new PlaylistService(context, NullLogger<PlaylistService>.Instance);
    }

    private static async Task<TestDatabase> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MusicDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new MusicDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new TestDatabase(connection, context);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public TestDatabase(SqliteConnection connection, MusicDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public MusicDbContext Context { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
