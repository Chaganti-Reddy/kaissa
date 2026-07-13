using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Optional Windows close-to-tray. Convenience only (allowed by the free-forever rule). When
// KaissaSettings.CloseToTray is on, clicking the window's close button hides it to a system-tray icon
// instead of quitting (minimize stays a normal minimize - deliberate); clicking the icon (or its
// Restore/Quit menu) brings the app back or really exits. Implemented with Shell_NotifyIcon and a
// subclassed window procedure, the same native-interop style as WindowChrome. Entirely gated behind
// the setting and wrapped in try/catch, so a normal launch never runs any of this native code and any
// interop failure falls back silently to ordinary window behaviour. Windows standalone only.
public sealed class TrayIcon : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.platform != RuntimePlatform.WindowsPlayer) return;
        if (!KaissaSettings.CloseToTray) return; // opt-in only
        var go = new GameObject("TrayIcon");
        DontDestroyOnLoad(go);
        go.AddComponent<TrayIcon>();
    }

    // ---- Win32 ----
    private const int GWLP_WNDPROC = -4;
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 1;
    private const int WM_CLOSE = 0x0010;
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_CLOSE = 0xF060;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_COMMAND = 0x0111;
    private const int SW_HIDE = 0, SW_SHOW = 5, SW_RESTORE = 9;
    private const uint NIM_ADD = 0, NIM_DELETE = 2;
    private const uint NIF_MESSAGE = 0x01, NIF_ICON = 0x02, NIF_TIP = 0x04;
    private const uint MF_STRING = 0x0000;
    private const uint TPM_RETURNCMD = 0x0100, TPM_RIGHTBUTTON = 0x0002;
    private const int IDM_RESTORE = 1, IDM_QUIT = 2;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }

    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int idx, WndProcDelegate newProc);
    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")] private static extern IntPtr CallWindowProc(IntPtr prev, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr_Native(IntPtr hWnd, int idx, IntPtr newProc);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int idx);
    [DllImport("user32.dll")] private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr iconName);
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")] private static extern bool Shell_NotifyIcon(uint msg, ref NOTIFYICONDATA data);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(IntPtr menu, uint flags, int id, string item);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] private static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hWnd, IntPtr rect);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);

    private static WndProcDelegate _proc; // kept alive so the GC never collects the installed proc
    private IntPtr _hwnd, _prevProc;
    private NOTIFYICONDATA _nid;
    private bool _installed, _hidden, _quitting;

    private void Start()
    {
        try
        {
            _hwnd = GetActiveWindow();
            if (_hwnd == IntPtr.Zero) return;

            IntPtr icon = GetClassLongPtr(_hwnd, -14 /*GCLP_HICON*/);
            if (icon == IntPtr.Zero) icon = LoadIcon(IntPtr.Zero, new IntPtr(32512) /*IDI_APPLICATION*/);

            _nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = icon,
                szTip = "Kaissa",
            };

            _proc = HookProc;
            _prevProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _proc);
            _installed = _prevProc != IntPtr.Zero;
        }
        catch (Exception e) { Debug.LogWarning($"TrayIcon disabled: {e.Message}"); _installed = false; }
    }

    private IntPtr HookProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            // Closing (X button or SC_CLOSE) hides to tray instead of quitting - unless we are really
            // quitting (from the tray Quit menu), in which case the close passes through normally.
            bool isClose = msg == WM_CLOSE || (msg == WM_SYSCOMMAND && (wParam.ToInt64() & 0xFFF0) == SC_CLOSE);
            if (isClose && !_quitting)
            {
                HideToTray();
                return IntPtr.Zero;
            }
            if (msg == WM_TRAYICON)
            {
                int ev = lParam.ToInt32();
                if (ev == WM_LBUTTONUP || ev == WM_LBUTTONDBLCLK) RestoreFromTray();
                else if (ev == WM_RBUTTONUP) ShowMenu();
            }
            else if (msg == WM_COMMAND)
            {
                int id = wParam.ToInt32() & 0xFFFF;
                if (id == IDM_RESTORE) RestoreFromTray();
                else if (id == IDM_QUIT) { _quitting = true; RemoveTray(); Application.Quit(); }
            }
        }
        catch (Exception e) { Debug.LogWarning($"TrayIcon proc: {e.Message}"); }
        return CallWindowProc(_prevProc, hWnd, msg, wParam, lParam);
    }

    private void HideToTray()
    {
        if (_hidden) return;
        Shell_NotifyIcon(NIM_ADD, ref _nid);
        ShowWindow(_hwnd, SW_HIDE);
        _hidden = true;
    }

    private void RestoreFromTray()
    {
        RemoveTray();
        ShowWindow(_hwnd, SW_RESTORE);
        ShowWindow(_hwnd, SW_SHOW);
        SetForegroundWindow(_hwnd);
        _hidden = false;
    }

    private void RemoveTray()
    {
        if (_hidden) Shell_NotifyIcon(NIM_DELETE, ref _nid);
    }

    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        AppendMenu(menu, MF_STRING, IDM_RESTORE, "Restore");
        AppendMenu(menu, MF_STRING, IDM_QUIT, "Quit");
        GetCursorPos(out var p);
        SetForegroundWindow(_hwnd);
        TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, p.X, p.Y, 0, _hwnd, IntPtr.Zero);
        DestroyMenu(menu);
    }

    private void OnApplicationQuit()
    {
        try
        {
            RemoveTray();
            if (_installed && _prevProc != IntPtr.Zero)
                SetWindowLongPtr_Native(_hwnd, GWLP_WNDPROC, _prevProc); // restore the original proc
        }
        catch { }
    }
}
