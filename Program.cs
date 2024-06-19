using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Lavalink4NET.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using LavaLinkLouieBot.Data;
using MongoDB.Driver;
using System;
using System.Linq;

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
    options.BaseAddress = new Uri(configuration["LavaLinkOptions:BaseAddress"]);
    options.Passphrase = configuration["LavaLinkOptions:PassPhrase"];
});
builder.Services.AddLogging(x => x.AddConsole().SetMinimumLevel(LogLevel.Warning));

// MongoDB configuration
builder.Services.AddSingleton<IMongoClient, MongoClient>(sp =>
{
    var settings = configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>();
    return new MongoClient(settings.ConnectionString);
});
builder.Services.AddSingleton(sp =>
{
    var settings = configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>();
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(settings.DatabaseName);
});

builder.Services.AddSingleton<SongRepository>();

var app = builder.Build();

try
{
    var client = app.Services.GetRequiredService<IMongoClient>();
    var databaseNames = client.ListDatabaseNames().ToList();
    Console.WriteLine("Successfully connected to MongoDB. Available databases:");
    foreach (var dbName in databaseNames)
    {
        Console.WriteLine($"- {dbName}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to connect to MongoDB: {ex.Message}");
}

app.Run();
