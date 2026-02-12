namespace LavaLinkLouieBot.Helpers;

public static class MusicStatusBuilder
{
    public static string BuildStatusContent(
        VoteLavalinkPlayer player,
        string? header = null,
        bool showQueueRemoveHints = false)
    {
        string currentTrackTitle = player.CurrentTrack?.Title ?? "Nothing is currently playing.";
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(header))
        {
            lines.Add(header);
        }

        lines.Add($"🎵 Now playing: {currentTrackTitle}");

        float speed = player.Filters?.Timescale?.Speed ?? 1f;
        if (MathF.Abs(speed - 1f) > 0.01f)
        {
            lines.Add($"⏩ Speed: {speed:0.##}x");
        }

        var upcomingTracks = player.Queue.Take(4).ToList();
        if (upcomingTracks.Count == 0)
        {
            lines.Add("⏭ Up next: queue is empty.");
        }
        else
        {
            lines.Add("⏭ Up next:");
            for (int i = 0; i < upcomingTracks.Count; i++)
            {
                string removeHint = showQueueRemoveHints ? $" [Remove {i + 1}]" : string.Empty;
                lines.Add($"{i + 1}. {GetQueueItemTitle(upcomingTracks[i])}{removeHint}");
            }
        }

        return string.Join('\n', lines);
    }

    private static string GetQueueItemTitle(ITrackQueueItem queueItem)
    {
        if (!string.IsNullOrWhiteSpace(queueItem.Track?.Title))
        {
            return queueItem.Track.Title;
        }

        return queueItem.Identifier;
    }
}
