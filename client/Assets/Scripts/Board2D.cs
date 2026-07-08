using System;
using System.Collections.Generic;
using Kaissa.Chess.Rules;
using UnityEngine;
using UnityEngine.UIElements;

// A flat 2D chess board rendered in UI Toolkit, chess.com-style. Renders a position from a FEN,
// accepts click-to-move (select a piece, then a target), shows legal-move dots, and the selected /
// last-move / check highlights. Legal moves and promotion are validated against the rules core, so
// it reports only legal moves through onMove (UCI). Mirrors BoardInteractor's contract for the 2D
// board mode; the 3D board keeps using BoardInteractor.
public sealed class Board2D
{
    public readonly VisualElement Root = new();

    private readonly Action<string> _onMove;

    private string _fen;
    private bool _whiteBottom = true;
    private bool _canMove;
    private (int f, int r)? _sel;
    private string _lastMove;

    private static readonly Color Light = UiKit.Hex(0xeb, 0xec, 0xd0);
    private static readonly Color Dark = UiKit.Hex(0x73, 0x95, 0x52);
    private static readonly Color Sel = new(0.96f, 0.96f, 0.41f, 0.55f);   // yellow select/last-move
    private static readonly Color Check = new(0.85f, 0.20f, 0.20f, 0.75f);

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
    }

    private void Build()
    {
        // rows top→bottom; recomputed on flip in Render via cell colors + glyphs.
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
                cell.RegisterCallback<ClickEvent>(_ => OnCellClicked(rr, cc));

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
        bool whiteChecked, blackChecked;
        int wkF = -1, wkR = -1, bkF = -1, bkR = -1;

        for (int row = 0; row < 8; row++)
        for (int col = 0; col < 8; col++)
        {
            var (f, r) = ToBoard(row, col);
            var cell = _rowCells[row, col];
            bool dark = (f + r) % 2 == 0; // a1 (0,0) dark
            cell.style.backgroundColor = dark ? Dark : Light;

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

            _rowOverlays[row, col].Clear();
            _rowOverlays[row, col].style.backgroundColor = new Color(0, 0, 0, 0);
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

    private void OnCellClicked(int row, int col)
    {
        if (!_canMove) return;
        var (f, r) = ToBoard(row, col);
        var game = SafeGame(_fen);
        if (game == null) return;

        if (_sel is { } s)
        {
            if (s == (f, r)) { ClearSelection(); return; }
            string uci = Uci(s) + Uci((f, r));
            // promotion → default queen (auto-queen); a picker can refine later
            var legal = game.LegalUciMoves();
            string chosen = null;
            foreach (var m in legal)
                if (m == uci || m == uci + "q") { chosen = m; break; }
            if (chosen != null) { _onMove?.Invoke(chosen); return; }
            // clicked another own piece → reselect; else clear
            if (IsOwnPiece(game, f, r)) { Select(game, (f, r)); }
            else ClearSelection();
            return;
        }

        if (IsOwnPiece(game, f, r))
            Select(game, (f, r));
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
