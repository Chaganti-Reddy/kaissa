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
// puzzle / Puzzle Blitz / play summaries, and the per-pattern mastery map. Read-only, no engine, no board.
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

        var scroll = UiKit.Scroll(); scroll.style.flexGrow = 1; _scroll = scroll;
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
        BuildRecentGames(main);
        BuildInsights(main);
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

        // Mid-scroll: the move-quality / phase / tactics insight cards (two depths to be sure to frame them).
        _scroll.scrollOffset = new Vector2(0, 700f);
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "stats_insights1.png"));
        yield return new WaitForSeconds(0.3f);
        _scroll.scrollOffset = new Vector2(0, 1150f);
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "stats_insights2.png"));
        yield return new WaitForSeconds(0.4f);

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
        p.strokeColor = new Color(1, 1, 1, 0.08f); p.lineWidth = 1;
        p.BeginPath(); p.MoveTo(new Vector2(0, rect.height - padY)); p.LineTo(new Vector2(rect.width, rect.height - padY)); p.Stroke();
        p.strokeColor = UiKit.GreenHi; p.lineWidth = 2.5f; p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
        p.BeginPath();
        p.MoveTo(new Vector2(PlotX(0), PlotY(h[0])));
        for (int i = 1; i < h.Count; i++) p.LineTo(new Vector2(PlotX(i), PlotY(h[i])));
        p.Stroke();
        p.fillColor = UiKit.Gold;
        p.BeginPath(); p.Arc(new Vector2(PlotX(h.Count - 1), PlotY(h[^1])), 4f, 0f, 360f); p.Fill();
    }

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

    private void BuildRecentGames(VisualElement main)
    {
        if (KaissaGameLog.Count == 0) return;
        var (card, body) = Card("Recent games");

        int w = KaissaGameLog.Wins, d = KaissaGameLog.Draws, l = KaissaGameLog.Losses;
        var wld = UiKit.Row(); wld.style.marginBottom = 8;
        wld.Add(Pill($"{w}W", UiKit.GreenHi));
        wld.Add(Pill($"{d}D", UiKit.Dim));
        wld.Add(Pill($"{l}L", UiKit.Danger));
        body.Add(wld);

        var recent = KaissaGameLog.Recent(12);
        var chart = new VisualElement { name = "recentchart" };
        chart.style.height = 70; chart.style.flexDirection = FlexDirection.Row; chart.style.alignItems = Align.FlexEnd;
        foreach (var acc in recent)
        {
            var col = new VisualElement();
            col.style.flexGrow = 1; col.style.marginRight = 3;
            col.style.height = new Length((float)Math.Max(4, acc) , LengthUnit.Percent);
            col.style.backgroundColor = acc >= 80 ? UiKit.GreenHi : acc >= 60 ? UiKit.Green : acc >= 40 ? UiKit.Gold : UiKit.Danger;
            UiKit.Radius(col, 3);
            chart.Add(col);
        }
        body.Add(chart);
        body.Add(UiKit.Text_($"last {recent.Count} games - avg {KaissaGameLog.Average:0}% accuracy", 11, UiKit.Mute));
        main.Add(card);
    }

    private void BuildInsights(VisualElement main)
    {
        var mix = KaissaGameLog.QualityMix;
        int total = mix.Sum();
        if (total == 0) return;

        var (card, body) = Card("Move quality");
        var colors = new[] { UiKit.GreenHi, UiKit.Green, UiKit.GreenDeep, UiKit.Gold, UiKit.Hex(0xe0, 0x82, 0x3a), UiKit.Danger };
        for (int i = 0; i < 6; i++)
        {
            var r = UiKit.Row(); r.style.alignItems = Align.Center; r.style.marginBottom = 5;
            var name = UiKit.Text_(KaissaGameLog.QualityNames[i], 13, UiKit.Dim); name.style.width = 90;
            r.Add(name);
            var track = new VisualElement(); track.style.flexGrow = 1; track.style.height = 10; track.style.marginRight = 8;
            track.style.backgroundColor = UiKit.Panel3; UiKit.Radius(track, 5);
            var fill = new VisualElement(); fill.style.height = 10; UiKit.Radius(fill, 5);
            fill.style.width = new Length(100f * mix[i] / total, LengthUnit.Percent);
            fill.style.backgroundColor = colors[i];
            track.Add(fill); r.Add(track);
            r.Add(UiKit.Text_($"{mix[i]}", 13, UiKit.Text, bold: true));
            body.Add(r);
        }
        main.Add(card);

        double? o = KaissaGameLog.PhaseOpen, m = KaissaGameLog.PhaseMid, e = KaissaGameLog.PhaseEnd;
        if (o.HasValue || m.HasValue || e.HasValue)
        {
            var (pc, pb) = Card("Accuracy by phase");
            StatLine(pb, "Opening", o.HasValue ? $"{o:0}%" : "-");
            StatLine(pb, "Middlegame", m.HasValue ? $"{m:0}%" : "-");
            StatLine(pb, "Endgame", e.HasValue ? $"{e:0}%" : "-");
            main.Add(pc);
        }

        // Tactics found vs missed - the pattern-recognition scoreboard: green bar = share you took,
        // red remainder = share you let slip, with the raw found/total on the right.
        var tf = KaissaGameLog.TacticsFound; var tm = KaissaGameLog.TacticsMissed;
        if (tf.Sum() + tm.Sum() > 0)
        {
            var (tc, tb) = Card("Tactics found vs missed");
            for (int i = 0; i < 4; i++)
            {
                int f = tf[i], miss = tm[i], tot = f + miss;
                var r = UiKit.Row(); r.style.alignItems = Align.Center; r.style.marginBottom = 5;
                var name = UiKit.Text_(KaissaGameLog.TacticNames[i], 13, UiKit.Dim); name.style.width = 90;
                r.Add(name);
                var track = new VisualElement(); track.style.flexGrow = 1; track.style.height = 10; track.style.marginRight = 8;
                track.style.backgroundColor = tot > 0 ? new Color(0.60f, 0.25f, 0.25f, 0.55f) : UiKit.Panel3;
                UiKit.Radius(track, 5);
                var fill = new VisualElement(); fill.style.height = 10; UiKit.Radius(fill, 5);
                fill.style.width = new Length(tot > 0 ? 100f * f / tot : 0f, LengthUnit.Percent);
                fill.style.backgroundColor = UiKit.GreenHi;
                track.Add(fill); r.Add(track);
                r.Add(UiKit.Text_($"{f}/{tot}", 13, UiKit.Text, bold: true));
                tb.Add(r);
            }
            main.Add(tc);
        }
    }

    private static VisualElement Pill(string text, Color color)
    {
        var p = UiKit.Text_(text, 13, UiKit.Text, bold: true);
        p.style.backgroundColor = color; UiKit.Pad(p, 4, 10, 4, 10); UiKit.Radius(p, 12); p.style.marginRight = 8;
        return p;
    }

    private static void StatLine(VisualElement body, string label, string value)
    {
        var r = UiKit.Row(); r.style.justifyContent = Justify.SpaceBetween; r.style.marginBottom = 5;
        r.Add(UiKit.Text_(label, 13, UiKit.Dim));
        r.Add(UiKit.Text_(value, 13, UiKit.Text, bold: true));
        body.Add(r);
    }

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
                SceneTransition.Go("SampleScene");
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
            SceneTransition.Go("Menu");
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
