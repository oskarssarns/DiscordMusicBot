namespace LavaLinkLouieBot.Data;
public class GachiDbContext : DbContext
{
    public GachiDbContext(DbContextOptions<GachiDbContext> options): base(options) { }
    public DbSet<Song> louie_bot_playlists { get; set; } 
    public DbSet<LavalinkServer> lavalink_servers { get; set; } 
}