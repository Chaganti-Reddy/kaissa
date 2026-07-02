using Kaissa.Training;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Endgame picker: choose an instructive endgame to play out against the engine. The chosen FEN is
// handed to the Play scene via EndgameRoute.
public static class EndgameRoute
{
    public static string? Fen;
}

public sealed class EndgameController : MonoBehaviour
{
    private void Start()
    {
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f); }

        var canvas = Hud.Canvas();
        Hud.Text(canvas, "Endgames", 44, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(800f, 60f));

        float y = 120f;
        foreach (var endgame in EndgameLibrary.All)
        {
            var fen = endgame.Fen;
            Hud.Button(canvas, endgame.Name, new Vector2(0f, y), () =>
            {
                EndgameRoute.Fen = fen;
                SceneManager.LoadScene("Play");
            }, 460f);
            y -= 70f;
        }

        Hud.Text(canvas, "Esc — menu", 18, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(400f, 30f));
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneManager.LoadScene("Menu");
    }
}
