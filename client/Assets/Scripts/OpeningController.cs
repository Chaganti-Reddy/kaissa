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
    private BoardView _view;
    private Text _prompt;

    private BoardInteractor _interactor;
    private PieceAudio _audio;
    private Transform _pickerCanvas;

    private void Start()
    {
        Board3D.SetupScene();

        var canvas = Hud.Canvas();
        _prompt = Hud.Text(canvas, "", 26, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1000f, 60f));
        Hud.Text(canvas, "Play the line. Esc — menu", 18, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(800f, 30f));

        _audio = PieceAudio.Attach(gameObject);
        _interactor = gameObject.AddComponent<BoardInteractor>();
        _interactor.Init(uci => OnPlayerMove(uci), _audio);

        ShowPicker();
    }

    private void ShowPicker()
    {
        _pickerCanvas = Hud.Canvas();
        Hud.Text(_pickerCanvas, "Choose an opening", 32, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(900f, 50f));
        float y = 150f;
        foreach (var line in OpeningLibrary.All)
        {
            var l = line;
            Hud.Button(_pickerCanvas, l.Name, new Vector2(0f, y), () => StartLine(l), 460f);
            y -= 62f;
        }
        Hud.Button(_pickerCanvas, "Back", new Vector2(0f, y),
            () => UnityEngine.SceneManagement.SceneManager.LoadScene("Menu"), 460f);
    }

    private void StartLine(OpeningLine line)
    {
        if (_pickerCanvas != null) { Destroy(_pickerCanvas.gameObject); _pickerCanvas = null; }
        _line = line;
        _trainer = new OpeningTrainer(_line);
        RenderBoard();
    }

    private void RenderBoard()
    {
        if (_boardRoot != null)
            Destroy(_boardRoot.gameObject);
        _view = BoardView.FromFen(_trainer.Fen);
        _boardRoot = Board3D.Render(_view);
        _prompt.text = _trainer.IsComplete
            ? $"{_line.Name}: complete!"
            : $"{_line.Name} — play {_trainer.ExpectedMove}";
        _interactor.OnBoardRendered(_boardRoot, _view, lastMoveUci: null, humanCanMove: !_trainer.IsComplete);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
    }

    // The interactor only reports legal moves; here we additionally require the book move.
    private void OnPlayerMove(string uci)
    {
        if (_trainer == null || _trainer.IsComplete)
            return;
        if (_trainer.Play(uci))
        {
            RenderBoard();
        }
        else
        {
            _audio.PlayWrong();
            RenderBoard(); // reset piece positions on the current line position
            _prompt.text = $"{_line.Name} — not the book move. Play {_trainer.ExpectedMove}";
        }
    }
}
