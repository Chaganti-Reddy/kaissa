using Kaissa.Training;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Board-vision drill: shown a square, tap Light or Dark.
public sealed class VisionController : MonoBehaviour
{
    private readonly VisionSession _session = new();
    private Text _prompt;
    private Text _score;

    private void Start()
    {
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f); }

        var canvas = Hud.Canvas();
        Hud.Text(canvas, "Light or dark square?", 30, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 50f));
        _prompt = Hud.Text(canvas, "", 80, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(400f, 120f));
        _score = Hud.Text(canvas, "", 24, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(400f, 40f));
        Hud.Button(canvas, "Light", new Vector2(-100f, -120f), () => Answer(true), 160f);
        Hud.Button(canvas, "Dark", new Vector2(100f, -120f), () => Answer(false), 160f);
        Hud.Text(canvas, "Esc — menu", 18, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(400f, 30f));

        Next();
    }

    private void Answer(bool light)
    {
        _session.Answer(light);
        Next();
    }

    private void Next()
    {
        _prompt.text = _session.NextSquare();
        _score.text = $"Score {_session.Score}/{_session.Asked}";
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
    }
}
