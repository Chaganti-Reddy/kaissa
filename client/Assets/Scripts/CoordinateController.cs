using Kaissa.Training;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Coordinate drill: shown a square name, click that square on the board.
public sealed class CoordinateController : MonoBehaviour
{
    private readonly CoordinateSession _session = new();
    private Text _prompt;
    private Text _score;
    private string _target = "";

    private void Start()
    {
        Board3D.SetupScene();
        Board3D.RenderEmpty();

        var canvas = Hud.Canvas();
        _prompt = Hud.Text(canvas, "", 34, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(700f, 50f));
        _score = Hud.Text(canvas, "", 24, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(400f, 40f));
        Hud.Text(canvas, "Click the named square.  Esc — menu", 20, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(800f, 40f));

        Next();
    }

    private void Next()
    {
        _target = _session.NextTarget();
        _prompt.text = $"Find:  {_target}";
        _score.text = $"Score {_session.Score}/{_session.Asked}";
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        var ray = Camera.main!.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out var hit) || !hit.transform.name.StartsWith("sq_"))
            return;

        var square = hit.transform.name.Substring(hit.transform.name.Length - 2);
        var (file, rank) = Coordinates.Parse(square);
        _session.Answer(file, rank);
        Next();
    }
}
