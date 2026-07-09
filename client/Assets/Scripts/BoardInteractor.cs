using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    private const float LiftDrag = 0.28f;        // how high a dragged piece floats (lower = less detached)
    private const float DragThresholdPixels = 6f;
    private const float MoveGlideSeconds = 0.11f; // slide-to-square duration (snappier reads better)
    private const float CapturePopSeconds = 0.10f;

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

    // Premove (opponent's turn): queue one move that fires when it becomes the human's turn again.
    public bool AllowPremove;
    private (string from, string to)? _premove;
    private string _premoveFrom;
    private string _hoverPreviewSquare;

    // right-click annotations (square highlights + arrows) on the 3D board
    private Transform _annRoot;
    private string _rDownSquare;
    private static readonly Color AnnRed = new(0.86f, 0.20f, 0.20f, 0.85f);
    private static readonly Color AnnGreen = new(0.35f, 0.62f, 0.24f, 0.9f);
    private static readonly Color AnnBlue = new(0.30f, 0.55f, 0.80f, 0.9f);

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
        _hoverPreviewSquare = null;

        _legal = LegalMovesOf(board);

        BoardFx.LastMove(root, lastMoveUci);
        BoardFx.Check(root, board);
        if (IsCheck(board))
            _audio?.PlayCheck();

        if (humanCanMove && _premove.HasValue)
        {
            var pm = _premove.Value;
            _premove = null;
            _premoveFrom = null;
            BoardFx.ClearPremove(root);
            _selected = pm.from;
            _grabbed = FindPiece(pm.from);
            if (_grabbed != null) _restPos = _grabbed.position;
            TryMove(pm.from, pm.to); // validates against the new position; illegal premoves are dropped
        }
        else if (!humanCanMove && _premove.HasValue)
        {
            BoardFx.Premove(root, _premove.Value.from, _premove.Value.to);
        }
    }

    public void SetInputEnabled(bool enabled) => _humanCanMove = enabled;

    // Animate a move made by someone else (the bot) on the current board: glide the piece and pop any
    // capture, with sound. Completes when the glide ends, so the caller can then render the position.
    public Task PlayVisualMoveAsync(string uci)
    {
        if (_root == null || _board == null || uci == null || uci.Length < 4)
            return Task.CompletedTask;

        var from = uci.Substring(0, 2);
        var to = uci.Substring(2, 2);
        var moving = FindPiece(from);
        var captured = Occupied(to) ? FindPiece(to) : null;
        PlayMoveSound(from, to, PieceCharAt(from), promo: uci.Length > 4);

        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(VisualMove(moving, captured, to, () => tcs.TrySetResult(true)));
        return tcs.Task;
    }

    private IEnumerator VisualMove(Transform moving, Transform captured, string to, Action onDone)
    {
        if (captured != null)
            StartCoroutine(Pop(captured));
        if (moving != null)
        {
            var start = moving.position;
            var target = new Vector3(to[0] - 'a', TileTopY, to[1] - '1');
            for (float t = 0f; t < 1f; t += Time.deltaTime / MoveGlideSeconds)
            {
                moving.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            moving.position = target;
        }
        onDone?.Invoke();
    }

    private void Update()
    {
        if (_root == null || _board == null)
            return;
        var mouse = Mouse.current;
        if (mouse == null)
            return;

        // right-click annotations (any time); a left press clears them
        if (mouse.rightButton.wasPressedThisFrame)
            _rDownSquare = RaycastSquare(mouse);
        else if (mouse.rightButton.wasReleasedThisFrame && _rDownSquare != null)
        {
            var up = RaycastSquare(mouse) ?? _rDownSquare;
            var col = AnnColor();
            if (up == _rDownSquare) ToggleHighlight(_rDownSquare, col);
            else AddArrow(_rDownSquare, up, col);
            _rDownSquare = null;
        }
        if (mouse.leftButton.wasPressedThisFrame)
            ClearAnnotations();

        if (!_humanCanMove)
        {
            if (AllowPremove && mouse.leftButton.wasPressedThisFrame)
                HandlePremoveInput(mouse);
            return;
        }

        // Handle the press/release first so a fresh click resets _pressPos before the drag check —
        // otherwise a click while a piece is still selected compares against a stale press position
        // and teleports the piece to the cursor.
        if (mouse.leftButton.wasPressedThisFrame)
            HandlePress(mouse);
        else if (mouse.leftButton.wasReleasedThisFrame)
            HandleRelease(mouse);

        // Drag-follow only while the button is held and past the drag threshold from this press.
        if (_grabbed != null && mouse.leftButton.isPressed && !mouse.leftButton.wasPressedThisFrame
            && KaissaSettings.DragToMove)
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

        // Hover preview: peek a piece's legal moves without selecting (nothing held or selected).
        if (KaissaSettings.MoveHints && _selected == null && !mouse.leftButton.isPressed)
        {
            string hover = null;
            if (Raycast(mouse, out var sq, out bool isPc, out char pc) && isPc && char.IsUpper(pc) == _board.WhiteToMove)
                hover = sq;
            if (hover != _hoverPreviewSquare)
            {
                _hoverPreviewSquare = hover;
                if (hover != null) BoardFx.LegalTargets(_root, hover, _legal, _board);
                else BoardFx.ClearDots(_root);
            }
        }
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
        _hoverPreviewSquare = null;
        if (_grabbed != null)
            _restPos = _grabbed.position; // leave it on the board; only dragging lifts it
        BoardFx.Selected(_root, square);
        if (KaissaSettings.MoveHints)
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

        // Grab references before clearing selection, then glide the piece to its square (and pop any
        // captured piece) before committing, so the move reads as an action rather than a snap.
        var moving = _grabbed;
        var captured = Occupied(to) ? FindPiece(to) : null;
        BoardFx.ClearSelect(_root);
        BoardFx.ClearDots(_root);
        BoardFx.ClearHover(_root);
        _selected = null;
        _grabbed = null;
        _dragging = false;
        _humanCanMove = false; // no input while the move animates

        StartCoroutine(AnimateThenCommit(moving, captured, from, to, promo, char.IsUpper(pieceChar)));
    }

    private IEnumerator AnimateThenCommit(Transform moving, Transform captured, string from, string to, bool promo, bool white)
    {
        if (captured != null)
            StartCoroutine(Pop(captured));

        if (moving != null)
        {
            var start = moving.position;
            var target = new Vector3(to[0] - 'a', TileTopY, to[1] - '1');
            const float dur = MoveGlideSeconds;
            for (float t = 0f; t < 1f; t += Time.deltaTime / dur)
            {
                moving.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            moving.position = target;
        }

        if (promo)
            PromotionPicker.Ask(white, letter => _onMove?.Invoke(from + to + letter));
        else
            _onMove?.Invoke(from + to);
    }

    private static IEnumerator Pop(Transform piece)
    {
        var start = piece.localScale;
        const float dur = CapturePopSeconds;
        for (float t = 0f; t < 1f; t += Time.deltaTime / dur)
        {
            if (piece == null) yield break;
            piece.localScale = Vector3.Lerp(start, Vector3.zero, Mathf.Clamp01(t));
            yield return null;
        }
        if (piece != null)
            piece.localScale = Vector3.zero;
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
        _hoverPreviewSquare = null;
        BoardFx.ClearSelect(_root);
        BoardFx.ClearDots(_root);
        BoardFx.ClearHover(_root);
    }

    private void HandlePremoveInput(Mouse mouse)
    {
        if (!Raycast(mouse, out var square, out bool isPiece, out char pieceChar))
        {
            CancelPremove();
            return;
        }
        // The human is the side NOT to move during the opponent's turn.
        bool humanPiece = isPiece && char.IsUpper(pieceChar) == !_board.WhiteToMove;

        if (_premoveFrom == null)
        {
            if (humanPiece) { _premoveFrom = square; _premove = null; BoardFx.Premove(_root, square, null); }
            else CancelPremove();
        }
        else if (square == _premoveFrom)
        {
            CancelPremove();
        }
        else if (humanPiece)
        {
            _premoveFrom = square; _premove = null; BoardFx.Premove(_root, square, null);
        }
        else
        {
            _premove = (_premoveFrom, square);
            _premoveFrom = null;
            BoardFx.Premove(_root, _premove.Value.from, _premove.Value.to);
        }
    }

    private void CancelPremove()
    {
        _premove = null;
        _premoveFrom = null;
        BoardFx.ClearPremove(_root);
    }

    // ----- right-click annotations (3D) -----

    private static Color AnnColor()
    {
        var kb = Keyboard.current;
        if (kb == null) return AnnRed;
        if (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed) return AnnBlue;
        if (kb.leftShiftKey.isPressed || kb.leftCtrlKey.isPressed) return AnnGreen;
        return AnnRed;
    }

    private string RaycastSquare(Mouse m) => Raycast(m, out var sq, out _, out _) ? sq : null;

    private Transform EnsureAnnRoot()
    {
        if (_annRoot == null || _annRoot.parent != _root)
        {
            var go = new GameObject("annotations");
            go.transform.SetParent(_root, false);
            _annRoot = go.transform;
        }
        return _annRoot;
    }

    private void ClearAnnotations()
    {
        if (_annRoot != null) { Destroy(_annRoot.gameObject); _annRoot = null; }
    }

    private static Vector3 Center(string sq) => new(sq[0] - 'a', TileTopY + 0.02f, sq[1] - '1');

    private void ToggleHighlight(string sq, Color col)
    {
        var root = EnsureAnnRoot();
        var existing = root.Find("hl_" + sq);
        if (existing != null) { Destroy(existing.gameObject); return; }
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "hl_" + sq;
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(root);
        q.transform.position = Center(sq) + new Vector3(0, 0.01f, 0);
        q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        q.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
        PaintAnn(q, col);
    }

    private void AddArrow(string from, string to, Color col)
    {
        var root = EnsureAnnRoot();
        Vector3 a = Center(from), b = Center(to), dir = b - a;
        float len = dir.magnitude;
        if (len < 0.1f) return;
        var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        const float headLen = 0.30f;
        float shaftLen = Mathf.Max(0.02f, len - headLen);

        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(shaft.GetComponent<Collider>()); shaft.name = "arrow"; shaft.transform.SetParent(root);
        shaft.transform.position = a + dir.normalized * (shaftLen * 0.5f) + new Vector3(0, 0.03f, 0);
        shaft.transform.rotation = rot;
        shaft.transform.localScale = new Vector3(0.12f, 0.05f, shaftLen);
        PaintAnn(shaft, col);

        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(head.GetComponent<Collider>()); head.name = "arrow"; head.transform.SetParent(root);
        head.transform.position = b - dir.normalized * (headLen * 0.5f) + new Vector3(0, 0.03f, 0);
        head.transform.rotation = rot * Quaternion.Euler(0f, 45f, 0f);
        head.transform.localScale = new Vector3(0.24f, 0.05f, 0.24f);
        PaintAnn(head, col);
    }

    private static void PaintAnn(GameObject go, Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        var m = new Material(sh) { color = c };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        go.GetComponent<Renderer>().material = m;
    }

    // Sample annotations for the screenshot harness to verify the 3D overlay.
    public void DebugAnnotate()
    {
        ToggleHighlight("d4", AnnRed);
        ToggleHighlight("e5", AnnGreen);
        ToggleHighlight("c6", AnnBlue);
        AddArrow("g1", "f3", AnnGreen);
        AddArrow("e2", "e4", AnnRed);
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
