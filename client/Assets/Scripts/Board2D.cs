using System;
using System.Collections.Generic;
using System.Linq;
using Kaissa.Chess.Rules;
using UnityEngine;
using UnityEngine.UIElements;

// A flat 2D chess board rendered in UI Toolkit, chess.com-style. Renders a position from a FEN,
// accepts click-to-move (select a piece, then a target), shows legal-move dots, and the selected /
// last-move / check highlights. Legal moves and promotion are validated against the rules core, so
// it reports only legal moves through onMove (UCI). Mirrors BoardInteractor's contract for the 2D
// board mode; the 3D board keeps using BoardInteractor.
public sealed class Board2D : IBoardView
{
    public readonly VisualElement Root = new();

    // When set, a click reports the clicked square (e.g. "e4") instead of making a move - used by the
    // coordinate drill. When true (default) edge coordinate labels show, honoring the global setting.
    public Action<string> SquareClickHandler { get; set; }
    public bool ShowCoordinates = true;
    public bool AllowPremove { get; set; }

    private readonly Action<string> _onMove;

    // premove (queue a move during the opponent's turn)
    private (int f, int r)? _premoveSel;
    private (string from, string to)? _premove;
    private static readonly Color PremoveCol = new(0.90f, 0.55f, 0.20f, 0.55f);

    private string _fen;
    private bool _whiteBottom = true;
    private bool _canMove;
    private (int f, int r)? _sel;
    private string _lastMove;

    // drag-to-move state
    private (int row, int col)? _downRC;
    private Vector2 _downPos;
    private string _dragFrom;
    private bool _dragging;
    private Label _ghost;
    private (int row, int col) _ghostSrc;

    private static readonly Color Sel = new(0.96f, 0.96f, 0.41f, 0.55f);   // yellow select/last-move
    private static readonly Color Check = new(0.85f, 0.20f, 0.20f, 0.75f);

    // 2D square palettes, indexed by KaissaSettings.BoardTheme (same order as Board3D.Themes so the
    // one Board-theme setting drives both boards): Walnut, Green, Blue, Slate, Marble, Coral, Ice, Midnight.
    private static readonly (Color light, Color dark)[] Themes2D =
    {
        (UiKit.Hex(0xf0, 0xd9, 0xb5), UiKit.Hex(0xb5, 0x88, 0x63)), // Walnut
        (UiKit.Hex(0xeb, 0xec, 0xd0), UiKit.Hex(0x73, 0x95, 0x52)), // Green
        (UiKit.Hex(0xde, 0xe3, 0xe6), UiKit.Hex(0x8c, 0xa2, 0xad)), // Blue
        (UiKit.Hex(0xdc, 0xdc, 0xdc), UiKit.Hex(0x7d, 0x87, 0x96)), // Slate
        (UiKit.Hex(0xe8, 0xe4, 0xda), UiKit.Hex(0x9b, 0x91, 0x87)), // Marble
        (UiKit.Hex(0xf2, 0xdf, 0xd0), UiKit.Hex(0xc9, 0x8b, 0x6a)), // Coral
        (UiKit.Hex(0xe6, 0xf0, 0xf5), UiKit.Hex(0x9f, 0xc0, 0xd4)), // Ice
        (UiKit.Hex(0xcf, 0xd3, 0xe0), UiKit.Hex(0x4b, 0x56, 0x70)), // Midnight
    };

    private static readonly Dictionary<char, string> Pieces = new()
    {
        ['P'] = "♟", ['N'] = "♞", ['B'] = "♝", ['R'] = "♜", ['Q'] = "♛", ['K'] = "♚",
    };

    public Board2D(Action<string> onMove)
    {
        _onMove = onMove;
        Root.style.flexDirection = FlexDirection.Column;
        Root.style.flexShrink = 0; // never let the layout squish the board out of square
        UiKit.Radius(Root, 6);
        Root.style.overflow = Overflow.Hidden;
        Build();
        // Size the piece glyphs to the board whenever it is laid out or resized.
        Root.RegisterCallback<GeometryChangedEvent>(e =>
        {
            int fs = Mathf.RoundToInt(e.newRect.width / 8f * 0.78f);
            if (fs <= 0) return;
            for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
                _rowPieces[row, col].style.fontSize = fs;
        });
        Root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        Root.RegisterCallback<PointerUpEvent>(OnPointerUp);
    }

    private void Build()
    {
        // rows top->bottom; recomputed on flip in Render via cell colors + glyphs.
        for (int row = 0; row < 8; row++)
        {
            var rowEl = new VisualElement();
            rowEl.style.flexDirection = FlexDirection.Row;
            rowEl.style.height = Length.Percent(12.5f);
            rowEl.style.flexShrink = 0;
            for (int col = 0; col < 8; col++)
            {
                var cell = new VisualElement();
                cell.style.width = Length.Percent(12.5f);
                cell.style.height = Length.Percent(100);
                cell.style.flexShrink = 0;
                cell.style.alignItems = Align.Center;
                cell.style.justifyContent = Justify.Center;
                int rr = row, cc = col;
                cell.RegisterCallback<PointerDownEvent>(e => OnPointerDown(e, rr, cc));

                var ov = new VisualElement();
                ov.pickingMode = PickingMode.Ignore;
                ov.style.position = Position.Absolute;
                ov.style.left = 0; ov.style.top = 0; ov.style.right = 0; ov.style.bottom = 0;
                ov.style.alignItems = Align.Center;
                ov.style.justifyContent = Justify.Center;

                var pc = new Label();
                pc.pickingMode = PickingMode.Ignore;
                pc.style.unityTextAlign = TextAnchor.MiddleCenter;

                cell.Add(ov);
                cell.Add(pc);
                rowEl.Add(cell);
                _rowCells[row, col] = cell;
                _rowOverlays[row, col] = ov;
                _rowPieces[row, col] = pc;
            }
            Root.Add(rowEl);
        }

        // Persistent annotation layer (right-click square highlights + arrows), above the cells and
        // not cleared by Render. Left-click or a move clears it.
        _annotations = new VisualElement { pickingMode = PickingMode.Ignore };
        _annotations.style.position = Position.Absolute;
        _annotations.style.left = 0; _annotations.style.top = 0; _annotations.style.right = 0; _annotations.style.bottom = 0;
        _annotations.generateVisualContent = OnDrawArrows;
        Root.Add(_annotations);
    }

    private VisualElement _annotations;
    private readonly List<(string sq, Color c)> _annSquares = new();
    private readonly List<(string from, string to, Color c)> _annArrows = new();
    private readonly List<(string from, string to, Color c)> _engineArrows = new(); // best move / threat
    private (int row, int col)? _rDown;

    public void SetEngineArrows(IReadOnlyList<(string from, string to, Color color)> arrows)
    {
        _engineArrows.Clear();
        if (arrows != null)
            foreach (var a in arrows) _engineArrows.Add((a.from, a.to, a.color));
        _annotations?.MarkDirtyRepaint();
    }

    private static readonly Color AnnRed = new(0.86f, 0.20f, 0.20f, 0.80f);
    private static readonly Color AnnGreen = new(0.35f, 0.62f, 0.24f, 0.85f);
    private static readonly Color AnnBlue = new(0.30f, 0.55f, 0.80f, 0.85f);

    private static Color AnnColor(bool alt, bool greenMod) => alt ? AnnBlue : greenMod ? AnnGreen : AnnRed;

    private void ToggleSquare(string sq, Color c)
    {
        int i = _annSquares.FindIndex(a => a.sq == sq);
        if (i >= 0) _annSquares.RemoveAt(i);
        else _annSquares.Add((sq, c));
    }

    private void ClearAnnotations()
    {
        if (_annSquares.Count == 0 && _annArrows.Count == 0) return;
        _annSquares.Clear();
        _annArrows.Clear();
        RefreshAnnotations();
    }

    private void RefreshAnnotations()
    {
        // square highlights are child elements; arrows are painted in generateVisualContent
        var toRemove = _annotations.Children().ToList();
        foreach (var c in toRemove) _annotations.Remove(c);
        foreach (var (sq, col) in _annSquares)
        {
            var (f, r) = Sq(sq);
            var (row, colIdx) = ScreenOf(f, r);
            var hl = new VisualElement { pickingMode = PickingMode.Ignore };
            hl.style.position = Position.Absolute;
            hl.style.left = Length.Percent(colIdx * 12.5f);
            hl.style.top = Length.Percent(row * 12.5f);
            hl.style.width = Length.Percent(12.5f);
            hl.style.height = Length.Percent(12.5f);
            hl.style.backgroundColor = col;
            _annotations.Add(hl);
        }
        _annotations.MarkDirtyRepaint();
    }

    private void OnDrawArrows(MeshGenerationContext ctx)
    {
        var rect = _annotations.contentRect;
        if (rect.width <= 0) return;
        float cw = rect.width / 8f, ch = rect.height / 8f;
        var p = ctx.painter2D;
        foreach (var (from, to, col) in _annArrows.Concat(_engineArrows))
        {
            var (ff, fr) = Sq(from);
            var (tf, tr) = Sq(to);
            var (frow, fcol) = ScreenOf(ff, fr);
            var (trow, tcol) = ScreenOf(tf, tr);
            Vector2 a = new((fcol + 0.5f) * cw, (frow + 0.5f) * ch);
            Vector2 b = new((tcol + 0.5f) * cw, (trow + 0.5f) * ch);
            Vector2 dir = (b - a).normalized;
            Vector2 tip = b - dir * (cw * 0.18f);      // stop shaft short of the head
            Vector2 headBase = b - dir * (cw * 0.42f);
            Vector2 perp = new(-dir.y, dir.x);
            float w = cw * 0.16f;

            p.strokeColor = col; p.lineWidth = cw * 0.16f; p.lineCap = LineCap.Round;
            p.BeginPath(); p.MoveTo(a); p.LineTo(tip); p.Stroke();

            p.fillColor = col;
            p.BeginPath();
            p.MoveTo(b);
            p.LineTo(headBase + perp * w * 1.6f);
            p.LineTo(headBase - perp * w * 1.6f);
            p.ClosePath(); p.Fill();
        }
    }

    // Row/col grid as laid out on screen (before mapping to file/rank via orientation).
    private readonly VisualElement[,] _rowCells = new VisualElement[8, 8];
    private readonly VisualElement[,] _rowOverlays = new VisualElement[8, 8];
    private readonly Label[,] _rowPieces = new Label[8, 8];

    // Map a screen (row,col) to board (file,rank) honoring orientation.
    private (int f, int r) ToBoard(int row, int col) =>
        _whiteBottom ? (col, 7 - row) : (7 - col, row);

    public void Render(string fen, bool canMove, string lastMove, bool whiteBottom)
    {
        _fen = fen;
        _canMove = canMove;
        _lastMove = lastMove;
        _whiteBottom = whiteBottom;
        _sel = null;

        var board = new AttackBoardLite(fen);
        var theme = Themes2D[Mathf.Clamp(KaissaSettings.BoardTheme, 0, Themes2D.Length - 1)];
        Color lightC = theme.light, darkC = theme.dark;
        int wkF = -1, wkR = -1, bkF = -1, bkR = -1;

        for (int row = 0; row < 8; row++)
        for (int col = 0; col < 8; col++)
        {
            var (f, r) = ToBoard(row, col);
            var cell = _rowCells[row, col];
            bool dark = (f + r) % 2 == 0; // a1 (0,0) dark
            cell.style.backgroundColor = dark ? darkC : lightC;

            char p = board.At(f, r);
            var pc = _rowPieces[row, col];
            if (p == '\0') { pc.text = ""; }
            else
            {
                bool white = char.IsUpper(p);
                pc.text = Pieces[char.ToUpperInvariant(p)];
                pc.style.color = white ? UiKit.Hex(0xf7, 0xf7, 0xf2) : UiKit.Hex(0x2b, 0x2b, 0x2b);
                pc.style.unityTextOutlineWidth = 1.4f;
                pc.style.unityTextOutlineColor = white ? UiKit.Hex(0x33, 0x33, 0x33) : UiKit.Hex(0xe8, 0xe8, 0xe8);
                if (char.ToUpperInvariant(p) == 'K') { if (white) { wkF = f; wkR = r; } else { bkF = f; bkR = r; } }
            }

            var ovel = _rowOverlays[row, col];
            ovel.Clear();
            ovel.style.backgroundColor = new Color(0, 0, 0, 0);

            if (ShowCoordinates && KaissaSettings.Coordinates)
            {
                Color coText = dark ? lightC : darkC;
                if (row == 7) ovel.Add(Coord($"{(char)('a' + f)}", coText, TextAnchor.LowerRight));
                if (col == 0) ovel.Add(Coord($"{r + 1}", coText, TextAnchor.UpperLeft));
            }
        }

        // last-move highlight
        if (!string.IsNullOrEmpty(lastMove) && lastMove.Length >= 4)
        {
            Highlight(Sq(lastMove.Substring(0, 2)), Sel);
            Highlight(Sq(lastMove.Substring(2, 2)), Sel);
        }

        // check highlight (red on the side-to-move king if in check)
        var game = SafeGame(fen);
        if (game != null && game.IsCheck)
        {
            bool whiteToMove = game.SideToMove == Side.White;
            if (whiteToMove && wkF >= 0) Highlight((wkF, wkR), Check);
            if (!whiteToMove && bkF >= 0) Highlight((bkF, bkR), Check);
        }

        // premove: show it while the opponent moves; play it as soon as it becomes our turn
        if (_premoveSel is { } psel) Highlight(psel, PremoveCol);
        if (_premove is { } pm)
        {
            if (canMove)
            {
                _premove = null;
                Root.schedule.Execute(() => AttemptMove(pm.from, pm.to, false)).ExecuteLater(0);
            }
            else
            {
                Highlight(Sq(pm.from), PremoveCol);
                Highlight(Sq(pm.to), PremoveCol);
            }
        }

        // glide the piece for a newly-arrived move (skip re-renders like flip/select of the same move)
        if (!string.IsNullOrEmpty(lastMove) && lastMove != _lastAnimated && lastMove.Length >= 4)
        {
            _lastAnimated = lastMove;
            AnimateGlide(lastMove);
        }
        else _lastAnimated = lastMove;
    }

    private string _lastAnimated;

    private void AnimateGlide(string uci)
    {
        float w = Root.resolvedStyle.width;
        if (w <= 1f) return; // not laid out yet
        float sq = w / 8f;
        var (ff, fr) = Sq(uci.Substring(0, 2));
        var (tf, tr) = Sq(uci.Substring(2, 2));
        var (frow, fcol) = ScreenOf(ff, fr);
        var (trow, tcol) = ScreenOf(tf, tr);
        var tgt = _rowPieces[trow, tcol];
        if (string.IsNullOrEmpty(tgt.text)) return;

        var ghost = new Label(tgt.text) { pickingMode = PickingMode.Ignore };
        ghost.style.position = Position.Absolute;
        ghost.style.width = sq; ghost.style.height = sq;
        ghost.style.unityTextAlign = TextAnchor.MiddleCenter;
        ghost.style.color = tgt.resolvedStyle.color;
        ghost.style.fontSize = tgt.resolvedStyle.fontSize;
        ghost.style.unityTextOutlineWidth = 1.4f;
        ghost.style.unityTextOutlineColor = tgt.resolvedStyle.unityTextOutlineColor;
        Root.Add(ghost);
        tgt.style.visibility = Visibility.Hidden;

        var startP = new Vector2(fcol * sq, frow * sq);
        var endP = new Vector2(tcol * sq, trow * sq);
        ghost.style.left = startP.x; ghost.style.top = startP.y;
        ghost.experimental.animation
            .Start(startP, endP, 110, (el, v) => { el.style.left = v.x; el.style.top = v.y; })
            .OnCompleted(() => { tgt.style.visibility = Visibility.Visible; if (ghost.parent != null) Root.Remove(ghost); });
    }

    private static Label Coord(string s, Color c, TextAnchor anchor)
    {
        var l = new Label(s);
        l.pickingMode = PickingMode.Ignore;
        l.style.position = Position.Absolute;
        l.style.fontSize = 11; l.style.color = c;
        l.style.unityFontStyleAndWeight = FontStyle.Bold;
        if (anchor == TextAnchor.LowerRight) { l.style.right = 3; l.style.bottom = 1; }
        else { l.style.left = 3; l.style.top = 1; }
        return l;
    }

    // Highlight a square by name (e.g. a hint's from-square, or a solution) on top of the current render.
    public void HighlightSquare(string sq, Color c)
    {
        if (!string.IsNullOrEmpty(sq) && sq.Length >= 2)
            Highlight(Sq(sq.Substring(0, 2)), c);
    }

    private void Highlight((int f, int r) sq, Color c)
    {
        for (int row = 0; row < 8; row++)
        for (int col = 0; col < 8; col++)
            if (ToBoard(row, col) == sq)
                _rowOverlays[row, col].style.backgroundColor = c;
    }

    private void Dot((int f, int r) sq)
    {
        for (int row = 0; row < 8; row++)
        for (int col = 0; col < 8; col++)
            if (ToBoard(row, col) == sq)
            {
                var ov = _rowOverlays[row, col];
                var dot = new VisualElement();
                dot.pickingMode = PickingMode.Ignore;
                dot.style.width = Length.Percent(28); dot.style.height = Length.Percent(28);
                UiKit.Radius(dot, 999);
                dot.style.backgroundColor = new Color(0, 0, 0, 0.22f);
                ov.Add(dot);
            }
    }

    // A press records the square; a drag past a threshold picks the piece up; release either drops it
    // (drag) or, if it barely moved, is treated as a click/tap (select then move).
    // --- hooks for the automated interaction harness (drive states, then screenshot) ---
    public void DebugSelect(string sq)
    {
        var game = SafeGame(_fen);
        if (game != null) Select(game, Sq(sq));
    }

    public void DebugAnnotate()
    {
        _annSquares.Clear(); _annArrows.Clear();
        _annSquares.Add(("d4", AnnRed));
        _annSquares.Add(("e5", AnnGreen));
        _annSquares.Add(("c6", AnnBlue));
        _annArrows.Add(("g1", "f3", AnnGreen));
        _annArrows.Add(("e2", "e4", AnnRed));
        RefreshAnnotations();
    }

    public void DebugPromotion(bool white) => ShowPromotion("e8", white, _ => { });

    public void DebugPremove(string from, string to)
    {
        _premove = (from, to);
        Render(_fen, false, _lastMove, _whiteBottom);
    }

    public void DebugClickMove(string from, string to)
    {
        // Exercise the true click-to-move path: tap the from-square (select), then tap the target.
        var (ff, fr) = Sq(from);
        var (tf, tr) = Sq(to);
        var (frow, fcol) = ScreenOf(ff, fr);
        var (trow, tcol) = ScreenOf(tf, tr);
        OnCellClicked(frow, fcol);
        OnCellClicked(trow, tcol);
    }

    private void OnPointerDown(PointerDownEvent e, int row, int col)
    {
        if (e.button == 1) { _rDown = (row, col); Root.CapturePointer(e.pointerId); return; } // right = annotate
        ClearAnnotations(); // a left press clears arrows/highlights (chess.com)

        _downRC = (row, col);
        _downPos = (Vector2)e.position;
        _dragging = false;
        _dragFrom = null;

        if (SquareClickHandler == null && _canMove && KaissaSettings.DragToMove)
        {
            var (f, r) = ToBoard(row, col);
            var game = SafeGame(_fen);
            if (game != null && IsOwnPiece(game, f, r))
                _dragFrom = Uci((f, r));
        }
        Root.CapturePointer(e.pointerId);
    }

    private void OnPointerMove(PointerMoveEvent e)
    {
        if (_downRC == null) return;
        if (_dragFrom != null && !_dragging && Vector2.Distance((Vector2)e.position, _downPos) > 6f)
        {
            _dragging = true;
            StartGhost(_dragFrom);
        }
        if (_dragging) MoveGhost((Vector2)e.position);
    }

    private void OnPointerUp(PointerUpEvent e)
    {
        if (_rDown is { } rd) // finish a right-click annotation
        {
            Root.ReleasePointer(e.pointerId);
            var up = CellUnder((Vector2)e.position) ?? rd;
            var color = AnnColor(e.altKey, e.shiftKey || e.ctrlKey);
            var (df, dr) = ToBoard(rd.row, rd.col); string fromSq = Uci((df, dr));
            var (uf, ur) = ToBoard(up.row, up.col); string toSq = Uci((uf, ur));
            if (up == rd) ToggleSquare(fromSq, color);
            else _annArrows.Add((fromSq, toSq, color));
            RefreshAnnotations();
            _rDown = null;
            return;
        }

        if (_downRC is not { } rc) return;
        Root.ReleasePointer(e.pointerId);

        if (_dragging)
        {
            EndGhost();
            var up = CellUnder((Vector2)e.position) ?? rc;
            var (tf, tr) = ToBoard(up.row, up.col);
            string to = Uci((tf, tr));
            if (to == _dragFrom || !AttemptMove(_dragFrom, to, e.altKey))
                Render(_fen, _canMove, _lastMove, _whiteBottom); // same square / illegal drop snaps back
            _dragging = false; _dragFrom = null; _downRC = null;
            return;
        }

        _dragFrom = null; _downRC = null;
        OnCellClicked(rc.row, rc.col, e.altKey); // a tap
    }

    // Applies from->to if legal, opening a promotion picker when needed (auto-queen honored; hold Alt
    // to under-promote when auto-queen is on). Returns true if it made or started a move.
    private bool AttemptMove(string from, string to, bool alt)
    {
        var game = SafeGame(_fen);
        if (game == null) return false;
        string baseUci = from + to;
        var legal = game.LegalUciMoves();
        if (legal.Contains(baseUci)) { ClearAnnotations(); _onMove?.Invoke(baseUci); return true; }

        bool isPromo = legal.Any(m => m.Length == 5 && m.StartsWith(baseUci, StringComparison.Ordinal));
        if (isPromo)
        {
            ClearAnnotations();
            if (KaissaSettings.AutoQueen && !alt) { _onMove?.Invoke(baseUci + "q"); return true; }
            bool white = game.SideToMove == Side.White;
            ShowPromotion(to, white, p => _onMove?.Invoke(baseUci + p));
            return true;
        }
        return false;
    }

    private void ShowPromotion(string toSquare, bool white, Action<char> pick)
    {
        var dim = new VisualElement();
        dim.style.position = Position.Absolute;
        dim.style.left = 0; dim.style.top = 0; dim.style.right = 0; dim.style.bottom = 0;
        dim.style.backgroundColor = new Color(0, 0, 0, 0.55f);
        dim.style.alignItems = Align.Center; dim.style.justifyContent = Justify.Center;

        var panel = new VisualElement();
        panel.style.flexDirection = FlexDirection.Row;
        panel.style.backgroundColor = UiKit.Panel;
        UiKit.Pad(panel, 8); UiKit.Radius(panel, 10);
        foreach (char p in new[] { 'q', 'r', 'b', 'n' })
        {
            char pc = p;
            var b = new Label(Pieces[char.ToUpperInvariant(pc)]);
            b.style.fontSize = 44; b.style.color = white ? UiKit.Hex(0xf7, 0xf7, 0xf2) : UiKit.Hex(0x2b, 0x2b, 0x2b);
            b.style.unityTextOutlineWidth = 1.4f;
            b.style.unityTextOutlineColor = white ? UiKit.Hex(0x33, 0x33, 0x33) : UiKit.Hex(0xe8, 0xe8, 0xe8);
            UiKit.Pad(b, 4, 10, 4, 10);
            b.RegisterCallback<ClickEvent>(_ => { Root.Remove(dim); pick(pc); });
            panel.Add(b);
        }
        dim.Add(panel);
        Root.Add(dim);
    }

    private (int row, int col) ScreenOf(int f, int r) => _whiteBottom ? (7 - r, f) : (r, 7 - f);

    private (int row, int col)? CellUnder(Vector2 pos)
    {
        for (int row = 0; row < 8; row++)
        for (int col = 0; col < 8; col++)
            if (_rowCells[row, col].worldBound.Contains(pos))
                return (row, col);
        return null;
    }

    private void StartGhost(string fromUci)
    {
        var (f, r) = Sq(fromUci);
        _ghostSrc = ScreenOf(f, r);
        var src = _rowPieces[_ghostSrc.row, _ghostSrc.col];
        _ghost = new Label(src.text) { pickingMode = PickingMode.Ignore };
        _ghost.style.position = Position.Absolute;
        _ghost.style.color = src.resolvedStyle.color;
        _ghost.style.fontSize = src.resolvedStyle.fontSize;
        _ghost.style.unityTextOutlineWidth = 1.4f;
        _ghost.style.unityTextOutlineColor = src.resolvedStyle.unityTextOutlineColor;
        Root.Add(_ghost);
        src.style.visibility = Visibility.Hidden;
    }

    private void MoveGhost(Vector2 panelPos)
    {
        if (_ghost == null) return;
        var local = Root.WorldToLocal(panelPos);
        float half = Root.worldBound.width / 16f; // half a square
        _ghost.style.left = local.x - half;
        _ghost.style.top = local.y - half;
    }

    private void EndGhost()
    {
        if (_ghost != null) { Root.Remove(_ghost); _ghost = null; }
        _rowPieces[_ghostSrc.row, _ghostSrc.col].style.visibility = Visibility.Visible;
    }

    private void OnCellClicked(int row, int col, bool alt = false)
    {
        var (f, r) = ToBoard(row, col);
        if (SquareClickHandler != null) { SquareClickHandler(Uci((f, r))); return; }

        if (!_canMove)
        {
            if (AllowPremove) PremoveClick(f, r);
            return;
        }
        var game = SafeGame(_fen);
        if (game == null) return;

        if (_sel is { } s)
        {
            if (s == (f, r)) { ClearSelection(); return; }
            if (AttemptMove(Uci(s), Uci((f, r)), alt)) return;
            // not a legal move - clicked another own piece -> reselect; else clear
            if (IsOwnPiece(game, f, r)) { Select(game, (f, r)); }
            else ClearSelection();
            return;
        }

        if (IsOwnPiece(game, f, r))
            Select(game, (f, r));
    }

    // Queue (or clear) a premove during the opponent's turn: click your piece, then its target.
    private void PremoveClick(int f, int r)
    {
        var game = SafeGame(_fen);
        if (game == null) return;
        bool playerIsWhite = game.SideToMove != Side.White; // the player is the side NOT to move now
        char p = new AttackBoardLite(_fen).At(f, r);

        if (_premoveSel is { } sel)
        {
            if (sel == (f, r)) { _premoveSel = null; _premove = null; Render(_fen, false, _lastMove, _whiteBottom); return; }
            _premove = (Uci(sel), Uci((f, r)));
            _premoveSel = null;
            Render(_fen, false, _lastMove, _whiteBottom);
        }
        else if (p != '\0' && char.IsUpper(p) == playerIsWhite)
        {
            _premoveSel = (f, r);
            _premove = null;
            Render(_fen, false, _lastMove, _whiteBottom);
        }
        else { _premove = null; Render(_fen, false, _lastMove, _whiteBottom); }
    }

    private void Select(ChessGame game, (int f, int r) sq)
    {
        // repaint to clear old dots/highlights, then mark selection + legal targets
        Render(_fen, _canMove, _lastMove, _whiteBottom);
        _sel = sq;
        Highlight(sq, Sel);
        foreach (var m in game.LegalUciMoves())
            if (m.Substring(0, 2) == Uci(sq))
                Dot(Sq(m.Substring(2, 2)));
    }

    private void ClearSelection()
    {
        _sel = null;
        Render(_fen, _canMove, _lastMove, _whiteBottom);
    }

    private static bool IsOwnPiece(ChessGame game, int f, int r)
    {
        var b = new AttackBoardLite(game.Fen);
        char p = b.At(f, r);
        if (p == '\0') return false;
        bool white = char.IsUpper(p);
        return white == (game.SideToMove == Side.White);
    }

    private static ChessGame SafeGame(string fen)
    {
        try { return ChessGame.FromFen(fen); } catch { return null; }
    }

    private static (int f, int r) Sq(string s) => (s[0] - 'a', s[1] - '1');
    private static string Uci((int f, int r) sq) => $"{(char)('a' + sq.f)}{(char)('1' + sq.r)}";

    // Minimal FEN piece reader (independent of the rules lib, like AttackBoard).
    private sealed class AttackBoardLite
    {
        private readonly char[,] _p = new char[8, 8];
        public AttackBoardLite(string fen)
        {
            var rows = fen.Split(' ')[0].Split('/');
            for (int i = 0; i < rows.Length && i < 8; i++)
            {
                int rank = 7 - i, file = 0;
                foreach (char c in rows[i])
                {
                    if (char.IsDigit(c)) file += c - '0';
                    else if (file < 8) _p[file++, rank] = c;
                }
            }
        }
        public char At(int f, int r) => _p[f, r];
    }
}
