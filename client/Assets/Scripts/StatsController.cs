using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Insights / progress dashboard: headline stat tiles, a rating-over-time chart, tier + XP progression,
// puzzle / Puzzle Blitz / play summaries, and the per-pattern mastery map - the player's pattern
// library made visible, which is the point of the whole app. Read-only, no engine, no board.
public sealed class StatsController : MonoBehaviour
{
    private IReadOnlyList<double> _ratingHistory = Array.Empty<double>();
    private ScrollView _scroll;

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
        root.Add(UiKit.NavRail("Stats"));

        var scroll = new ScrollView(); scroll.style.flexGrow = 1; _scroll = scroll;
        var main = scroll.contentContainer;
        UiKit.Pad(main, 26, 34, 34, 34);
        main.style.maxWidth = 980;
        main.Add(UiKit.Text_("Insights", 26, UiKit.Text, bold: true));

        KaissaTrainer trainer;
        try { trainer = KaissaTrainer.CreateDefault(KaissaProgress.Load()); }
        catch (Exception e) { main.Add(UiKit.Text_("Could not load progress: " + e.Message, 15, UiKit.Danger)); Debug.LogError(e); root.Add(scroll); yield break; }

        var stats = trainer.GetStats();
        _ratingHistory = stats.RatingHistory;
        var standing = PuzzleProgression.Standing(KaissaSettings.PuzzleXp);

        BuildTiles(main, stats, standing);
        BuildRatingTrend(main, stats);
        BuildProgression(main, standing);
        BuildSummaryRow(main, stats);
        BuildMastery(main, trainer, stats);

        root.Add(scroll);

        if (Environment.GetCommandLineArgs().Contains("-kaissa-statstest"))
            StartCoroutine(AutoDemo(root));
    }

    private IEnumerator AutoDemo(VisualElement root)
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);

        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "stats_warmup.png"));
        yield return new WaitForSeconds(1.0f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "stats_top.png"));
        yield return new WaitForSeconds(0.6f);

        // Scroll down to the mastery map.
        _scroll.scrollOffset = new Vector2(0, 10000f);
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "stats_mastery.png"));
        yield return new WaitForSeconds(0.6f);

        // Real click on the "Practice weakest" button (routes to Puzzles); do last.
        UiAutomation.Click(root.Q<Button>("practice"));
        yield return new WaitForSeconds(1.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "stats_practice.png"));
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

    // ---------------- tiles ----------------

    private void BuildTiles(VisualElement main, PlayerStats stats, PuzzleProgression.TierStanding standing)
    {
        var row = UiKit.Row(); row.style.marginTop = 16; row.style.flexWrap = Wrap.Wrap;
        string delta = stats.RatingHistory.Count > 0
            ? $"{(int)Math.Round(stats.Rating - stats.RatingHistory[0]):+0;-0}" : "";
        row.Add(Tile("Rating", $"{stats.Rating:0}", delta, UiKit.Text));
        row.Add(Tile("Puzzle accuracy", stats.TotalAttempts > 0 ? $"{stats.Accuracy:P0}" : "-", $"{stats.TotalCorrect}/{stats.TotalAttempts}", UiKit.GreenHi));
        row.Add(Tile("Day streak", $"{KaissaStreak.CurrentDays()}", "days in a row", UiKit.Gold));
        row.Add(Tile("Tier", standing.Name, standing.IsMax ? "max" : $"{standing.TotalXp:n0} XP", UiKit.Gold));
        main.Add(row);
    }

    private static VisualElement Tile(string label, string value, string sub, Color valueColor)
    {
        var t = Panel(); UiKit.Pad(t, 14, 18, 14, 18);
        t.style.minWidth = 190; t.style.marginRight = 12; t.style.marginBottom = 12; t.style.flexGrow = 1;
        t.Add(UiKit.Text_(label.ToUpperInvariant(), 11, UiKit.Mute, bold: true));
        var v = UiKit.Text_(value, 30, valueColor, bold: true); v.style.marginTop = 2;
        t.Add(v);
        if (!string.IsNullOrEmpty(sub)) t.Add(UiKit.Text_(sub, 12, UiKit.Dim));
        return t;
    }

    // ---------------- rating trend chart ----------------

    private void BuildRatingTrend(VisualElement main, PlayerStats stats)
    {
        var (card, body) = Card("Rating over time");
        if (stats.RatingHistory.Count < 2)
        {
            body.Add(UiKit.Text_("Not enough history yet - keep training and your rating curve will appear here.", 13, UiKit.Mute));
            main.Add(card);
            return;
        }
        var chart = new VisualElement();
        chart.style.height = 180; chart.style.marginTop = 6;
        chart.generateVisualContent = DrawTrend;
        body.Add(chart);

        double lo = stats.RatingHistory.Min(), hi = stats.RatingHistory.Max();
        var range = UiKit.Row();
        range.style.justifyContent = Justify.SpaceBetween; range.style.marginTop = 4;
        range.Add(UiKit.Text_($"low {lo:0}", 11, UiKit.Mute));
        range.Add(UiKit.Text_($"now {stats.Rating:0}", 11, UiKit.Dim, bold: true));
        range.Add(UiKit.Text_($"high {hi:0}", 11, UiKit.Mute));
        body.Add(range);
        main.Add(card);
    }

    private void DrawTrend(MeshGenerationContext ctx)
    {
        var h = _ratingHistory;
        if (h.Count < 2) return;
        var rect = ctx.visualElement.contentRect;
        if (rect.width <= 1 || rect.height <= 1) return;

        double lo = h.Min(), hi = h.Max();
        double span = Math.Max(1.0, hi - lo);
        float padY = 10f;
        float PlotY(double v) => rect.height - padY - (float)((v - lo) / span) * (rect.height - 2 * padY);
        float PlotX(int i) => (float)i / (h.Count - 1) * rect.width;

        var p = ctx.painter2D;
        // baseline
        p.strokeColor = new Color(1, 1, 1, 0.08f); p.lineWidth = 1;
        p.BeginPath(); p.MoveTo(new Vector2(0, rect.height - padY)); p.LineTo(new Vector2(rect.width, rect.height - padY)); p.Stroke();
        // rating line
        p.strokeColor = UiKit.GreenHi; p.lineWidth = 2.5f; p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
        p.BeginPath();
        p.MoveTo(new Vector2(PlotX(0), PlotY(h[0])));
        for (int i = 1; i < h.Count; i++) p.LineTo(new Vector2(PlotX(i), PlotY(h[i])));
        p.Stroke();
        // end dot
        p.fillColor = UiKit.Gold;
        p.BeginPath(); p.Arc(new Vector2(PlotX(h.Count - 1), PlotY(h[^1])), 4f, 0f, 360f); p.Fill();
    }

    // ---------------- progression ----------------

    private void BuildProgression(VisualElement main, PuzzleProgression.TierStanding standing)
    {
        var (card, body) = Card("Progression");
        body.Add(UiKit.Text_(standing.Name, 20, UiKit.Gold, bold: true));
        var track = new VisualElement();
        track.style.height = 12; track.style.marginTop = 8; track.style.marginBottom = 6;
        track.style.backgroundColor = UiKit.Panel3; UiKit.Radius(track, 6);
        var fill = new VisualElement();
        fill.style.height = 12; UiKit.Radius(fill, 6); fill.style.backgroundColor = UiKit.Gold;
        fill.style.width = new Length(standing.Fraction * 100f, LengthUnit.Percent);
        track.Add(fill);
        body.Add(track);
        string next = standing.IsMax
            ? $"{standing.TotalXp:n0} XP (max tier)"
            : $"{standing.XpIntoTier:n0} / {standing.XpForNext:n0} XP to {PuzzleProgression.Tiers[standing.Index + 1].Name}";
        body.Add(UiKit.Text_(next, 12, UiKit.Dim));
        main.Add(card);
    }

    // ---------------- summary row (puzzle / blitz / play) ----------------

    private void BuildSummaryRow(VisualElement main, PlayerStats stats)
    {
        var row = UiKit.Row(); row.style.flexWrap = Wrap.Wrap; row.style.marginTop = 4;

        var (pz, pzb) = Card("Puzzles");
        pz.style.flexGrow = 1; pz.style.minWidth = 260; pz.style.marginRight = 12;
        StatLine(pzb, "Solved", stats.TotalAttempts > 0 ? $"{stats.TotalCorrect}/{stats.TotalAttempts}  ({stats.Accuracy:P0})" : "-");
        StatLine(pzb, "Current streak", $"{stats.CurrentStreak}");
        StatLine(pzb, "Best streak", $"{Math.Max(stats.BestStreak, KaissaSettings.PuzzleBestStreak)}");
        StatLine(pzb, "Patterns seen", $"{stats.PatternsSeen}");
        row.Add(pz);

        var (blitz, blitzB) = Card("Puzzle Blitz best");
        blitz.style.flexGrow = 1; blitz.style.minWidth = 200; blitz.style.marginRight = 12;
        StatLine(blitzB, "3 minutes", $"{KaissaSettings.RushBest3}");
        StatLine(blitzB, "5 minutes", $"{KaissaSettings.RushBest5}");
        StatLine(blitzB, "Survival", $"{KaissaSettings.RushBestSurvival}");
        row.Add(blitz);

        var (play, playB) = Card("Play vs bot");
        play.style.flexGrow = 1; play.style.minWidth = 200;
        if (KaissaGameLog.Count > 0)
        {
            StatLine(playB, "Games", $"{KaissaGameLog.Count}");
            StatLine(playB, "Avg accuracy", $"{KaissaGameLog.Average:0.0}%");
        }
        else StatLine(playB, "Games", "none yet");
        row.Add(play);

        main.Add(row);
    }

    private static void StatLine(VisualElement body, string label, string value)
    {
        var r = UiKit.Row(); r.style.justifyContent = Justify.SpaceBetween; r.style.marginBottom = 5;
        r.Add(UiKit.Text_(label, 13, UiKit.Dim));
        r.Add(UiKit.Text_(value, 13, UiKit.Text, bold: true));
        body.Add(r);
    }

    // ---------------- mastery map ----------------

    private void BuildMastery(VisualElement main, KaissaTrainer trainer, PlayerStats stats)
    {
        var (card, body) = Card("Pattern mastery");
        var rows = trainer.Progress().ToList();
        // strongest first (mastered/most stable), so the library reads top-down
        rows.Sort((a, b) => PuzzleProgression.MasteryFor(b).CompareTo(PuzzleProgression.MasteryFor(a)));

        foreach (var row in rows)
            body.Add(MasteryRow(row));

        var seen = rows.Where(r => r.Seen).OrderBy(r => PuzzleProgression.MasteryFor(r)).ThenByDescending(r => r.Lapses).ToList();
        if (seen.Count > 0)
        {
            var weakest = seen[0];
            var btn = UiKit.Primary($"Practice weakest: {weakest.PatternName}", () =>
            {
                ThemeRoute.PatternId = weakest.PatternId;
                ThemeRoute.PatternName = weakest.PatternName;
                SceneManager.LoadScene("SampleScene");
            }, 15);
            btn.name = "practice";
            btn.style.marginTop = 14; btn.style.alignSelf = Align.FlexStart;
            body.Add(btn);
        }
        main.Add(card);
    }

    private static VisualElement MasteryRow(ProgressRow row)
    {
        var m = PuzzleProgression.MasteryFor(row);
        var wrap = new VisualElement(); wrap.style.marginBottom = 9;

        var head = UiKit.Row(); head.style.justifyContent = Justify.SpaceBetween;
        head.Add(UiKit.Text_(row.PatternName, 13, UiKit.Text, bold: true));
        var label = UiKit.Text_(PuzzleProgression.MasteryLabel(m), 12, MasteryColor(m), bold: true);
        head.Add(label);
        wrap.Add(head);

        var track = new VisualElement();
        track.style.height = 7; track.style.marginTop = 3; track.style.backgroundColor = UiKit.Panel3; UiKit.Radius(track, 4);
        var fill = new VisualElement();
        fill.style.height = 7; UiKit.Radius(fill, 4); fill.style.backgroundColor = MasteryColor(m);
        fill.style.width = new Length((int)m / 5f * 100f, LengthUnit.Percent);
        track.Add(fill);
        wrap.Add(track);

        if (row.Seen)
        {
            var sub = UiKit.Text_($"stability {row.StabilityDays:0}d   -   lapses {row.Lapses}", 11, UiKit.Mute);
            sub.style.marginTop = 2;
            wrap.Add(sub);
        }
        return wrap;
    }

    private static Color MasteryColor(PuzzleProgression.Mastery m) => m switch
    {
        PuzzleProgression.Mastery.Mastered => UiKit.Gold,
        PuzzleProgression.Mastery.Strong => UiKit.GreenHi,
        PuzzleProgression.Mastery.Proficient => UiKit.Green,
        PuzzleProgression.Mastery.Familiar => UiKit.Dim,
        PuzzleProgression.Mastery.Learning => UiKit.Mute,
        _ => UiKit.Mute,
    };

    // ---------------- helpers ----------------

    private static (VisualElement card, VisualElement body) Card(string title)
    {
        var card = Panel(); UiKit.Pad(card, 16, 18, 16, 18); card.style.marginTop = 14;
        card.Add(UiKit.Text_(title, 12, UiKit.Mute, bold: true));
        var body = new VisualElement(); body.style.marginTop = 8;
        card.Add(body);
        return (card, body);
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
