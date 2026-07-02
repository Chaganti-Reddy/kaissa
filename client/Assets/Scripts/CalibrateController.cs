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
    private Scenario _current;

    private string _originSquare;
    private Transform _selectedPiece;
    private bool _done;

    private IReadOnlyList<string> _legalMoves = Array.Empty<string>();
    private readonly List<GameObject> _hints = new();
    private Text _hudText;

    private void Start()
    {
        Board3D.SetupScene();
        _session = new CalibrationSession(ScenarioLibrary.LoadDefault(), puzzles: 12);

        var canvas = Hud.Canvas();
        _hudText = Hud.Text(canvas, "", 26, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1100f, 60f));
        Hud.Text(canvas, "Solve to find your level. Esc — skip", 18, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(800f, 30f));

        DealNext();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene("Menu");
            return;
        }

        var mouse = Mouse.current;
        if (_done || _board == null || mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        var ray = Camera.main!.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out var hit))
            return;

        var hitName = hit.transform.name;
        if (hitName.Length < 2)
            return;
        var square = hitName.Substring(hitName.Length - 2);

        if (_originSquare == null)
        {
            if (!hitName.StartsWith("pc_", StringComparison.Ordinal))
                return;
            if (char.IsUpper(hitName[3]) != _board.WhiteToMove)
                return; // only the side to move
            _originSquare = square;
            _selectedPiece = hit.transform;
            _selectedPiece.position += Vector3.up * 0.35f;
            ShowHints(square);
        }
        else
        {
            var move = _originSquare + square;
            char moving = PieceAt(_originSquare);
            if ((moving == 'P' && square[1] == '8') || (moving == 'p' && square[1] == '1'))
                move += "q";
            _originSquare = null;
            _selectedPiece = null;
            ClearHints();
            _session.Submit(move, TimeSpan.FromSeconds(4));
            DealNext();
        }
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

        _current = scenario;
        _board = BoardView.FromFen(scenario.Fen);
        _legalMoves = ChessGame.FromFen(scenario.Fen).LegalUciMoves();
        ClearHints();
        if (_boardRoot != null) Destroy(_boardRoot.gameObject);
        _boardRoot = Board3D.Render(_board);
        Board3D.OrientCamera(_board.WhiteToMove);
        var side = _board.WhiteToMove ? "White" : "Black";
        _hudText.text = $"Calibration {_session.Answered + 1}/{_session.Total}   —   {side} to move   (est {_session.EstimatedRating:0})";
    }

    private void ShowHints(string origin)
    {
        ClearHints();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var color = new Color(0.30f, 0.90f, 0.45f);
        foreach (var uci in _legalMoves)
        {
            if (!uci.StartsWith(origin, StringComparison.Ordinal) || uci.Length < 4)
                continue;
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(_boardRoot);
            disc.transform.localScale = new Vector3(0.34f, 0.03f, 0.34f);
            disc.transform.position = new Vector3(uci[2] - 'a', 0.28f, uci[3] - '1');
            var mat = new Material(shader);
            mat.color = color;
            mat.SetColor("_BaseColor", color);
            disc.GetComponent<Renderer>().material = mat;
            _hints.Add(disc);
        }
    }

    private void ClearHints()
    {
        foreach (var hint in _hints)
            if (hint != null)
                Destroy(hint);
        _hints.Clear();
    }

    private void Finish()
    {
        _done = true;
        int rating = (int)Math.Round(_session.EstimatedRating);

        // Seed the estimate into saved progress (preserve any existing cards).
        var saved = KaissaProgress.Load();
        var model = saved != null ? SkillModel.FromJson(saved) : new SkillModel();
        model.RatingEstimate = rating;
        KaissaProgress.Save(model.ToJson());

        _hudText.text = $"Your starting rating: {rating}.   Esc — menu";
        if (_boardRoot != null) Destroy(_boardRoot.gameObject);
    }

    private char PieceAt(string square)
    {
        foreach (var p in _board.Pieces)
            if (p.Square == square)
                return p.Piece;
        return '\0';
    }
}
