using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace NoFocusSteal;

/// <summary>
/// Identifies clicks on the taskbar, its thumbnails, the system tray / overflow,
/// or the Alt+Tab switcher, and whether the button that was clicked belongs to a
/// given window.
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

    // Top-level XAML hosts that Windows 11 sometimes uses for the tray overflow
    // instead of NotifyIconOverflowWindow. Only counted when the window belongs
    // to explorer and sits next to the taskbar, so ordinary app windows with
    // these class names are not treated as switcher clicks.
    private static readonly HashSet<string> ExplorerFlyoutClasses = new(StringComparer.Ordinal)
    {
        "Windows.UI.Core.CoreWindow",
        "WinUIDesktopWin32WindowClass",
        "Microsoft.UI.Windowing.WindowHost",
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
                if (root == IntPtr.Zero || root == w)
                {
                    if (IsExplorerTrayFlyout(w)) return true;
                    break;
                }
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
        string uia = NameFromUia(pt);
        if (UsableSwitcherName(uia)) return uia;

        try
        {
            if (Native.AccessibleObjectFromPoint(pt, out object acc, out object child) == 0 && acc != null)
            {
                object name = acc.GetType().InvokeMember(
                    "accName",
                    BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                    null, acc, new[] { child ?? 0 });
                if (name is string s && UsableSwitcherName(s))
                    return s;
            }
        }
        catch
        {
            // Accessibility can fail on some taskbar buttons; fall back to window text.
        }

        string text = WindowTextAt(pt);
        return UsableSwitcherName(text) ? text : null;
    }

    /// <summary>
    /// True when a taskbar, tray, or Alt+Tab button name refers to the window that is coming forward.
    /// </summary>
    public static bool Matches(string buttonName, string windowTitle, string exeName)
    {
        string name = Normalize(buttonName);
        if (name.Length == 0 || IsGenericSwitcherName(name)) return false;

        foreach (string candidate in MatchCandidates(windowTitle, exeName))
        {
            if (ContainsIgnoreCase(name, candidate) || ContainsIgnoreCase(candidate, name))
                return true;
        }
        return false;
    }

    /// <summary>Title, exe stem, and known display names for the incoming process.</summary>
    internal static IEnumerable<string> MatchCandidates(string windowTitle, string exeName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void add(string s)
        {
            string t = Normalize(s);
            if (t.Length >= 3) seen.Add(t);
        }

        add(windowTitle);
        add(Path.GetFileNameWithoutExtension(exeName ?? ""));
        if (IsLolClient(exeName))
        {
            add("League of Legends");
            add("League of Legends (TM) Client");
            add("英雄联盟");
            add("LeagueClientUx");
            add("LeagueClient");
            add("LeagueClientUxRender");
            add("Riot Client");
            add("RiotClient");
        }
        return seen;
    }

    internal static bool IsLolClient(string exeName)
    {
        string exe = Path.GetFileName(exeName ?? "");
        return exe.Equals("LeagueClientUx.exe", StringComparison.OrdinalIgnoreCase)
               || exe.Equals("LeagueClient.exe", StringComparison.OrdinalIgnoreCase)
               || exe.Equals("LeagueClientUxRender.exe", StringComparison.OrdinalIgnoreCase)
               || exe.Equals("League of Legends.exe", StringComparison.OrdinalIgnoreCase);
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

    internal static bool UsableSwitcherName(string s) =>
        !string.IsNullOrWhiteSpace(s) && !IsGenericSwitcherName(s.Trim());

    internal static bool IsGenericSwitcherName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return true;
        s = s.Trim();
        return EqualsI(s, "Taskbar")
               || EqualsI(s, "任务栏")
               || EqualsI(s, "Running applications")
               || EqualsI(s, "运行中的应用程序")
               || EqualsI(s, "Notification overflow")
               || EqualsI(s, "Overflow")
               || EqualsI(s, "Overflow Notification Area")
               || EqualsI(s, "Overflow notification area")
               || EqualsI(s, "NotifyIconOverflowWindow")
               || EqualsI(s, "Notification area")
               || EqualsI(s, "User Promoted Notification Area")
               || EqualsI(s, "System Promoted Notification Area")
               || EqualsI(s, "Show hidden icons")
               || EqualsI(s, "Hidden icons")
               || EqualsI(s, "System tray")
               || EqualsI(s, "Tray")
               || EqualsI(s, "Chevron")
               || EqualsI(s, "Notification chevron")
               || EqualsI(s, "Overflow chevron")
               || EqualsI(s, "Taskbar overflow")
               || EqualsI(s, "More icons")
               || EqualsI(s, "Desktop")
               || EqualsI(s, "Task list")
               || EqualsI(s, "通知区域")
               || EqualsI(s, "通知溢出")
               || EqualsI(s, "溢出通知区域")
               || EqualsI(s, "溢出")
               || EqualsI(s, "显示隐藏的图标")
               || EqualsI(s, "隐藏的图标")
               || EqualsI(s, "隐藏的通知图标")
               || EqualsI(s, "系统托盘")
               || EqualsI(s, "托盘")
               || EqualsI(s, "更多图标")
               || EqualsI(s, "其他图标");
    }

    private static bool EqualsI(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsIgnoreCase(string haystack, string needle) =>
        haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private static string NameFromUia(Native.POINT pt)
    {
        try
        {
            var el = AutomationElement.FromPoint(new System.Windows.Point(pt.X, pt.Y));
            return BestUiaName(el, pt, 0);
        }
        catch
        {
            return null;
        }
    }

    private static string BestUiaName(AutomationElement el, Native.POINT pt, int depth)
    {
        if (el == null || depth > 8) return null;

        string own = FirstUsable(SafeUiaString(() => el.Current.Name),
                                 SafeUiaString(() => el.Current.HelpText));
        if (own != null) return own;

        AutomationElement child;
        try { child = TreeWalker.RawViewWalker.GetFirstChild(el); }
        catch { return null; }

        while (child != null)
        {
            try
            {
                var r = child.Current.BoundingRectangle;
                if (!r.IsEmpty && r.Width > 0 && r.Height > 0
                    && pt.X >= r.Left && pt.X < r.Right && pt.Y >= r.Top && pt.Y < r.Bottom)
                {
                    string inner = BestUiaName(child, pt, depth + 1);
                    if (inner != null) return inner;
                }
            }
            catch
            {
                // Keep walking siblings if one child is stale.
            }

            try { child = TreeWalker.RawViewWalker.GetNextSibling(child); }
            catch { break; }
        }
        return null;
    }

    private static string FirstUsable(params string[] names)
    {
        foreach (string s in names)
        {
            if (UsableSwitcherName(s)) return s;
        }
        return null;
    }

    private static string SafeUiaString(Func<string> read)
    {
        try { return read(); }
        catch { return null; }
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
                if (UsableSwitcherName(t))
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

    private static bool IsExplorerTrayFlyout(IntPtr hwnd)
    {
        if (!ExplorerFlyoutClasses.Contains(ClassName(hwnd))) return false;
        if (!IsSameProcessAsTray(hwnd)) return false;
        if (!Native.GetWindowRect(hwnd, out Native.RECT wr)) return false;
        int width = wr.Right - wr.Left;
        int height = wr.Bottom - wr.Top;
        // The desktop and ordinary Explorer windows overlap the taskbar; the overflow is a compact popup.
        if (width <= 0 || height <= 0 || width > 800 || height > 800) return false;
        return IsNearTaskbar(wr, 80);
    }

    private static bool IsSameProcessAsTray(IntPtr hwnd)
    {
        IntPtr tray = Native.FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero) return false;
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        Native.GetWindowThreadProcessId(tray, out uint trayPid);
        return pid != 0 && pid == trayPid;
    }

    private static bool IsNearTaskbar(Native.RECT window, int pad)
    {
        if (NearAppBarOrRect(Native.FindWindow("Shell_TrayWnd", null), window, pad)) return true;
        IntPtr secondary = IntPtr.Zero;
        while ((secondary = Native.FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
        {
            if (Native.GetWindowRect(secondary, out Native.RECT rc) && IntersectsInflated(window, rc, pad))
                return true;
        }
        return false;
    }

    private static bool NearAppBarOrRect(IntPtr tray, Native.RECT window, int pad)
    {
        if (tray == IntPtr.Zero) return false;
        var data = new Native.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf(typeof(Native.APPBARDATA)),
            hWnd = tray,
        };
        if (Native.SHAppBarMessage(Native.ABM_GETTASKBARPOS, ref data) != IntPtr.Zero)
            return IntersectsInflated(window, data.rc, pad);
        return Native.GetWindowRect(tray, out Native.RECT rc) && IntersectsInflated(window, rc, pad);
    }

    private static bool IntersectsInflated(Native.RECT a, Native.RECT b, int pad) =>
        a.Left < b.Right + pad && a.Right > b.Left - pad
        && a.Top < b.Bottom + pad && a.Bottom > b.Top - pad;

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
