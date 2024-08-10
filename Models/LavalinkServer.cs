namespace LavaLinkLouieBot.Models
{
    public class LavalinkServer
    {
        public int Id { get; set; }
        public string Host { get; set; }
        public string Port { get; set; }
        public string Password { get; set; }
        public string Secure { get; set; }
        public string Version { get; set; }
    }
}
