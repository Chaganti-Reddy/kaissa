using System;
using System.Collections.Generic;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// First-run calibration: play a short adaptive run to estimate a starting rating, then seed it into
// saved progress so difficulty fits from the first real session. Reuses the shared board/input.
public sealed class CalibrateController : MonoBehaviour
{
    private CalibrationSession _session;
    private Transform _boardRoot;
    private BoardView _board;
    private bool _done;

    private BoardInteractor _interactor;
    private PieceAudio _audio;
    private Text _hudText;

    private void Start()
    {
        Board3D.SetupScene();
        _session = new CalibrationSession(ScenarioLibrary.LoadDefault(), puzzles: 12);

        var canvas = Hud.Canvas();
        _hudText = Hud.Text(canvas, "", 26, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1100f, 60f));
        Hud.Text(canvas, "Solve to find your level. Esc — skip", 18, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(800f, 30f));

        _audio = PieceAudio.Attach(gameObject);
        _interactor = gameObject.AddComponent<BoardInteractor>();
        _interactor.Init(uci => OnPlayerMove(uci), _audio);

        DealNext();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneManager.LoadScene("Menu");
    }

    // The interactor reports only legal moves (illegal ones snap back), so calibration grades a real move.
    private void OnPlayerMove(string uci)
    {
        if (_done || _board == null)
            return;
        _interactor.SetInputEnabled(false);
        _session.Submit(uci, TimeSpan.FromSeconds(4));
        DealNext();
    }

    private void DealNext()
    {
        if (_session.IsComplete)
        {
            Finish();
            return;
        }

        var scenario = _session.Next();
        if (scenario == null) { Finish(); return; }

        _board = BoardView.FromFen(scenario.Fen);
        if (_boardRoot != null) Destroy(_boardRoot.gameObject);
        _boardRoot = Board3D.Render(_board);
        Board3D.OrientCamera(!KaissaSettings.Flip || _board.WhiteToMove);
        _interactor.OnBoardRendered(_boardRoot, _board, lastMoveUci: null, humanCanMove: true);

        var side = _board.WhiteToMove ? "White" : "Black";
        _hudText.text = $"Calibration {_session.Answered + 1}/{_session.Total}   —   {side} to move   (est {_session.EstimatedRating:0})";
    }

    private void Finish()
    {
        _done = true;
        _interactor.SetInputEnabled(false);
        int rating = (int)Math.Round(_session.EstimatedRating);

        // Seed the estimate into saved progress (preserve any existing cards).
        var saved = KaissaProgress.Load();
        var model = saved != null ? SkillModel.FromJson(saved) : new SkillModel();
        model.RatingEstimate = rating;
        KaissaProgress.Save(model.ToJson());

        _hudText.text = $"Your starting rating: {rating}.   Esc — menu";
        if (_boardRoot != null) Destroy(_boardRoot.gameObject);
    }
}
