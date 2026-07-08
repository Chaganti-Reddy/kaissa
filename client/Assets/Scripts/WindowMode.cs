using UnityEngine;

// Applies the player's window preference: a maximized window by default (has a title bar and can be
// un-maximized, so the player is never trapped), or borderless fullscreen. Native desktop resolution
// is used for both so the app fills the screen instead of the old fixed 1280x720.
public static class WindowMode
{
    public static void Apply()
    {
        var mode = KaissaSettings.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.MaximizedWindow;
        var res = Screen.currentResolution;
        Screen.SetResolution(res.width, res.height, mode);
    }
}
