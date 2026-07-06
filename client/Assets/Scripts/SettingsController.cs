using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Settings screen: sound, board flip, board theme, piece style, and reset progress. Rebuilds on
// each change so button labels stay current. Esc returns to the menu.
public sealed class SettingsController : MonoBehaviour
{
    private Transform _canvas;
    private string _resetLabel = "Reset progress";

    private void Start()
    {
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f); }
        Build();
    }

    private void Build()
    {
        if (_canvas != null)
            Destroy(_canvas.gameObject);
        _canvas = Hud.Canvas();

        Hud.Text(_canvas, "Settings", 44, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(800f, 60f));

        float y = 240f;
        Hud.Button(_canvas, $"Sound: {(KaissaSettings.Sound ? "On" : "Off")}", new Vector2(0f, y),
            () => { KaissaSettings.Sound = !KaissaSettings.Sound; Build(); }, 380f);
        y -= 66f;
        Hud.Button(_canvas, $"Move by: {(KaissaSettings.DragToMove ? "Drag or click" : "Click only")}", new Vector2(0f, y),
            () => { KaissaSettings.DragToMove = !KaissaSettings.DragToMove; Build(); }, 380f);
        y -= 66f;
        Hud.Button(_canvas, $"Auto-queen: {(KaissaSettings.AutoQueen ? "On" : "Off")}", new Vector2(0f, y),
            () => { KaissaSettings.AutoQueen = !KaissaSettings.AutoQueen; Build(); }, 380f);
        y -= 66f;
        Hud.Button(_canvas, $"Flip board: {(KaissaSettings.Flip ? "On" : "Off")}", new Vector2(0f, y),
            () => { KaissaSettings.Flip = !KaissaSettings.Flip; Build(); }, 380f);
        y -= 66f;
        var themeName = Board3D.Themes[Mathf.Clamp(KaissaSettings.BoardTheme, 0, Board3D.Themes.Length - 1)].Name;
        Hud.Button(_canvas, $"Board: {themeName}", new Vector2(0f, y),
            () => { KaissaSettings.BoardTheme = (KaissaSettings.BoardTheme + 1) % Board3D.Themes.Length; Build(); }, 380f);
        y -= 66f;
        Hud.Button(_canvas, $"Pieces: {(KaissaSettings.UseModels ? "Modeled" : "Simple")}", new Vector2(0f, y),
            () => { KaissaSettings.UseModels = !KaissaSettings.UseModels; Build(); }, 380f);
        y -= 66f;
        Hud.Button(_canvas, _resetLabel, new Vector2(0f, y),
            () => { KaissaProgress.Clear(); _resetLabel = "Progress reset ✓"; Build(); }, 380f);
        y -= 70f;
        Hud.Button(_canvas, "Back", new Vector2(0f, y), () => SceneManager.LoadScene("Menu"), 380f);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneManager.LoadScene("Menu");
    }
}
