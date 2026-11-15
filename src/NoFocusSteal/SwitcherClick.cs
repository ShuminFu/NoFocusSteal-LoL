using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace NoFocusSteal;

/// <summary>
/// Identifies clicks on the taskbar, its thumbnails, or the Alt+Tab switcher, and whether
/// the button that was clicked belongs to a given window.
/// </summary>
internal static class SwitcherClick
{
    internal static readonly HashSet<string> Classes = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "TaskListThumbnailWnd",
        "MSTaskSwWClass", "MSTaskListWClass", "TaskSwitcherWnd",
        "MultitaskingViewFrame", "XamlExplorerHostIslandWindow",
        "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland",
        "DV2ControlHost",
    };

    public static bool IsSwitcherWindow(IntPtr hwnd)
    {
        for (IntPtr w = hwnd; w != IntPtr.Zero; )
        {
            if (Classes.Contains(ClassName(w))) return true;
            IntPtr parent = Native.GetParent(w);
            if (parent == IntPtr.Zero || parent == w)
            {
                IntPtr root = Native.GetAncestor(w, Native.GA_ROOT);
                if (root == IntPtr.Zero || root == w) break;
                w = root;
                continue;
            }
            w = parent;
        }
        return false;
    }

    public static bool IsPointOnTaskbar(Native.POINT pt)
    {
        if (PointInAppBar(Native.FindWindow("Shell_TrayWnd", null), pt)) return true;
        IntPtr secondary = IntPtr.Zero;
        while ((secondary = Native.FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
        {
            if (Native.GetWindowRect(secondary, out Native.RECT rc) && Contains(rc, pt)) return true;
        }
        return false;
    }

    public static string NameAt(Native.POINT pt)
    {
        try
        {
            if (Native.AccessibleObjectFromPoint(pt, out object acc, out object child) == 0 && acc != null)
            {
                object name = acc.GetType().InvokeMember(
                    "accName",
                    BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                    null, acc, new[] { child ?? 0 });
                if (name is string s && !string.IsNullOrWhiteSpace(s) && !IsGenericSwitcherName(s))
                    return s;
            }
        }
        catch
        {
            // Accessibility can fail on some taskbar buttons; fall back to window text.
        }
        return WindowTextAt(pt);
    }

    /// <summary>
    /// True when a taskbar or Alt+Tab button name refers to the window that is coming forward.
    /// </summary>
    public static bool Matches(string buttonName, string windowTitle, string exeName)
    {
        string name = Normalize(buttonName);
        if (name.Length == 0) return false;

        string title = Normalize(windowTitle);
        if (title.Length > 0
            && (ContainsIgnoreCase(name, title) || ContainsIgnoreCase(title, name)))
            return true;

        string exe = Normalize(Path.GetFileNameWithoutExtension(exeName ?? ""));
        return exe.Length >= 3 && ContainsIgnoreCase(name, exe);
    }

    internal static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        string t = s.Trim();
        int dash = t.LastIndexOf(" - ", StringComparison.Ordinal);
        if (dash > 0)
        {
            string suffix = t.Substring(dash + 3);
            if (suffix.IndexOf("running window", StringComparison.OrdinalIgnoreCase) >= 0
                || suffix.IndexOf("个窗口", StringComparison.Ordinal) >= 0)
                t = t.Substring(0, dash);
        }
        return t.Trim();
    }

    private static bool ContainsIgnoreCase(string haystack, string needle) =>
        haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool IsGenericSwitcherName(string s)
    {
        s = s.Trim();
        return s.Equals("Taskbar", StringComparison.OrdinalIgnoreCase)
               || s.Equals("任务栏", StringComparison.Ordinal)
               || s.Equals("Running applications", StringComparison.OrdinalIgnoreCase)
               || s.Equals("运行中的应用程序", StringComparison.Ordinal);
    }

    private static string WindowTextAt(Native.POINT pt)
    {
        IntPtr under = Native.WindowFromPoint(pt);
        for (IntPtr w = under; w != IntPtr.Zero; )
        {
            var sb = new StringBuilder(512);
            if (Native.GetWindowText(w, sb, sb.Capacity) > 0)
            {
                string t = sb.ToString();
                if (!string.IsNullOrWhiteSpace(t) && !IsGenericSwitcherName(t))
                    return t;
            }
            IntPtr parent = Native.GetParent(w);
            if (parent == IntPtr.Zero || parent == w) break;
            w = parent;
        }
        return null;
    }

    private static string ClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        return Native.GetClassName(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : "";
    }

    private static bool PointInAppBar(IntPtr tray, Native.POINT pt)
    {
        if (tray == IntPtr.Zero) return false;
        var data = new Native.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf(typeof(Native.APPBARDATA)),
            hWnd = tray,
        };
        if (Native.SHAppBarMessage(Native.ABM_GETTASKBARPOS, ref data) != IntPtr.Zero)
            return Contains(data.rc, pt);
        return Native.GetWindowRect(tray, out Native.RECT rc) && Contains(rc, pt);
    }

    private static bool Contains(Native.RECT rc, Native.POINT pt) =>
        pt.X >= rc.Left && pt.X < rc.Right && pt.Y >= rc.Top && pt.Y < rc.Bottom;
}
