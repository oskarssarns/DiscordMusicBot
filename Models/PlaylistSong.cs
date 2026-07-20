namespace LavaLinkLouieBot.Models;
public class PlaylistSong
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Link { get; set; }
    public string? Playlist { get; set; }
    public string? UserAdded { get; set; }
    public DateTime? Created { get; set; }
}
