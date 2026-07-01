using System;
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

        string content;
        try
        {
            content = BuildReport();
        }
        catch (Exception e)
        {
            content = "Error building stats:\n" + e.Message;
            Debug.LogError(e);
        }

        RenderText(font, content);
    }

    private static string BuildReport()
    {
        var saved = KaissaProgress.Load();
        Debug.Log($"Stats: save file {(saved == null ? "MISSING" : $"found, {saved.Length} chars")} at {Application.persistentDataPath}");

        var trainer = KaissaTrainer.CreateDefault(saved);
        var stats = trainer.GetStats();

        var sb = new StringBuilder();
        sb.AppendLine("Your Progress");
        sb.AppendLine();

        if (stats.TotalAttempts == 0)
        {
            sb.AppendLine("No progress yet.");
            sb.AppendLine("Train some puzzles first — progress saves automatically.");
            sb.AppendLine();
            sb.AppendLine("Esc - back to menu");
            return sb.ToString();
        }

        sb.AppendLine($"Rating: {stats.Rating:0}");
        sb.AppendLine($"Solved: {stats.TotalCorrect} / {stats.TotalAttempts}  ({stats.Accuracy:P0})");
        sb.AppendLine($"Streak: {stats.CurrentStreak}   (best {stats.BestStreak})");
        sb.AppendLine($"Patterns seen: {stats.PatternsSeen}");
        sb.AppendLine();
        sb.AppendLine("Mastery");
        foreach (var row in trainer.Progress())
        {
            sb.AppendLine(row.Seen
                ? $"  {row.PatternName,-22} {row.StabilityDays,6:0}d   reps {row.Reps}   lapses {row.Lapses}"
                : $"  {row.PatternName,-22} not yet seen");
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
        canvasObj.AddComponent<CanvasScaler>();
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
