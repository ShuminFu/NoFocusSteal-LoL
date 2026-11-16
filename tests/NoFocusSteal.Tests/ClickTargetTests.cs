using System;
using Xunit;

namespace NoFocusSteal.Tests;

public class ClickTargetTests
{
    private static readonly IntPtr Lobby = new IntPtr(11);
    private static readonly IntPtr Foreground = new IntPtr(22);
    private static readonly IntPtr Captured = new IntPtr(33);

    [Fact]
    public void VisibleCursorWithNoCaptureClicksTheWindowUnderThePointer()
    {
        Assert.Equal(Lobby, ClickTarget.Resolve(Lobby, Foreground, IntPtr.Zero, cursorVisible: true));
    }

    [Fact]
    public void CapturedMouseBelongsToTheCapturingWindowNotTheLeftoverCursor()
    {
        Assert.Equal(Captured, ClickTarget.Resolve(Lobby, Foreground, Captured, cursorVisible: true));
    }

    [Fact]
    public void HiddenCursorBelongsToTheForegroundWindow()
    {
        Assert.Equal(Foreground, ClickTarget.Resolve(Lobby, Foreground, IntPtr.Zero, cursorVisible: false));
    }

    [Fact]
    public void CaptureTakesPriorityOverAHiddenCursor()
    {
        Assert.Equal(Captured, ClickTarget.Resolve(Lobby, Foreground, Captured, cursorVisible: false));
    }

    [Fact]
    public void HiddenCursorWithNoForegroundFallsBackToTheWindowUnderThePointer()
    {
        Assert.Equal(Lobby, ClickTarget.Resolve(Lobby, IntPtr.Zero, IntPtr.Zero, cursorVisible: false));
    }
}
