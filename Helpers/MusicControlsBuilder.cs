namespace LavaLinkLouieBot.Helpers;

public static class MusicControlsBuilder
{
    public static MessageComponent BuildControls(
        bool isPaused,
        bool isRepeating,
        int upcomingCount = 0,
        bool showQueueRemoveButtons = false)
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

        if (showQueueRemoveButtons)
        {
            for (int i = 0; i < 4; i++)
            {
                builder.WithButton(
                    $"Remove {i + 1}",
                    $"remove_queue_{i + 1}",
                    ButtonStyle.Secondary,
                    row: 1,
                    disabled: i >= upcomingCount);
            }
        }

        return builder.Build();
    }
}
