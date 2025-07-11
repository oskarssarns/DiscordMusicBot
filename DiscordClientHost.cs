using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Lavalink4NET;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;
using LavaLinkLouieBot.Helpers;

namespace LavaLinkLouieBot
{
    internal sealed class DiscordClientHost : IHostedService, IDisposable
    {
        private readonly DiscordSocketClient _discordSocketClient;
        private readonly InteractionService _interactionService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DiscordClientHost> _logger;
        private readonly IAudioService _audioService;
        private Timer _timer;
        private TaskCompletionSource<bool> _readyTcs;

        public DiscordClientHost(
            DiscordSocketClient discordSocketClient,
            InteractionService interactionService,
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<DiscordClientHost> logger,
            IAudioService audioService)
        {
            ArgumentNullException.ThrowIfNull(discordSocketClient);
            ArgumentNullException.ThrowIfNull(interactionService);
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(audioService);

            _discordSocketClient = discordSocketClient;
            _interactionService = interactionService;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _audioService = audioService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _readyTcs = new TaskCompletionSource<bool>();
            _discordSocketClient.InteractionCreated += InteractionCreated;
            _discordSocketClient.Ready += ClientReady;

            _logger.LogInformation("Starting Discord client...");
            await _discordSocketClient
                .LoginAsync(TokenType.Bot, _configuration["BotToken"])
                .ConfigureAwait(false);

            await _discordSocketClient
                .StartAsync()
                .ConfigureAwait(false);

            _logger.LogInformation("Waiting for Discord client to be ready...");
            try
            {
                await _readyTcs.Task.WaitAsync(TimeSpan.FromSeconds(180), cancellationToken);
                _logger.LogInformation("Discord client is ready.");
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, "Timed out while waiting for Discord client being ready.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while waiting for Discord client being ready.");
                throw;
            }

            _timer = new Timer(CheckAndReconnectLavalink, null, 0, (int)TimeSpan.FromMinutes(5).TotalMilliseconds);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _discordSocketClient.InteractionCreated -= InteractionCreated;
            _discordSocketClient.Ready -= ClientReady;

            await _discordSocketClient
                .StopAsync()
                .ConfigureAwait(false);

            _timer?.Change(Timeout.Infinite, 0);
            _timer?.Dispose();
        }

        private Task InteractionCreated(SocketInteraction interaction)
        {
            var interactionContext = new SocketInteractionContext(_discordSocketClient, interaction);
            return _interactionService.ExecuteCommandAsync(interactionContext, _serviceProvider);
        }

        private async Task ClientReady()
        {
            _readyTcs.SetResult(true);

            await _interactionService
                .AddModulesAsync(Assembly.GetExecutingAssembly(), _serviceProvider)
                .ConfigureAwait(false);

            //await _interactionService
            //    .RegisterCommandsToGuildAsync(ulong.Parse(_configuration["Server2"]))
            //    .ConfigureAwait(false);
            //await _interactionService
            //    .RegisterCommandsToGuildAsync(ulong.Parse(_configuration["Server1"]))
            //    .ConfigureAwait(false);
            //await _interactionService
            //    .RegisterCommandsToGuildAsync(ulong.Parse(_configuration["Server3"]))
            //    .ConfigureAwait(false);
            await _interactionService
                .RegisterCommandsToGuildAsync(ulong.Parse(_configuration["Server4"]))
                .ConfigureAwait(false);
        }

        private async void CheckAndReconnectLavalink(object state)
        {
            try
            {
                bool isPlayingMusic = await CheckLavalinkMusicStatusAsync();
                if (!isPlayingMusic)
                {
                    _logger.LogInformation("Music playback issue detected. Reconnecting to a new Lavalink server.");
                    await ReconnectLavalinkAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking Lavalink server status.");
            }
        }

        private async Task<bool> CheckLavalinkMusicStatusAsync()
        {
            try
            {
                var servers = await LavaLinkHelper.GetLavalinkServers(_configuration["LavaLinkSource"]);
                var onlineServers = await LavaLinkHelper.GetOnlineLavalinkServers(servers, _configuration["TestQuery"], _configuration);
                return onlineServers.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking Lavalink music status.");
                return false;
            }
        }

        private async Task ReconnectLavalinkAsync()
        {
            try
            {
                var newConfig = await GetLavalinkServerConfiguration(_configuration);
                _logger.LogInformation("Reconnecting to Lavalink server: {BaseAddress}", newConfig.BaseAddress);
                // Note: Lavalink4NET v4.0.27 does not support direct restart. Consider updating or handling reconnection differently.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect to Lavalink server.");
            }
        }

        private async Task<LavaLinkLouieBot.Helpers.LavalinkServerConfig> GetLavalinkServerConfiguration(IConfiguration configuration)
        {
            List<LavaLinkLouieBot.Helpers.LavalinkServer> servers = await LavaLinkHelper.GetLavalinkServers(configuration["LavaLinkSource"]);
            List<LavaLinkLouieBot.Helpers.LavalinkServer> onlineServers = await LavaLinkHelper.GetOnlineLavalinkServers(servers, configuration["TestQuery"], configuration);
            if (onlineServers.Count > 0)
            {
                var server = onlineServers[0];
                string scheme = server.Secure.ToLower() == "true" ? "https" : "http";
                return new LavaLinkLouieBot.Helpers.LavalinkServerConfig
                {
                    BaseAddress = $"{scheme}://{server.Host}:{server.Port}",
                    Passphrase = server.Password
                };
            }
            else
            {
                throw new InvalidOperationException("No online servers found.");
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}