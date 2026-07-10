using System;
using System.Collections;
using System.Linq;
using System.Text;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Progress / insights screen, redesigned in UI Toolkit. Loads saved progress and shows headline stats,
// per-pattern strengths, and a button to drill the weakest pattern. Read-only.
public sealed class StatsController : MonoBehaviour
{
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

        var main = new VisualElement();
        main.style.flexGrow = 1; UiKit.Pad(main, 26, 34, 34, 34);
        main.Add(UiKit.Text_("Your Progress", 26, UiKit.Text, bold: true));

        KaissaTrainer trainer = null;
        string content;
        try { trainer = KaissaTrainer.CreateDefault(KaissaProgress.Load()); content = BuildReport(trainer); }
        catch (Exception e) { content = "Error building stats:\n" + e.Message; Debug.LogError(e); }

        var panel = new VisualElement();
        panel.style.backgroundColor = UiKit.Panel;
        panel.style.borderTopWidth = panel.style.borderBottomWidth = panel.style.borderLeftWidth = panel.style.borderRightWidth = 1;
        panel.style.borderTopColor = panel.style.borderBottomColor = panel.style.borderLeftColor = panel.style.borderRightColor = UiKit.Line;
        UiKit.Radius(panel, 12); UiKit.Pad(panel, 20); panel.style.marginTop = 16; panel.style.maxWidth = 720;

        var text = UiKit.Text_(content, 15, UiKit.Dim);
        text.style.whiteSpace = WhiteSpace.Normal;
        panel.Add(text);
        main.Add(panel);

        if (trainer != null)
            AddPracticeButton(main, trainer);
        root.Add(main);
    }

    private static void AddPracticeButton(VisualElement main, KaissaTrainer trainer)
    {
        var seen = trainer.Progress().Where(r => r.Seen).OrderBy(r => r.StabilityDays).ToList();
        if (seen.Count == 0) return;
        var weakest = seen[0];
        var btn = UiKit.Primary($"Practice: {weakest.PatternName}", () =>
        {
            ThemeRoute.PatternId = weakest.PatternId;
            ThemeRoute.PatternName = weakest.PatternName;
            SceneManager.LoadScene("SampleScene");
        }, 15);
        btn.style.marginTop = 16; btn.style.alignSelf = Align.FlexStart;
        main.Add(btn);
    }

    private static string BuildReport(KaissaTrainer trainer)
    {
        var stats = trainer.GetStats();
        var sb = new StringBuilder();

        if (KaissaGameLog.Count > 0)
            sb.AppendLine($"Games played   {KaissaGameLog.Count}   -   avg accuracy {KaissaGameLog.Average:0.0}%").AppendLine();

        if (stats.TotalAttempts == 0)
        {
            sb.AppendLine("No puzzle progress yet. Train some puzzles - progress saves automatically.");
            return sb.ToString();
        }

        string trend = "";
        if (stats.RatingHistory.Count > 0)
        {
            int delta = (int)Math.Round(stats.Rating - stats.RatingHistory[0]);
            trend = $"   ({delta:+0;-0} since start)";
        }
        sb.AppendLine($"Rating   {stats.Rating:0}{trend}");
        sb.AppendLine($"Solved   {stats.TotalCorrect}/{stats.TotalAttempts}   ({stats.Accuracy:P0})");
        sb.AppendLine($"Streak   {stats.CurrentStreak}   (best {stats.BestStreak})");
        sb.AppendLine($"Patterns seen   {stats.PatternsSeen}");
        sb.AppendLine();

        var rows = trainer.Progress().ToList();
        var seen = rows.Where(r => r.Seen).OrderByDescending(r => r.StabilityDays).ToList();
        var unseen = rows.Where(r => !r.Seen).ToList();
        if (seen.Count > 0)
        {
            sb.AppendLine("Strongest");
            foreach (var r in seen.Take(3)) sb.AppendLine($"  {r.PatternName,-24} {r.StabilityDays,5:0}d");
            var weak = seen.AsEnumerable().Reverse().Take(3).ToList();
            sb.AppendLine();
            sb.AppendLine("Needs work");
            foreach (var r in weak) sb.AppendLine($"  {r.PatternName,-24} {r.StabilityDays,5:0}d   lapses {r.Lapses}");
        }
        if (unseen.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Not yet seen: " + string.Join(", ", unseen.Select(r => r.PatternName)));
        }
        return sb.ToString();
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
