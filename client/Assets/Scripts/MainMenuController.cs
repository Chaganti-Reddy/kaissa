using System.Collections;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// The home screen, rebuilt in UI Toolkit: a chess.com-style left nav rail + a card grid. Buttons load
// the mode scenes. Built in code via UiKit so the look stays consistent without a stylesheet asset.
public sealed class MainMenuController : MonoBehaviour
{
    private static bool _appliedWindowMode;
    private VisualElement _root;

    private void Start()
    {
        if (!_appliedWindowMode) { WindowMode.Apply(); _appliedWindowMode = true; }
        EnsureEventSystem();

        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }

        var doc = gameObject.AddComponent<UIDocument>();
        doc.panelSettings = Resources.Load<PanelSettings>("KaissaPanel");
        StartCoroutine(Build(doc));
    }

    private IEnumerator Build(UIDocument doc)
    {
        yield return null; // let the document create its root
        _root = doc.rootVisualElement;
        _root.Clear();
        _root.style.flexDirection = FlexDirection.Row;
        _root.style.flexGrow = 1;
        _root.style.backgroundColor = UiKit.Bg;

        _root.Add(BuildRail());
        _root.Add(BuildMain());

        if (!KaissaSettings.Onboarded)
            _root.Add(BuildWelcome());
    }

    // ---- left nav rail ----
    private VisualElement BuildRail()
    {
        var rail = new VisualElement();
        rail.style.width = 200;
        rail.style.backgroundColor = UiKit.Rail;
        rail.style.borderRightWidth = 1;
        rail.style.borderRightColor = UiKit.Line;
        UiKit.Pad(rail, 14, 10, 14, 10);

        var mark = UiKit.Text_("♞  Kaissa", 22, UiKit.GreenHi, bold: true);
        UiKit.Pad(mark, 6, 8, 16, 8);
        rail.Add(mark);

        rail.Add(Nav("Home", null, active: true));
        rail.Add(Nav("Play", "Play"));
        rail.Add(GroupLabel("Train"));
        rail.Add(Nav("Puzzles", "SampleScene"));
        rail.Add(Nav("Puzzle Blitz", "Rush"));
        rail.Add(Nav("Openings", "Opening"));
        rail.Add(Nav("Learn", "Library"));
        rail.Add(Nav("Endgames", "Endgame"));
        rail.Add(GroupLabel("Tools"));
        rail.Add(Nav("Board Vision", "Vision"));
        rail.Add(Nav("Coordinates", "Coordinate"));
        rail.Add(Nav("Stats", "Stats"));

        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        rail.Add(spacer);

        rail.Add(Nav("Calibrate", "Calibrate"));
        rail.Add(Nav("Settings", "Settings"));
        return rail;
    }

    private VisualElement GroupLabel(string text)
    {
        var l = UiKit.Text_(text.ToUpperInvariant(), 11, UiKit.Mute, bold: true);
        l.style.letterSpacing = 1.2f;
        UiKit.Pad(l, 14, 10, 5, 10);
        return l;
    }

    private VisualElement Nav(string label, string scene, bool active = false)
    {
        var item = UiKit.Row(UiKit.Text_(label, 15, active ? UiKit.Text : UiKit.Dim, bold: true));
        UiKit.Pad(item, 10); UiKit.Radius(item, 6);
        item.style.marginBottom = 1;
        var idle = active ? UiKit.Hex(0x3d, 0x3a, 0x36) : new Color(0, 0, 0, 0);
        item.style.backgroundColor = idle;
        item.RegisterCallback<MouseEnterEvent>(_ => { if (!active) item.style.backgroundColor = UiKit.Panel2; });
        item.RegisterCallback<MouseLeaveEvent>(_ => item.style.backgroundColor = idle);
        if (scene != null)
            item.RegisterCallback<ClickEvent>(_ => SceneManager.LoadScene(scene));
        return item;
    }

    // ---- main content ----
    private VisualElement BuildMain()
    {
        var main = new VisualElement();
        main.style.flexGrow = 1;
        UiKit.Pad(main, 26, 34, 40, 34);

        var top = UiKit.Row();
        top.style.justifyContent = Justify.SpaceBetween;
        top.style.marginBottom = 22;
        top.Add(UiKit.Text_("Welcome back", 26, UiKit.Text, bold: true));
        top.Add(BuildChips());
        main.Add(top);

        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.Add(Card("Play vs the bot", "An engine capped to your level. Your mistakes come back as puzzles.", "Play", hero: true));
        grid.Add(Card("Daily puzzle", "One position a day. Solve it to keep your streak.", "SampleScene", daily: true));
        grid.Add(Card("Puzzles", "Adaptive, spaced practice across 15 patterns.", "SampleScene"));
        grid.Add(Card("Puzzle Blitz", "Solve as many as you can before three misses.", "Rush"));
        grid.Add(Card("Openings", "Drill your repertoire - recall your moves, spaced.", "Opening"));
        grid.Add(Card("Learn patterns", "Browse each motif with an example, then drill it.", "Library"));
        main.Add(grid);
        return main;
    }

    private VisualElement BuildChips()
    {
        var chips = UiKit.Row();
        try
        {
            var trainer = KaissaTrainer.CreateDefault(KaissaProgress.Load());
            var stats = trainer.GetStats();
            if (stats.TotalAttempts > 0)
            {
                int days = KaissaStreak.CurrentDays();
                if (days > 0) chips.Add(UiKit.Chip($"Streak {days}d", UiKit.Green));
                chips.Add(UiKit.Chip($"{trainer.DueCount()} due for review", UiKit.Gold));
            }
        }
        catch { /* new player - no chips */ }
        return chips;
    }

    private VisualElement Card(string title, string desc, string scene, bool hero = false, bool daily = false)
    {
        var t = UiKit.Text_(title, hero ? 22 : 17, UiKit.Text, bold: true);
        var d = UiKit.Text_(desc, 13, UiKit.Dim);
        d.style.marginTop = 8;
        var c = UiKit.Col(t, d);
        c.style.backgroundColor = hero ? UiKit.Hex(0x35, 0x4a, 0x2f) : UiKit.Panel;
        c.style.borderTopWidth = c.style.borderBottomWidth = c.style.borderLeftWidth = c.style.borderRightWidth = 1;
        c.style.borderTopColor = c.style.borderBottomColor = c.style.borderLeftColor = c.style.borderRightColor =
            hero ? UiKit.Hex(0x4c, 0x6b, 0x3f) : UiKit.Line;
        UiKit.Pad(c, 18); UiKit.Radius(c, 12);
        c.style.marginRight = 14; c.style.marginBottom = 14;
        c.style.flexBasis = Length.Percent(hero ? 64 : 30);
        c.style.minWidth = 220;
        if (hero)
        {
            var btn = UiKit.Primary("Play", () => Go(scene, daily));
            btn.style.marginTop = 12;
            btn.style.alignSelf = Align.FlexStart;
            c.Add(btn);
        }
        c.RegisterCallback<ClickEvent>(_ => Go(scene, daily));
        return c;
    }

    private static void Go(string scene, bool daily)
    {
        if (daily) DailyRoute.Active = true;
        SceneManager.LoadScene(scene);
    }

    // ---- first-run welcome overlay ----
    private VisualElement BuildWelcome()
    {
        var dim = new VisualElement();
        dim.style.position = Position.Absolute;
        dim.style.left = 0; dim.style.top = 0; dim.style.right = 0; dim.style.bottom = 0;
        dim.style.backgroundColor = new Color(0, 0, 0, 0.72f);
        dim.style.alignItems = Align.Center;
        dim.style.justifyContent = Justify.Center;

        var panel = new VisualElement();
        panel.style.width = 520;
        panel.style.backgroundColor = UiKit.Panel;
        UiKit.Pad(panel, 30); UiKit.Radius(panel, 14);
        panel.style.alignItems = Align.Center;

        panel.Add(UiKit.Text_("Welcome to Kaissa", 30, UiKit.Text, bold: true));
        var msg = UiKit.Text_("Find your level so the puzzles and the bot match you. It takes about a minute.", 16, UiKit.Dim);
        msg.style.unityTextAlign = TextAnchor.MiddleCenter; msg.style.marginTop = 12; msg.style.marginBottom = 22;
        panel.Add(msg);

        var find = UiKit.Primary("Find my level", () => { KaissaSettings.Onboarded = true; SceneManager.LoadScene("Calibrate"); }, 16);
        find.style.marginBottom = 10; find.style.width = 300;
        var skip = UiKit.Ghost("Skip for now", () => { KaissaSettings.Onboarded = true; _root.Remove(dim); });
        skip.style.width = 300;
        panel.Add(find); panel.Add(skip);

        dim.Add(panel);
        return dim;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Quit();
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
            return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
