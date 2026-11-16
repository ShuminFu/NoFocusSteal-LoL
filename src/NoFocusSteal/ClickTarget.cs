using System;
using System.Runtime.InteropServices;

namespace NoFocusSteal;

/// <summary>
/// Decides which top-level window a mouse button-down belongs to.
/// Remote-control apps and games often leave the visible Windows cursor sitting over
/// some other window, or hide it, while they keep the mouse captured. In those cases
/// the click is still meant for the foreground window, not whatever happens to be
/// under the leftover cursor.
/// A visible cursor with no capture still counts as a click on the window under the
/// point, so you can click a background window (including the lobby) to bring it forward.
/// </summary>
internal static class ClickTarget
{
    public static IntPtr Resolve(IntPtr underRoot, IntPtr foregroundRoot, IntPtr capturedRoot, bool cursorVisible)
    {
        if (capturedRoot != IntPtr.Zero)
            return capturedRoot;
        if (!cursorVisible && foregroundRoot != IntPtr.Zero)
            return foregroundRoot;
        return underRoot;
    }

    public static IntPtr ResolveFromDesktop(IntPtr under, IntPtr foreground)
    {
        IntPtr captured = IntPtr.Zero;
        bool cursorVisible = true;
        if (foreground != IntPtr.Zero)
        {
            uint tid = Native.GetWindowThreadProcessId(foreground, out _);
            var gti = new Native.GUITHREADINFO { cbSize = (uint)Marshal.SizeOf(typeof(Native.GUITHREADINFO)) };
            if (Native.GetGUIThreadInfo(tid, ref gti) && gti.hwndCapture != IntPtr.Zero)
                captured = Root(gti.hwndCapture);
        }

        var ci = new Native.CURSORINFO { cbSize = Marshal.SizeOf(typeof(Native.CURSORINFO)) };
        if (Native.GetCursorInfo(ref ci))
        {
            cursorVisible = (ci.flags & Native.CURSOR_SHOWING) != 0
                            && (ci.flags & Native.CURSOR_SUPPRESSED) == 0;
        }

        return Resolve(Root(under), Root(foreground), captured, cursorVisible);
    }

    private static IntPtr Root(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return hwnd;
        IntPtr root = Native.GetAncestor(hwnd, Native.GA_ROOTOWNER);
        return root != IntPtr.Zero ? root : hwnd;
    }
}
