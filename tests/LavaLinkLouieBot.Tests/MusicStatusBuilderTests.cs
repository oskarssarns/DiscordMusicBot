using LavaLinkLouieBot.Helpers;
using Xunit;

namespace LavaLinkLouieBot.Tests;

public sealed class MusicStatusBuilderTests
{
    [Fact]
    public void BuildStatusContent_HidesSpeed_WhenSpeedIsDefault()
    {
        var result = MusicStatusBuilder.BuildStatusContent(
            currentTrackTitle: "Track A",
            speed: 1.0f,
            upcomingTrackTitles: ["Track B"]);

        Assert.DoesNotContain("⏩ Speed:", result);
    }

    [Fact]
    public void BuildStatusContent_ShowsSpeed_WhenSpeedIsNotDefault()
    {
        var result = MusicStatusBuilder.BuildStatusContent(
            currentTrackTitle: "Track A",
            speed: 1.25f,
            upcomingTrackTitles: ["Track B"]);

        Assert.Contains("⏩ Speed: 1.25x", result);
    }

    [Fact]
    public void BuildStatusContent_ShowsOnlyFirstFourUpcomingTracks()
    {
        var result = MusicStatusBuilder.BuildStatusContent(
            currentTrackTitle: "Track A",
            speed: 1.0f,
            upcomingTrackTitles: ["B", "C", "D", "E", "F"]);

        Assert.Contains("1. B", result);
        Assert.Contains("2. C", result);
        Assert.Contains("3. D", result);
        Assert.Contains("4. E", result);
        Assert.DoesNotContain("5. F", result);
        Assert.DoesNotContain("F", result);
    }

    [Fact]
    public void BuildStatusContent_ShowsEmptyQueueMessage_WhenNoUpcomingTracks()
    {
        var result = MusicStatusBuilder.BuildStatusContent(
            currentTrackTitle: "Track A",
            speed: 1.0f,
            upcomingTrackTitles: []);

        Assert.Contains("⏭ Up next: queue is empty.", result);
    }

    [Fact]
    public void BuildStatusContent_ShowsRemoveHints_WhenEnabled()
    {
        var result = MusicStatusBuilder.BuildStatusContent(
            currentTrackTitle: "Track A",
            speed: 1.0f,
            upcomingTrackTitles: ["B", "C"],
            showQueueRemoveHints: true);

        Assert.Contains("1. B [Remove 1]", result);
        Assert.Contains("2. C [Remove 2]", result);
    }

    [Fact]
    public void BuildStatusContent_IncludesHeader_WhenProvided()
    {
        var result = MusicStatusBuilder.BuildStatusContent(
            currentTrackTitle: "Track A",
            speed: 1.0f,
            upcomingTrackTitles: ["B"],
            header: "🔈 Playlist started");

        var lines = result.Split('\n');
        Assert.Equal("🔈 Playlist started", lines[0]);
    }
}
