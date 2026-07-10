using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Forces the Windows title bar to dark mode. Unity does not set this, so the standalone window shows
// a light title bar (and flips to a white inactive bar when it loses focus). We set the DWM dark-mode
// attribute on the game window and re-apply on focus changes so it stays dark. Windows-only; no-op
// elsewhere and in the editor.
public sealed class WindowChrome : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("WindowChrome");
        DontDestroyOnLoad(go);
        go.AddComponent<WindowChrome>();
    }

    private void Start() => Apply();

    // The inactive title bar is what reverts to white; re-applying on focus change keeps it dark.
    private void OnApplicationFocus(bool _) => Apply();

    private static void Apply()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            var hwnd = GetActiveWindow();
            if (hwnd == IntPtr.Zero)
                return;
            int on = 1;
            // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE (Windows 10 2004+). 19 was the pre-release id;
            // set both so older builds are covered - an unknown id is simply ignored.
            DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int));
            DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int));
        }
        catch { /* dwmapi missing or attribute unsupported: leave the default chrome */ }
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();
#endif
}
