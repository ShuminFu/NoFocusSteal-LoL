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

    [Fact]
    public void MatchesChineseTrayNameToEnglishLobbyTitle()
    {
        // CN WeGame installs often use "英雄联盟" on the tray icon while the
        // lobby window title stays "League of Legends".
        Assert.True(SwitcherClick.Matches("英雄联盟", "League of Legends", "LeagueClientUx.exe"));
        Assert.True(SwitcherClick.Matches("英雄联盟 - 1 running window", "League of Legends", "LeagueClientUx.exe"));
        Assert.True(SwitcherClick.Matches("英雄联盟", "League of Legends (TM) Client", "League of Legends.exe"));
    }

    [Fact]
    public void MatchesEnglishTrayNameToChineseLobbyTitle()
    {
        Assert.True(SwitcherClick.Matches("League of Legends", "英雄联盟", "LeagueClientUx.exe"));
        Assert.True(SwitcherClick.Matches("League of Legends (TM) Client", "英雄联盟", "League of Legends.exe"));
    }

    [Fact]
    public void GenericOverflowNamesDoNotMatchTheLobby()
    {
        foreach (string name in new[]
        {
            "NotifyIconOverflowWindow",
            "Overflow Notification Area",
            "Notification overflow",
            "Show hidden icons",
            "Notification area",
            "任务栏",
            "通知溢出",
            "显示隐藏的图标",
            "系统托盘",
            "溢出",
        })
        {
            Assert.False(SwitcherClick.Matches(name, "League of Legends", "LeagueClientUx.exe"), name);
            Assert.False(SwitcherClick.UsableSwitcherName(name), name);
        }
    }

    [Fact]
    public void GenericTaskbarClickDoesNotMatchAnUnrelatedApp()
    {
        Assert.False(SwitcherClick.Matches("Show hidden icons", "Grok Bot", "Grok Bot.exe"));
        Assert.False(SwitcherClick.Matches("通知溢出", "从夯到拉主观测评千珏皮肤！【阿丁】_游戏热门视频 - Google Chrome", "chrome.exe"));
    }

    [Fact]
    public void WeGameTrayNameDoesNotByItselfMatchTheLobby()
    {
        // A generic WeGame / Pallas tray click must not open the lobby gate.
        Assert.False(SwitcherClick.Matches("WeGame", "League of Legends", "LeagueClientUx.exe"));
        Assert.False(SwitcherClick.Matches("Pallas", "League of Legends", "LeagueClientUx.exe"));
        Assert.False(SwitcherClick.Matches("腾讯游戏平台", "League of Legends", "LeagueClientUx.exe"));
    }

    [Fact]
    public void PointInChromeDoesNotHitATaskbarButtonRect()
    {
        // Taskbar button along the bottom; a click in the Chrome window above it
        // must not be treated as a hit on that button, even if the button is named
        // 英雄联盟 / League of Legends.
        const int buttonLeft = 100, buttonTop = 1040, buttonWidth = 80, buttonHeight = 40;
        Assert.True(SwitcherClick.RectContains(buttonLeft, buttonTop, buttonWidth, buttonHeight, 140, 1050));
        Assert.False(SwitcherClick.RectContains(buttonLeft, buttonTop, buttonWidth, buttonHeight, 140, 900));
        Assert.False(SwitcherClick.RectContains(buttonLeft, buttonTop, buttonWidth, buttonHeight, 140, 1039));
        Assert.False(SwitcherClick.RectContains(buttonLeft, buttonTop, buttonWidth, buttonHeight, 99, 1050));
    }
}
