namespace LavaLinkLouieBot.Data;
public class MusicDbContext : DbContext
{
    public MusicDbContext(DbContextOptions<MusicDbContext> options): base(options) { }
    public DbSet<Song> louie_bot_playlists { get; set; } 
}