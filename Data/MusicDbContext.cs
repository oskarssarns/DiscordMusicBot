namespace LavaLinkLouieBot.Data;
public class MusicDbContext : DbContext
{
    public MusicDbContext(DbContextOptions<MusicDbContext> options) : base(options) { }

    public DbSet<PlaylistSong> PlaylistSongs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlaylistSong>(entity =>
        {
            entity.ToTable("louie_bot_playlists");
            entity.Property(song => song.Name).IsRequired();
            entity.Property(song => song.Link).IsRequired();
            entity.Property(song => song.Playlist).IsRequired();
            entity.Property(song => song.UserAdded).IsRequired();
            entity.Property(song => song.Created).IsRequired();
            entity.HasIndex(song => new { song.Playlist, song.Link }).IsUnique();
        });
    }
}
