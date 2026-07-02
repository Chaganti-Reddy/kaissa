using System;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Opening trainer: play the shown line move by move (a hint shows the next book move).
public sealed class OpeningController : MonoBehaviour
{
    private OpeningLine _line;
    private OpeningTrainer _trainer;
    private Transform _boardRoot;
    private Text _prompt;

    private string _originSquare;
    private Transform _selectedPiece;

    private void Start()
    {
        Board3D.SetupScene();
        _line = OpeningLibrary.All[0]; // Italian by default; a picker can choose later
        _trainer = new OpeningTrainer(_line);

        var canvas = Hud.Canvas();
        _prompt = Hud.Text(canvas, "", 26, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1000f, 60f));
        Hud.Text(canvas, "Play the line. Esc — menu", 18, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(800f, 30f));

        RenderBoard();
    }

    private void RenderBoard()
    {
        if (_boardRoot != null)
            Destroy(_boardRoot.gameObject);
        _boardRoot = Board3D.Render(BoardView.FromFen(_trainer.Fen));
        _prompt.text = _trainer.IsComplete
            ? $"{_line.Name}: complete!"
            : $"{_line.Name} — play {_trainer.ExpectedMove}";
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            return;
        }

        var mouse = Mouse.current;
        if (_trainer.IsComplete || mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        var ray = Camera.main!.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out var hit))
            return;

        var name = hit.transform.name;
        if (name.Length < 2)
            return;
        var square = name.Substring(name.Length - 2);

        if (_originSquare == null)
        {
            if (!name.StartsWith("pc_", StringComparison.Ordinal))
                return;
            if (char.IsUpper(name[3]) != BoardView.FromFen(_trainer.Fen).WhiteToMove)
                return; // only the side to move
            _originSquare = square;
            _selectedPiece = hit.transform;
            _selectedPiece.position += Vector3.up * 0.35f;
        }
        else
        {
            var move = _originSquare + square;
            _originSquare = null;
            _selectedPiece = null;
            if (_trainer.Play(move))
                RenderBoard();
            else
                _prompt.text = $"{_line.Name} — not the book move. Play {_trainer.ExpectedMove}";
        }
    }
}
