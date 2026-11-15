using Xunit;

namespace NoFocusSteal.Tests;

public class SwitcherClickTests
{
    [Fact]
    public void MatchesLobbyTitle()
    {
        Assert.True(SwitcherClick.Matches("League of Legends", "League of Legends", "LeagueClientUx.exe"));
    }

    [Fact]
    public void MatchesRunningWindowSuffix()
    {
        Assert.True(SwitcherClick.Matches("League of Legends - 1 running window", "League of Legends", "LeagueClientUx.exe"));
    }

    [Fact]
    public void DoesNotMatchAnotherApp()
    {
        Assert.False(SwitcherClick.Matches("XT-CA-01", "League of Legends", "LeagueClientUx.exe"));
        Assert.False(SwitcherClick.Matches("Grok Bot", "League of Legends", "LeagueClientUx.exe"));
    }

    [Fact]
    public void EmptyNameDoesNotMatch()
    {
        Assert.False(SwitcherClick.Matches("", "League of Legends", "LeagueClientUx.exe"));
        Assert.False(SwitcherClick.Matches(null, "League of Legends", "LeagueClientUx.exe"));
    }

    [Fact]
    public void MatchesExeNameWhenTitleDiffers()
    {
        Assert.True(SwitcherClick.Matches("GameViewer", "XT-CA-01", "GameViewer.exe"));
    }

    [Fact]
    public void DoesNotTreatChromeTitleDashAsRunningWindowSuffix()
    {
        string title = "AgentPilot 的改进循环是怎么选下一步的 | FunFun Zone - Google Chrome";
        Assert.Equal(title, SwitcherClick.Normalize(title));
        Assert.True(SwitcherClick.Matches(title, title, "chrome.exe"));
        Assert.False(SwitcherClick.Matches(title, "League of Legends", "LeagueClientUx.exe"));
    }
}
