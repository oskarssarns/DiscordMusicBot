namespace LavaLinkLouieBot.Helpers;

public static class MusicControlsBuilder
{
    public static MessageComponent BuildControls(bool isPaused, bool isRepeating)
    {
        var builder = new ComponentBuilder()
            .WithButton(isPaused ? "Resume" : "Pause",
                        isPaused ? "resume_button" : "pause_button",
                        ButtonStyle.Primary)
            .WithButton("Skip", "skip_button", ButtonStyle.Secondary)
            .WithButton(isRepeating ? "Stop Repeating" : "Repeat",
                        "repeat_button",
                        ButtonStyle.Primary)
            .WithButton("Stop", "stop_button", ButtonStyle.Danger);

        return builder.Build();
    }
}
