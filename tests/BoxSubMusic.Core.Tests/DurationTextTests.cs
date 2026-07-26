using System;
using BoxSubMusic.Core.Browsing;
using Xunit;

namespace BoxSubMusic.Core.Tests;

/// <summary>
/// The shared duration wordings (§7.1/§7.2/§8/§9). These shapes are what the
/// detail pages have shown since Slice 07; the browse list rows now read from
/// the same place, so a change here is a change everywhere.
/// </summary>
public class DurationTextTests
{
    [Fact]
    public void Track_UnderAnHour_ReadsMinutesAndSeconds()
    {
        Assert.Equal("4:12", DurationText.Track(new TimeSpan(0, 4, 12)));
    }

    [Fact]
    public void Track_PadsSecondsToTwoDigits()
    {
        Assert.Equal("3:07", DurationText.Track(new TimeSpan(0, 3, 7)));
    }

    [Fact]
    public void Track_AnHourOrMore_GrowsAnHoursField()
    {
        Assert.Equal("1:05:03", DurationText.Track(new TimeSpan(1, 5, 3)));
    }

    [Fact]
    public void Total_UnderAnHour_ReadsMinutesOnly()
    {
        Assert.Equal("48 min", DurationText.Total(new TimeSpan(0, 48, 30)));
    }

    [Fact]
    public void Total_AnHourOrMore_ReadsHoursAndMinutes()
    {
        Assert.Equal("9 hr 22 min", DurationText.Total(new TimeSpan(9, 22, 41)));
    }

    [Fact]
    public void Total_MoreThanADay_KeepsCountingHours()
    {
        // Days must not silently drop out of a big favorites/library total.
        Assert.Equal("26 hr 5 min", DurationText.Total(new TimeSpan(1, 2, 5, 0)));
    }

    [Fact]
    public void Total_UnderAMinute_RoundsUpToOne()
    {
        // A non-empty collection reading "0 min" would look like missing data.
        Assert.Equal("1 min", DurationText.Total(TimeSpan.FromSeconds(20)));
    }
}
