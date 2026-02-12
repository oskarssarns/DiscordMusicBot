namespace LavaLinkLouieBot.Helpers;

public static class MusicStatusBuilder
{
    public static string BuildStatusContent(
        VoteLavalinkPlayer player,
        string? header = null,
        bool showQueueRemoveHints = false)
    {
        string currentTrackTitle = player.CurrentTrack?.Title ?? "Nothing is currently playing.";
        float speed = player.Filters?.Timescale?.Speed ?? 1f;
        var upcomingTrackTitles = player.Queue
            .Take(4)
            .Select(GetQueueItemTitle)
            .ToList();

        return BuildStatusContent(
            currentTrackTitle,
            speed,
            upcomingTrackTitles,
            header,
            showQueueRemoveHints);
    }

    public static string BuildStatusContent(
        string currentTrackTitle,
        float speed,
        IReadOnlyList<string> upcomingTrackTitles,
        string? header = null,
        bool showQueueRemoveHints = false)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(header))
        {
            lines.Add(header);
        }

        lines.Add($"🎵 Now playing: {currentTrackTitle}");

        if (MathF.Abs(speed - 1f) > 0.01f)
        {
            lines.Add($"⏩ Speed: {speed:0.##}x");
        }

        if (upcomingTrackTitles.Count == 0)
        {
            lines.Add("⏭ Up next: queue is empty.");
        }
        else
        {
            lines.Add("⏭ Up next:");
            int take = Math.Min(4, upcomingTrackTitles.Count);
            for (int i = 0; i < take; i++)
            {
                string removeHint = showQueueRemoveHints ? $" [Remove {i + 1}]" : string.Empty;
                lines.Add($"{i + 1}. {upcomingTrackTitles[i]}{removeHint}");
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
