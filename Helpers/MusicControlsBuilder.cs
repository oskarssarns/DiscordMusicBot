namespace LavaLinkLouieBot.Helpers;

public static class MusicControlsBuilder
{
    public static MessageComponent BuildControls(
        bool isPaused,
        bool isRepeating,
        int upcomingCount = 0)
    {
        var builder = new ComponentBuilder()
            .WithButton(isPaused ? "Resume" : "Pause",
                        isPaused ? "resume_button" : "pause_button",
                        ButtonStyle.Primary)
            .WithButton("Next", "next_button", ButtonStyle.Secondary)
            .WithButton(isRepeating ? "Stop Repeating" : "Repeat",
                        "repeat_button",
                        ButtonStyle.Primary)
            .WithButton("Stop", "stop_button", ButtonStyle.Danger);

        int removeButtonCount = Math.Min(4, upcomingCount);
        for (int i = 0; i < removeButtonCount; i++)
        {
            builder.WithButton(
                $"Remove {i + 1}",
                $"remove_queue_{i + 1}",
                ButtonStyle.Secondary,
                row: 1);
        }

        return builder.Build();
    }
}
