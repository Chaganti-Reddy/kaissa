using System;
using System.Collections.Generic;
using Kaissa.Chess.Rules;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.InputSystem;

// Reusable board input + feedback for every 3D board screen. Handles click-to-move and drag-to-move
// together, derives legal moves / check / capture / castle / promotion from the position FEN, drives
// the highlight overlays (BoardFx) and sounds (PieceAudio), shows the promotion picker, and reports a
// completed move through a single onMove(uci) callback. The owning controller renders the board and,
// after each render, calls OnBoardRendered(...) so the interactor rebinds to the fresh objects.
public sealed class BoardInteractor : MonoBehaviour
{
    private const float TileTopY = 0.075f;
    private const float LiftSelect = 0.30f;
    private const float LiftDrag = 0.45f;
    private const float DragThresholdPixels = 6f;

    private Action<string> _onMove;
    private PieceAudio _audio;

    private Transform _root;
    private BoardView _board;
    private IReadOnlyList<string> _legal = Array.Empty<string>();
    private bool _humanCanMove;

    private string _selected;
    private Transform _grabbed;
    private Vector3 _restPos;
    private bool _dragging;
    private Vector2 _pressPos;

    public void Init(Action<string> onMove, PieceAudio audio)
    {
        _onMove = onMove;
        _audio = audio;
    }

    // Called by the controller after it (re)builds the board. Rebinds state and re-applies the
    // persistent highlights (last move + check). humanCanMove gates whether input is accepted.
    public void OnBoardRendered(Transform root, BoardView board, string lastMoveUci, bool humanCanMove)
    {
        _root = root;
        _board = board;
        _humanCanMove = humanCanMove;
        _selected = null;
        _grabbed = null;
        _dragging = false;

        _legal = LegalMovesOf(board);

        BoardFx.LastMove(root, lastMoveUci);
        BoardFx.Check(root, board);
        if (IsCheck(board))
            _audio?.PlayCheck();
    }

    public void SetInputEnabled(bool enabled) => _humanCanMove = enabled;

    private void Update()
    {
        if (_root == null || _board == null || !_humanCanMove)
            return;
        var mouse = Mouse.current;
        if (mouse == null)
            return;

        if (_grabbed != null && mouse.leftButton.isPressed && KaissaSettings.DragToMove)
        {
            var ground = GroundPoint(mouse);
            if (ground.HasValue)
            {
                if ((mouse.position.ReadValue() - _pressPos).magnitude > DragThresholdPixels)
                    _dragging = true;
                if (_dragging)
                {
                    var p = ground.Value;
                    _grabbed.position = new Vector3(Mathf.Clamp(p.x, 0f, 7f), TileTopY + LiftDrag, Mathf.Clamp(p.z, 0f, 7f));
                    BoardFx.Hover(_root, SquareAt(p));
                }
            }
        }

        if (mouse.leftButton.wasPressedThisFrame)
            HandlePress(mouse);
        else if (mouse.leftButton.wasReleasedThisFrame)
            HandleRelease(mouse);
    }

    private void HandlePress(Mouse mouse)
    {
        _pressPos = mouse.position.ReadValue();
        _dragging = false;

        if (!Raycast(mouse, out var square, out bool isPiece, out char pieceChar))
        {
            if (_selected != null) Deselect(snapBack: true);
            return;
        }

        bool grabbable = isPiece && char.IsUpper(pieceChar) == _board.WhiteToMove;

        if (_selected == null)
        {
            if (grabbable) BeginSelect(square);
        }
        else if (square == _selected)
        {
            _grabbed = FindPiece(square); // re-grab for a possible drag; keep the selection
        }
        else if (grabbable)
        {
            Deselect(snapBack: true);
            BeginSelect(square);
        }
        else
        {
            TryMove(_selected, square); // click-to-move onto a target square
        }
    }

    private void HandleRelease(Mouse mouse)
    {
        if (_grabbed == null)
            return;

        if (_dragging)
        {
            var ground = GroundPoint(mouse);
            var target = ground.HasValue ? SquareAt(ground.Value) : null;
            if (target == null || target == _selected)
                Deselect(snapBack: true);
            else
                TryMove(_selected, target);
            _dragging = false;
        }
        // A click without dragging keeps the piece selected (click-to-move); just clear the hover.
        BoardFx.ClearHover(_root);
    }

    private void BeginSelect(string square)
    {
        _selected = square;
        _grabbed = FindPiece(square);
        if (_grabbed != null)
        {
            _restPos = _grabbed.position;
            _grabbed.position = _restPos + Vector3.up * LiftSelect;
        }
        BoardFx.Selected(_root, square);
        BoardFx.LegalTargets(_root, square, _legal, _board);
        _audio?.PlaySelect();
    }

    private void TryMove(string from, string to)
    {
        char pieceChar = PieceCharAt(from);
        bool isPawn = char.ToUpperInvariant(pieceChar) == 'P';
        bool promo = isPawn && (to[1] == '8' || to[1] == '1');

        bool legal = promo ? Contains(from + to + "q") : Contains(from + to);
        if (!legal)
        {
            _audio?.PlayIllegal();
            Deselect(snapBack: true);
            return;
        }

        PlayMoveSound(from, to, pieceChar, promo);

        if (promo)
        {
            Deselect(snapBack: false);
            PromotionPicker.Ask(char.IsUpper(pieceChar), letter => _onMove?.Invoke(from + to + letter));
        }
        else
        {
            Deselect(snapBack: false);
            _onMove?.Invoke(from + to);
        }
    }

    private void PlayMoveSound(string from, string to, char pieceChar, bool promo)
    {
        if (_audio == null) return;
        bool king = char.ToUpperInvariant(pieceChar) == 'K';
        bool capture = Occupied(to) || (char.ToUpperInvariant(pieceChar) == 'P' && from[0] != to[0] && !Occupied(to));
        if (king && Mathf.Abs(from[0] - to[0]) == 2) _audio.PlayCastle();
        else if (promo) _audio.PlayPromote();
        else if (capture) _audio.PlayCapture();
        else _audio.PlayMove();
    }

    private void Deselect(bool snapBack)
    {
        if (snapBack && _grabbed != null)
            _grabbed.position = _restPos;
        _selected = null;
        _grabbed = null;
        _dragging = false;
        BoardFx.ClearSelect(_root);
        BoardFx.ClearDots(_root);
        BoardFx.ClearHover(_root);
    }

    // ----- geometry / lookup helpers -----

    private bool Raycast(Mouse mouse, out string square, out bool isPiece, out char pieceChar)
    {
        square = null; isPiece = false; pieceChar = '\0';
        var cam = Camera.main;
        if (cam == null) return false;
        var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out var hit)) return false;

        var name = hit.transform.name;
        if (name.Length < 2) return false;
        square = name.Substring(name.Length - 2);
        if (name.StartsWith("pc_", StringComparison.Ordinal) && name.Length >= 4)
        {
            isPiece = true;
            pieceChar = name[3];
        }
        return true;
    }

    private Vector3? GroundPoint(Mouse mouse)
    {
        var cam = Camera.main;
        if (cam == null) return null;
        var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        var plane = new Plane(Vector3.up, new Vector3(0f, TileTopY, 0f));
        return plane.Raycast(ray, out float d) ? ray.GetPoint(d) : (Vector3?)null;
    }

    private static string SquareAt(Vector3 p)
    {
        int f = Mathf.RoundToInt(p.x);
        int r = Mathf.RoundToInt(p.z);
        if (f < 0 || f > 7 || r < 0 || r > 7) return null;
        return $"{(char)('a' + f)}{r + 1}";
    }

    private Transform FindPiece(string square)
    {
        if (_root == null) return null;
        foreach (Transform child in _root)
            if (child.name.StartsWith("pc_", StringComparison.Ordinal) &&
                child.name.EndsWith("_" + square, StringComparison.Ordinal))
                return child;
        return null;
    }

    private char PieceCharAt(string square)
    {
        foreach (var p in _board.Pieces)
            if (p.Square == square) return p.Piece;
        return '\0';
    }

    private bool Occupied(string square)
    {
        foreach (var p in _board.Pieces)
            if (p.Square == square) return true;
        return false;
    }

    private bool Contains(string uci)
    {
        foreach (var m in _legal)
            if (m == uci) return true;
        return false;
    }

    private static IReadOnlyList<string> LegalMovesOf(BoardView board)
    {
        try { return ChessGame.FromFen(board.Fen).LegalUciMoves(); }
        catch { return Array.Empty<string>(); }
    }

    private static bool IsCheck(BoardView board)
    {
        try { return ChessGame.FromFen(board.Fen).IsCheck; }
        catch { return false; }
    }
}
