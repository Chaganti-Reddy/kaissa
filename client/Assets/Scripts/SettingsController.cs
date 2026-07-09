using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Settings, redesigned in UI Toolkit: a list of setting rows, each a label + a value button that
// cycles the option and rebuilds. Esc returns to the menu.
public sealed class SettingsController : MonoBehaviour
{
    private VisualElement _list;
    private string _resetLabel = "Reset progress";

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }
        var doc = gameObject.AddComponent<UIDocument>();
        doc.panelSettings = Resources.Load<PanelSettings>("KaissaPanel");
        StartCoroutine(Build(doc));
    }

    private IEnumerator Build(UIDocument doc)
    {
        yield return null;
        var root = doc.rootVisualElement;
        root.Clear();
        root.style.flexDirection = FlexDirection.Row; root.style.flexGrow = 1; root.style.backgroundColor = UiKit.Bg;
        root.Add(UiKit.NavRail("Settings"));

        var main = new VisualElement();
        main.style.flexGrow = 1; UiKit.Pad(main, 26, 34, 34, 34);
        main.Add(UiKit.Text_("Settings", 26, UiKit.Text, bold: true));

        _list = new VisualElement();
        _list.style.marginTop = 16; _list.style.maxWidth = 560;
        main.Add(_list);
        root.Add(main);
        Refresh();
    }

    private void Refresh()
    {
        _list.Clear();
        var speeds = new[] { "Fast", "Normal", "Slow" };
        Row("Sound", KaissaSettings.Sound ? "On" : "Off", () => KaissaSettings.Sound = !KaissaSettings.Sound);
        Row("Move by", KaissaSettings.DragToMove ? "Drag or click" : "Click only", () => KaissaSettings.DragToMove = !KaissaSettings.DragToMove);
        Row("Move hints", KaissaSettings.MoveHints ? "On" : "Off (train recall)", () => KaissaSettings.MoveHints = !KaissaSettings.MoveHints);
        Row("Auto-queen", KaissaSettings.AutoQueen ? "On" : "Off", () => KaissaSettings.AutoQueen = !KaissaSettings.AutoQueen);
        Row("Bot speed", speeds[Mathf.Clamp(KaissaSettings.BotSpeed, 0, 2)], () => KaissaSettings.BotSpeed = (KaissaSettings.BotSpeed + 1) % 3);
        Row("Display", KaissaSettings.Fullscreen ? "Fullscreen" : "Maximized", () => { KaissaSettings.Fullscreen = !KaissaSettings.Fullscreen; WindowMode.Apply(); });
        Row("Board", KaissaSettings.BoardView == 1 ? "3D" : "2D", () => KaissaSettings.BoardView = KaissaSettings.BoardView == 1 ? 0 : 1);
        Row("Eval bar (Play)", KaissaSettings.EvalBar ? "On" : "Off", () => KaissaSettings.EvalBar = !KaissaSettings.EvalBar);
        Row("Flip board", KaissaSettings.Flip ? "On" : "Off", () => KaissaSettings.Flip = !KaissaSettings.Flip);
        Row("Board theme", Board3D.Themes[Mathf.Clamp(KaissaSettings.BoardTheme, 0, Board3D.Themes.Length - 1)].Name,
            () => KaissaSettings.BoardTheme = (KaissaSettings.BoardTheme + 1) % Board3D.Themes.Length);
        Row("Pieces", KaissaSettings.UseModels ? "Modeled" : "Simple", () => KaissaSettings.UseModels = !KaissaSettings.UseModels);
        Row("Coordinates", KaissaSettings.Coordinates ? "On" : "Off", () => KaissaSettings.Coordinates = !KaissaSettings.Coordinates);
        Row(_resetLabel, "Reset", () => { KaissaProgress.Clear(); _resetLabel = "Progress reset"; });
    }

    private void Row(string label, string value, Action onToggle)
    {
        var l = UiKit.Text_(label, 15, UiKit.Text, bold: true);
        l.style.flexGrow = 1;
        var btn = UiKit.Ghost(value, () => { onToggle(); Refresh(); }, 13);
        btn.style.minWidth = 160;
        var row = UiKit.Row(l, btn);
        row.style.justifyContent = Justify.SpaceBetween;
        UiKit.Pad(row, 8, 8, 8, 8);
        row.style.borderBottomWidth = 1; row.style.borderBottomColor = UiKit.Line;
        _list.Add(row);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneManager.LoadScene("Menu");
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
