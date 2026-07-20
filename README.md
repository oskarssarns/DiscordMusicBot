# LavaLink Louie Bot

Discord music bot built with Discord.Net, Lavalink4NET, local Lavalink, and SQLite playlist storage.

## Features

- Slash commands for music playback.
- Local Lavalink service through Docker Compose.
- YouTube support through the Lavalink YouTube plugin.
- Persistent local playlists in SQLite.
- Playlist playback is shuffled when started.
- Playback controls with Discord buttons.

## Discord Setup

In the Discord Developer Portal, create an install link with these Guild Install scopes:

```text
applications.commands
bot
```

Recommended bot permissions:

```text
View Channels
Send Messages
Read Message History
Connect
Speak
Use Voice Activity
Use Slash Commands
```

Privileged intents are not required. The bot uses only guild and voice-state gateway intents.

## Configuration

Create a local `.env` file:

```bash
cp .env.example .env
```

Set at least:

```text
BOT_TOKEN=your_discord_bot_token
```

Optional values:

```text
LAVALINK_PASSWORD=change_this_lavalink_password
LAVALINK_JAVA_OPTIONS=-Xmx768m
TEST_QUERY=https://www.youtube.com/watch?v=dQw4w9WgXcQ
PLAYBACK_SELF_TEST_QUERY=
PLAYBACK_SELF_TEST_VOICE_CHANNEL_ID=
```

`PLAYBACK_SELF_TEST_QUERY` and `PLAYBACK_SELF_TEST_VOICE_CHANNEL_ID` can be set together to make the bot run a startup playback test.

## Running With Docker

Start the bot and local Lavalink:

```bash
docker compose up -d --build
```

Watch logs:

```bash
docker compose logs -f bot lavalink
```

Stop the stack:

```bash
docker compose down
```

The bot stores playlists in a Docker volume named `musicbot-data` at:

```text
/data/musicbot.db
```

Do not run `docker compose down -v` unless you intentionally want to delete the playlist database volume.

## Commands

Play a YouTube link or search by name:

```text
/play query-or-name:<youtube_link_or_song_name>
```

Play the built-in radio track:

```text
/radio
```

Stop playback and disconnect:

```text
/stop
```

Disconnect:

```text
/disconnect
```

Set volume:

```text
/volume volume:<0-1000>
```

Change speed:

```text
/speed speed:<0.5-3.0>
```

Add a song to a playlist:

```text
/playlistadd playlist:<playlist_name> query-or-name:<youtube_link_or_song_name>
```

Play a playlist in shuffled order:

```text
/pp playlist:<playlist_name>
```

Remove a saved song from a playlist by exact link or exact saved title:

```text
/playlistremove playlist:<playlist_name> query-or-name:<youtube_link_or_exact_saved_title>
```

When a playlist is playing, the status message shows the playlist name plus the specific current track and upcoming tracks.

## Local Development

Run tests:

```bash
dotnet test
```

For non-Docker runs, configure `appsettings.json` or environment variables:

```json
{
  "BotToken": "YOUR_DISCORD_BOT_TOKEN_HERE",
  "LavalinkBaseAddress": "http://localhost:2333",
  "LavalinkPassphrase": "change_this_lavalink_password",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=musicbot.db"
  }
}
```

You still need a Lavalink server running at the configured address.

## Troubleshooting

Check container status:

```bash
docker compose ps
```

Check bot logs:

```bash
docker compose logs -f bot
```

Check Lavalink logs:

```bash
docker compose logs -f lavalink
```

Healthy startup should include:

```text
Playlist database is ready.
Selected Lavalink server: http://lavalink:2333
Connection to Lavalink node established.
Node is ready
Discord interaction commands registered.
```

If slash commands do not update immediately, wait a few minutes. Global Discord command registration can be delayed.

If YouTube playback fails, check `lavalink` logs first. YouTube extractor issues usually appear there, not in the bot logs.
