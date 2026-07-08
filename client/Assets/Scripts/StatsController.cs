using System;
using System.Linq;
using System.Text;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Insights screen: loads saved progress and shows headline stats plus per-pattern mastery.
// Read-only; Esc returns to the menu.
public sealed class StatsController : MonoBehaviour
{
    private void Start()
    {
        var font = Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf") as Font
                   ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
        }

        KaissaTrainer trainer = null;
        string content;
        try
        {
            trainer = KaissaTrainer.CreateDefault(KaissaProgress.Load());
            content = BuildReport(trainer);
        }
        catch (Exception e)
        {
            content = "Error building stats:\n" + e.Message;
            Debug.LogError(e);
        }

        RenderText(font, content);
        if (trainer != null)
            AddPracticeButton(trainer);
    }

    // A "Practice" button targeting the weakest seen pattern (lowest stability) → themed drill.
    private static void AddPracticeButton(KaissaTrainer trainer)
    {
        var seen = trainer.Progress()
            .Where(r => r.Seen)
            .OrderBy(r => r.StabilityDays)
            .ToList();
        if (seen.Count == 0)
            return;
        var weakest = seen[0];

        var canvas = Hud.Canvas();
        Hud.Button(canvas, $"Practice: {weakest.PatternName}", new Vector2(0f, -300f),
            () =>
            {
                ThemeRoute.PatternId = weakest.PatternId;
                ThemeRoute.PatternName = weakest.PatternName;
                UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
            }, 380f);
    }

    private static string BuildReport(KaissaTrainer trainer)
    {
        var stats = trainer.GetStats();

        var sb = new StringBuilder();
        sb.AppendLine("Your Progress");
        sb.AppendLine();

        if (KaissaGameLog.Count > 0)
        {
            sb.AppendLine($"Games played   {KaissaGameLog.Count}   ·   avg accuracy {KaissaGameLog.Average:0.0}%");
            sb.AppendLine();
        }

        if (stats.TotalAttempts == 0)
        {
            sb.AppendLine("No progress yet.");
            sb.AppendLine("Train some puzzles first — progress saves automatically.");
            sb.AppendLine();
            sb.AppendLine("Esc - back to menu");
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
            foreach (var r in seen.Take(3))
                sb.AppendLine($"  {r.PatternName,-24} {r.StabilityDays,5:0}d");

            var weak = seen.AsEnumerable().Reverse().Take(3).ToList();
            if (weak.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Needs work");
                foreach (var r in weak)
                    sb.AppendLine($"  {r.PatternName,-24} {r.StabilityDays,5:0}d   lapses {r.Lapses}");
            }
        }
        if (unseen.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Not yet seen: " + string.Join(", ", unseen.Select(r => r.PatternName)));
        }

        sb.AppendLine();
        sb.AppendLine("Esc - back to menu");
        return sb.ToString();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
    }

    private static void RenderText(Font font, string content)
    {
        var canvasObj = new GameObject("HUD");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Hud.ConfigureScaler(canvasObj.AddComponent<CanvasScaler>());
        canvasObj.AddComponent<GraphicRaycaster>();

        // Dark panel so the screen region is clearly rendering.
        var panelObj = new GameObject("panel");
        panelObj.transform.SetParent(canvas.transform, false);
        var panel = panelObj.AddComponent<Image>();
        panel.color = new Color(0.10f, 0.11f, 0.15f, 0.95f);
        var prt = panel.rectTransform;
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(30f, 30f);
        prt.offsetMax = new Vector2(-30f, -30f);

        var textObj = new GameObject("stats");
        textObj.transform.SetParent(panel.transform, false);
        var text = textObj.AddComponent<Text>();
        text.font = font;
        text.fontSize = 22;
        text.alignment = TextAnchor.UpperLeft;
        text.color = Color.white;
        text.text = content;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(30f, 30f);
        rt.offsetMax = new Vector2(-30f, -30f);
    }
}
