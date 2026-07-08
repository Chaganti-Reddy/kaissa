using System.Collections.Generic;
using System.Linq;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Custom practice: pick any pattern to drill in themed mode, instead of the adaptive loop. The chosen
// pattern is handed to the training scene via ThemeRoute (same path as Stats' "practice weakest").
public sealed class CustomController : MonoBehaviour
{
    private void Start()
    {
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f); }

        var canvas = Hud.Canvas();
        Hud.Text(canvas, "Practice a pattern", 44, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 60f));

        var patterns = LoadPatterns();
        if (patterns.Count == 0)
        {
            Hud.Text(canvas, "No patterns available.", 24, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 60f));
        }
        else
        {
            // Two columns so the full pattern set fits without scrolling.
            const float top = 150f, step = 56f;
            int half = (patterns.Count + 1) / 2;
            for (int i = 0; i < patterns.Count; i++)
            {
                var p = patterns[i];
                float x = i < half ? -240f : 240f;
                float y = top - (i % half) * step;
                Hud.Button(canvas, p.Name, new Vector2(x, y), () =>
                {
                    ThemeRoute.PatternId = p.Id;
                    ThemeRoute.PatternName = p.Name;
                    SceneManager.LoadScene("SampleScene");
                }, 440f);
            }
        }

        Hud.Text(canvas, "Esc — menu", 18, TextAnchor.LowerCenter,
            new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(400f, 30f));
    }

    private static List<(string Id, string Name)> LoadPatterns()
    {
        try
        {
            var trainer = KaissaTrainer.CreateDefault(KaissaProgress.Load());
            return trainer.Progress()
                .Select(r => (r.PatternId, r.PatternName))
                .OrderBy(r => r.PatternName)
                .ToList();
        }
        catch
        {
            return new List<(string, string)>();
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneManager.LoadScene("Menu");
    }
}
