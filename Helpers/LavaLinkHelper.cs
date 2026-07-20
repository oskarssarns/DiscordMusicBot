namespace LavaLinkLouieBot.Helpers;

public static class LavaLinkHelper
{
    public static Task<LavalinkServerConfig> GetLavalinkServerConfiguration(IConfiguration configuration)
    {
        string? configuredBaseAddress = configuration["LavalinkBaseAddress"];
        string? configuredPassphrase = configuration["LavalinkPassphrase"];
        if (string.IsNullOrWhiteSpace(configuredBaseAddress) ||
            string.IsNullOrWhiteSpace(configuredPassphrase))
        {
            throw new InvalidOperationException(
                "LavalinkBaseAddress and LavalinkPassphrase must be configured.");
        }

        Console.WriteLine($"Using configured Lavalink server: {configuredBaseAddress}");
        return Task.FromResult(
            new LavalinkServerConfig
            {
                BaseAddress = configuredBaseAddress,
                Passphrase = configuredPassphrase
            });
    }
}
