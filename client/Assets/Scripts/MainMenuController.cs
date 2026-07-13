using System;
using System.Collections;
using System.Linq;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Home dashboard: the shared nav rail plus a snapshot (rating, tier, streak, due) and a grid of mode
// cards that launch each screen. First run shows a welcome that offers calibration. Built in code via
// UiKit so the look matches every other page.
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
        yield return null;
        _root = doc.rootVisualElement;
        _root.Clear();
        _root.style.flexDirection = FlexDirection.Row;
        _root.style.flexGrow = 1;
        _root.style.backgroundColor = UiKit.Bg;

        _root.Add(UiKit.NavRail("Menu"));
        _root.Add(BuildMain());

        if (Environment.GetCommandLineArgs().Contains("-kaissa-hometest"))
            KaissaSettings.Onboarded = true;

        if (!KaissaSettings.Onboarded)
            _root.Add(BuildWelcome());

        if (Environment.GetCommandLineArgs().Contains("-kaissa-hometest"))
            StartCoroutine(AutoDemo());
    }

    // ---- main content ----
    private VisualElement BuildMain()
    {
        var scroll = new ScrollView(); scroll.style.flexGrow = 1;
        var main = scroll.contentContainer;
        UiKit.Pad(main, 26, 34, 40, 34);

        var top = UiKit.Row();
        top.style.justifyContent = Justify.SpaceBetween; top.style.marginBottom = 16;
        top.Add(UiKit.Text_("Welcome back", 26, UiKit.Text, bold: true));
        top.Add(BuildChips());
        main.Add(top);

        main.Add(BuildTiles());

        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row; grid.style.flexWrap = Wrap.Wrap; grid.style.marginTop = 6;
        grid.Add(Card("Play vs the bot", "An engine capped to your level. Your mistakes come back as puzzles.", "Play", hero: true));
        grid.Add(DailyCard());
        grid.Add(Card("Puzzles", "Adaptive, spaced practice across the pattern library.", "SampleScene"));
        grid.Add(Card("Puzzle Blitz", "Solve as many as you can before three misses.", "Rush"));
        grid.Add(Card("Openings", "Explore, learn, and drill your repertoire.", "Opening"));
        grid.Add(Card("Learn", "Guided lessons: a motif explained, then drilled.", "Library"));
        grid.Add(Card("Endgames", "Play instructive endings against the engine.", "Endgame"));
        grid.Add(Card("Analysis", "Any position, engine lines, arrows, load a FEN.", "Analysis"));
        grid.Add(Card("Board Vision", "Light or dark square, against the clock.", "Vision"));
        grid.Add(Card("Coordinates", "Find the square, against the clock.", "Coordinate"));
        main.Add(grid);
        return scroll;
    }

    private VisualElement BuildTiles()
    {
        var row = UiKit.Row(); row.style.flexWrap = Wrap.Wrap; row.style.marginBottom = 8;
        try
        {
            var trainer = KaissaTrainer.CreateDefault(KaissaProgress.Load());
            var stats = trainer.GetStats();
            var standing = PuzzleProgression.Standing(KaissaSettings.PuzzleXp);
            row.Add(Tile("Rating", $"{stats.Rating:0}", UiKit.Text));
            row.Add(Tile("Tier", standing.Name, UiKit.Gold));
            row.Add(Tile("Day streak", $"{KaissaStreak.CurrentDays()}", UiKit.GreenHi));
            row.Add(Tile("Due for review", $"{trainer.DueCount()}", UiKit.Gold));
        }
        catch { /* new player - skip tiles */ }
        return row;
    }

    private static VisualElement Tile(string label, string value, Color color)
    {
        var t = Panel(); UiKit.Pad(t, 12, 16, 12, 16);
        t.style.minWidth = 150; t.style.marginRight = 12; t.style.marginBottom = 12; t.style.flexGrow = 1;
        t.Add(UiKit.Text_(label.ToUpperInvariant(), 11, UiKit.Mute, bold: true));
        var v = UiKit.Text_(value, 26, color, bold: true); v.style.marginTop = 2;
        t.Add(v);
        return t;
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
        catch { }
        return chips;
    }

    private VisualElement DailyCard()
    {
        bool done = KaissaSettings.DailyDone == DateTime.Today.ToString("yyyy-MM-dd");
        var c = Card(done ? "Daily puzzle - solved" : "Daily puzzle",
            done ? "You solved today's. Come back tomorrow." : "One position a day. Solve it to keep your streak.",
            "SampleScene", daily: true);
        if (done)
        {
            var dot = new VisualElement();
            dot.style.position = Position.Absolute; dot.style.top = 14; dot.style.right = 14;
            dot.style.width = 10; dot.style.height = 10; UiKit.Radius(dot, 5); dot.style.backgroundColor = UiKit.Green;
            c.Add(dot);
        }
        return c;
    }

    private VisualElement Card(string title, string desc, string scene, bool hero = false, bool daily = false)
    {
        var t = UiKit.Text_(title, hero ? 22 : 17, UiKit.Text, bold: true);
        var d = UiKit.Text_(desc, 13, UiKit.Dim);
        d.style.whiteSpace = WhiteSpace.Normal; d.style.marginTop = 8;
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
            btn.style.marginTop = 12; btn.style.alignSelf = Align.FlexStart;
            c.Add(btn);
        }
        c.RegisterCallback<ClickEvent>(_ => Go(scene, daily));
        UiKit.Interactive(c, 1.01f);
        return c;
    }

    private static void Go(string scene, bool daily)
    {
        if (daily) DailyRoute.Active = true;
        SceneTransition.Go(scene);
    }

    private static VisualElement Panel()
    {
        var p = new VisualElement();
        p.style.backgroundColor = UiKit.Panel;
        p.style.borderTopWidth = p.style.borderBottomWidth = p.style.borderLeftWidth = p.style.borderRightWidth = 1;
        p.style.borderTopColor = p.style.borderBottomColor = p.style.borderLeftColor = p.style.borderRightColor = UiKit.Line;
        UiKit.Radius(p, 12);
        return p;
    }

    // ---- first-run welcome overlay ----
    private VisualElement BuildWelcome()
    {
        var dim = new VisualElement();
        dim.style.position = Position.Absolute;
        dim.style.left = 0; dim.style.top = 0; dim.style.right = 0; dim.style.bottom = 0;
        dim.style.backgroundColor = new Color(0, 0, 0, 0.72f);
        dim.style.alignItems = Align.Center; dim.style.justifyContent = Justify.Center;

        var panel = Panel();
        panel.style.width = 520; UiKit.Pad(panel, 30); panel.style.alignItems = Align.Center;
        panel.Add(UiKit.Text_("Welcome to Kaissa", 30, UiKit.Text, bold: true));
        var msg = UiKit.Text_("Find your level so the puzzles and the bot match you. It takes about a minute.", 16, UiKit.Dim);
        msg.style.unityTextAlign = TextAnchor.MiddleCenter; msg.style.marginTop = 12; msg.style.marginBottom = 22; msg.style.whiteSpace = WhiteSpace.Normal;
        panel.Add(msg);

        var find = UiKit.Primary("Find my level", () => { KaissaSettings.Onboarded = true; SceneTransition.Go("Calibrate"); }, 16);
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

    // ---- self-test ----
    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);

        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "home_warmup.png"));
        yield return new WaitForSeconds(1.0f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "home.png"));
        yield return new WaitForSeconds(0.6f);

        // Real click the Play hero button -> routes to Play (proves the launcher wiring).
        UiAutomation.Click(UiAutomation.FindButton(_root, "Play"));
        yield return new WaitForSeconds(1.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "home_play.png"));
        yield return new WaitForSeconds(0.4f);
        Application.Quit();
    }

    private static string ArgValue(string key)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == key) return args[i + 1];
        return null;
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
            return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
