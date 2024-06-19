using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Lavalink4NET.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using LavaLinkLouieBot.Data;
using Microsoft.EntityFrameworkCore;

var builder = new HostApplicationBuilder(args);
IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

builder.Services.AddSingleton<IConfiguration>(configuration);
builder.Services.AddSingleton<DiscordSocketClient>();
builder.Services.AddSingleton<InteractionService>();
builder.Services.AddHostedService<DiscordClientHost>();
builder.Services.AddLavalink();
builder.Services.ConfigureLavalink(options =>
{
    options.BaseAddress = new Uri(configuration[$"LavaLinkOptions:BaseAddress"]);
    options.Passphrase = configuration["LavaLinkOptions:PassPhrase"];
});
builder.Services.AddLogging(x => x.AddConsole().SetMinimumLevel(LogLevel.Trace));

builder.Services.AddDbContext<GachiDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("GachiBase")));

builder.Build().Run();