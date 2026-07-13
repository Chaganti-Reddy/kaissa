using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Analysis board, rebuilt to mirror chess.com's analysis surface: play and branch any line, an
// evaluation bar, several engine lines at once (MultiPV) that you can click to explore, a best-move
// arrow (and an optional threat arrow), a clickable move list, the opening name, load-a-FEN and
// copy-FEN/PGN, and play-vs-computer from the current position. Works on the 2D or 3D board and uses
// the shared full-strength analysis engine. Right-click arrows/highlights work as on every board.
public sealed class AnalysisController : MonoBehaviour
{
    private readonly AnalysisSession _session = new();
    private IBoardView _board;
    private VisualElement _boardHost;
    private PieceAudio _audio;
    private KaissaAnalysis _engine;
    private OpeningBook _book;
    private CancellationTokenSource _evalCts;
    private bool _whiteBottom = true;
    private bool _threatsOn;
    private string _lastMove;
    private const int Depth = 18;
    private const int Lines = 3;

    private IReadOnlyList<AnalysisLine> _current = Array.Empty<AnalysisLine>();

    private VisualElement _root, _evalFill, _linesBody, _movesBody;
    private Label _evalText, _openingLabel, _depthLabel;
    private TextField _fenField;
    private Button _threatsBtn;

    // ---- board editor (2D) state ----
    private VisualElement _editorHost, _paletteRow;
    private Label _editStatus;
    private bool _editing;
    private readonly char[,] _edit = new char[8, 8]; // [file, rank]; '\0' = empty
    private char _paletteChar = 'P';                 // currently selected stamp; '\0' = eraser
    private bool _editWhiteToMove = true;
    private bool _cWK, _cWQ, _cBK, _cBQ;             // castling availability

    private static readonly Color BestArrow = new(0.36f, 0.62f, 0.86f, 0.85f);
    private static readonly Color ThreatArrow = new(0.86f, 0.30f, 0.28f, 0.85f);

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }

        _audio = PieceAudio.Attach(gameObject);
        try { _book = OpeningBook.LoadDefault(); } catch (Exception e) { Debug.LogWarning(e.Message); }

        var doc = gameObject.AddComponent<UIDocument>();
        doc.panelSettings = Resources.Load<PanelSettings>("KaissaPanel");
        StartCoroutine(Build(doc));
    }

    private IEnumerator Build(UIDocument doc)
    {
        yield return null;
        _root = doc.rootVisualElement;
        _root.Clear();
        _root.style.flexDirection = FlexDirection.Row; _root.style.flexGrow = 1; _root.style.backgroundColor = UiKit.Bg;
        _root.Add(UiKit.NavRail("Analysis"));
        _root.Add(BuildCenter());
        _root.Add(BuildRightRail());
        _root.Add(_editorHost);

        _board = BoardMount.Create(gameObject, _boardHost, _root, uci => OnMove(uci), _audio);
        if (!string.IsNullOrEmpty(AnalysisRoute.Fen))
        {
            _session.LoadFen(AnalysisRoute.Fen);
            AnalysisRoute.Fen = null;
        }
        StartCoroutine(StartEngine());
        RenderCurrent();

        if (Environment.GetCommandLineArgs().Contains("-kaissa-analysistest"))
            StartCoroutine(AutoDemo());
    }

    // ---------------- layout ----------------

    private VisualElement BuildCenter()
    {
        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 18, 24, 18, 24);
        _openingLabel = UiKit.Text_("Analysis", 24, UiKit.Text, bold: true);
        center.Add(_openingLabel);
        _depthLabel = UiKit.Text_("", 12, UiKit.Mute, bold: true);
        _depthLabel.style.marginBottom = 8;
        center.Add(_depthLabel);

        // eval bar + board side by side
        var boardRow = UiKit.Row();
        boardRow.style.alignItems = Align.Center;
        var bar = new VisualElement();
        bar.style.width = 16; bar.style.height = 480; bar.style.marginRight = 10; bar.style.flexShrink = 0;
        bar.style.flexDirection = FlexDirection.ColumnReverse; bar.style.overflow = Overflow.Hidden;
        bar.style.backgroundColor = UiKit.Hex(0x40, 0x3d, 0x39); UiKit.Radius(bar, 4);
        _evalFill = new VisualElement();
        _evalFill.style.width = Length.Percent(100); _evalFill.style.height = Length.Percent(50);
        _evalFill.style.backgroundColor = UiKit.Hex(0xf4, 0xf4, 0xf4);
        bar.Add(_evalFill);
        boardRow.Add(bar);
        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        boardRow.Add(_boardHost);
        center.Add(boardRow);

        var nav = UiKit.Row(
            NavBtn("|<", _session.GoToStart), NavBtn("<", () => _session.StepBack()),
            NavBtn(">", () => _session.StepForward()), NavBtn(">|", _session.GoToEnd),
            NavBtn("Flip", () => _whiteBottom = !_whiteBottom), NavBtn("Reset", Reset));
        nav.style.marginTop = 12;
        center.Add(nav);
        return center;
    }

    private VisualElement BuildRightRail()
    {
        var rail = new VisualElement();
        rail.style.width = 360; UiKit.Pad(rail, 18, 24, 18, 8);

        var evalP = Panel(); UiKit.Pad(evalP, 14, 16, 14, 16);
        _evalText = UiKit.Text_("Evaluation -", 22, UiKit.Text, bold: true);
        evalP.Add(_evalText);
        rail.Add(evalP);

        var linesP = Panel(); linesP.style.marginTop = 12; UiKit.Pad(linesP, 12, 14, 12, 14);
        linesP.Add(UiKit.Text_("Engine lines", 12, UiKit.Mute, bold: true));
        _linesBody = new VisualElement(); _linesBody.style.marginTop = 6;
        linesP.Add(_linesBody);
        rail.Add(linesP);

        var movesP = Panel(); movesP.style.marginTop = 12; UiKit.Pad(movesP, 12, 14, 12, 14);
        movesP.Add(UiKit.Text_("Moves", 12, UiKit.Mute, bold: true));
        var scroll = new ScrollView(); scroll.style.maxHeight = 200; scroll.style.marginTop = 4;
        _movesBody = scroll.contentContainer;
        movesP.Add(scroll);
        rail.Add(movesP);

        var toolsP = Panel(); toolsP.style.marginTop = 12; UiKit.Pad(toolsP, 12, 14, 12, 14);
        toolsP.Add(UiKit.Text_("Position", 12, UiKit.Mute, bold: true));
        _fenField = new TextField { value = "" };
        _fenField.style.marginTop = 6; _fenField.style.marginBottom = 6;
        _fenField.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) LoadFenFromField(); });
        toolsP.Add(_fenField);
        var row1 = UiKit.Row(Tool("Load FEN", LoadFenFromField), Tool("Copy FEN", CopyFen), Tool("Copy PGN", CopyPgn));
        toolsP.Add(row1);
        var row2 = UiKit.Row();
        row2.style.marginTop = 6;
        _threatsBtn = Tool("Threats: off", ToggleThreats);
        row2.Add(_threatsBtn);
        row2.Add(Tool("Play from here", PlayFromHere));
        toolsP.Add(row2);
        var row3 = UiKit.Row(); row3.style.marginTop = 6;
        row3.Add(Tool("Edit position", EnterEdit));
        toolsP.Add(row3);
        rail.Add(toolsP);
        _editorHost = new VisualElement();
        _editorHost.style.position = Position.Absolute;
        _editorHost.style.left = 0; _editorHost.style.top = 0; _editorHost.style.right = 0; _editorHost.style.bottom = 0;
        _editorHost.pickingMode = PickingMode.Ignore;
        return rail;
    }

    private Button Tool(string label, Action onClick)
    {
        var b = UiKit.Ghost(label, onClick, 12);
        b.style.marginRight = 5; UiKit.Pad(b, 6, 10, 6, 10);
        return b;
    }

    private static VisualElement Panel()
    {
        var p = new VisualElement();
        p.style.backgroundColor = UiKit.Panel;
        p.style.borderTopWidth = p.style.borderBottomWidth = p.style.borderLeftWidth = p.style.borderRightWidth = 1;
        p.style.borderTopColor = p.style.borderBottomColor = p.style.borderLeftColor = p.style.borderRightColor = UiKit.Line;
        UiKit.Radius(p, 12);
        return p;
    }

    private VisualElement NavBtn(string label, Action onClick)
    {
        var b = UiKit.Ghost(label, () => { onClick(); RenderCurrent(); }, 13);
        b.style.marginLeft = 3; b.style.marginRight = 3; b.style.minWidth = 46;
        return b;
    }

    // ---------------- actions ----------------

    private void OnMove(string uci)
    {
        if (_session.Play(uci)) { _lastMove = uci; RenderCurrent(); }
    }

    private void Reset()
    {
        _session.LoadFen(ChessGame.StartFen);
        _lastMove = null;
        if (_fenField != null) _fenField.value = "";
    }

    private void LoadFenFromField()
    {
        var fen = _fenField?.value?.Trim();
        if (string.IsNullOrEmpty(fen)) return;
        try { _ = ChessGame.FromFen(fen); } // validate before loading
        catch { _evalText.text = "Invalid FEN"; return; }
        _session.LoadFen(fen);
        _lastMove = null;
        RenderCurrent();
    }

    private void CopyFen() => GUIUtility.systemCopyBuffer = _session.CurrentFen;

    private void CopyPgn() => GUIUtility.systemCopyBuffer = BuildPgn();

    // ---------------- board editor (2D) ----------------
    // Set up a position by hand: pick a piece from the palette and click squares to stamp it, or pick
    // the eraser to clear. Toggle side-to-move and castling rights, clear or reset the board, then Apply
    // to validate and load the FEN. Uses the 2D board's square-click reporting; the 3D board has no
    // square picking, so editing prompts the player to switch to the 2D board.
    private void EnterEdit()
    {
        _editing = true;
        _evalCts?.Cancel();
        ParseFenIntoEdit(_session.CurrentFen);
        _board.SetEngineArrows(null);
        _board.SquareClickHandler = OnEditSquare;
        BuildEditorPanel();
        RenderEditBoard();
    }

    private void ExitEdit(bool apply)
    {
        if (apply)
        {
            string fen = BuildEditFen();
            try { _ = ChessGame.FromFen(fen); }
            catch { if (_editStatus != null) _editStatus.text = "Illegal position - need both kings and no pawns on the back ranks."; return; }
            _session.LoadFen(fen);
            _lastMove = null;
            if (_fenField != null) _fenField.value = fen;
        }
        _editing = false;
        _board.SquareClickHandler = null;
        _editorHost.Clear();
        RenderCurrent();
    }

    private void OnEditSquare(string sq)
    {
        if (!_editing || string.IsNullOrEmpty(sq) || sq.Length < 2) return;
        int f = sq[0] - 'a', r = sq[1] - '1';
        if (f < 0 || f > 7 || r < 0 || r > 7) return;
        _edit[f, r] = _paletteChar; // '\0' from the eraser clears the square
        RenderEditBoard();
    }

    private void RenderEditBoard()
    {
        _board.Render(BuildEditFen(placeholderLegal: true), canMove: false, lastMove: null, whiteBottom: _whiteBottom);
    }

    private void ParseFenIntoEdit(string fen)
    {
        for (int f = 0; f < 8; f++) for (int r = 0; r < 8; r++) _edit[f, r] = '\0';
        var parts = fen.Split(' ');
        var rows = parts[0].Split('/');
        for (int i = 0; i < rows.Length && i < 8; i++)
        {
            int rank = 7 - i, file = 0;
            foreach (char c in rows[i])
            {
                if (char.IsDigit(c)) file += c - '0';
                else if (file < 8) _edit[file++, rank] = c;
            }
        }
        _editWhiteToMove = parts.Length < 2 || parts[1] != "b";
        string cr = parts.Length > 2 ? parts[2] : "KQkq";
        _cWK = cr.Contains('K'); _cWQ = cr.Contains('Q'); _cBK = cr.Contains('k'); _cBQ = cr.Contains('q');
    }

    // Builds a FEN from the editor state. When placeholderLegal is set (for rendering the in-progress
    // board), castling is dropped to "-" so Board2D never trips on rights without the matching rook/king.
    private string BuildEditFen(bool placeholderLegal = false)
    {
        var sb = new System.Text.StringBuilder();
        for (int rank = 7; rank >= 0; rank--)
        {
            int empty = 0;
            for (int file = 0; file < 8; file++)
            {
                char c = _edit[file, rank];
                if (c == '\0') { empty++; continue; }
                if (empty > 0) { sb.Append(empty); empty = 0; }
                sb.Append(c);
            }
            if (empty > 0) sb.Append(empty);
            if (rank > 0) sb.Append('/');
        }
        string castle = placeholderLegal ? "-" : Castle();
        return $"{sb} {(_editWhiteToMove ? "w" : "b")} {castle} - 0 1";
    }

    private string Castle()
    {
        string c = (_cWK ? "K" : "") + (_cWQ ? "Q" : "") + (_cBK ? "k" : "") + (_cBQ ? "q" : "");
        return c.Length == 0 ? "-" : c;
    }

    private void BuildEditorPanel()
    {
        _editorHost.Clear();
        var panel = Panel();
        panel.style.position = Position.Absolute;
        panel.style.right = 22; panel.style.top = 84; panel.style.width = 320;
        UiKit.Pad(panel, 16);
        panel.Add(UiKit.Text_("Edit position", 14, UiKit.Text, bold: true));
        var hint = UiKit.Text_("Pick a piece, click squares to place. Eraser clears.", 12, UiKit.Dim);
        hint.style.whiteSpace = WhiteSpace.Normal; hint.style.marginTop = 4; hint.style.marginBottom = 8;
        panel.Add(hint);

        var white = UiKit.Row(); white.style.flexWrap = Wrap.Wrap;
        foreach (char c in "KQRBNP") white.Add(PieceButton(c));
        white.Add(EraserButton());
        panel.Add(white);
        var black = UiKit.Row(); black.style.flexWrap = Wrap.Wrap; black.style.marginTop = 6;
        foreach (char c in "kqrbnp") black.Add(PieceButton(c));
        panel.Add(black);

        var sideRow = UiKit.Row(); sideRow.style.marginTop = 12;
        sideRow.Add(UiKit.Text_("To move", 12, UiKit.Mute, bold: true));
        var wPill = TogglePill("White", _editWhiteToMove, () => { _editWhiteToMove = true; BuildEditorPanel(); });
        var bPill = TogglePill("Black", !_editWhiteToMove, () => { _editWhiteToMove = false; BuildEditorPanel(); });
        wPill.style.marginLeft = 8; sideRow.Add(wPill); sideRow.Add(bPill);
        panel.Add(sideRow);

        var castRow = UiKit.Row(); castRow.style.marginTop = 8; castRow.style.flexWrap = Wrap.Wrap;
        castRow.Add(UiKit.Text_("Castling", 12, UiKit.Mute, bold: true));
        var wk = TogglePill("W O-O", _cWK, () => { _cWK = !_cWK; BuildEditorPanel(); }); wk.style.marginLeft = 8; castRow.Add(wk);
        castRow.Add(TogglePill("W O-O-O", _cWQ, () => { _cWQ = !_cWQ; BuildEditorPanel(); }));
        castRow.Add(TogglePill("B O-O", _cBK, () => { _cBK = !_cBK; BuildEditorPanel(); }));
        castRow.Add(TogglePill("B O-O-O", _cBQ, () => { _cBQ = !_cBQ; BuildEditorPanel(); }));
        panel.Add(castRow);

        var actions = UiKit.Row(); actions.style.marginTop = 12; actions.style.flexWrap = Wrap.Wrap;
        actions.Add(Tool("Clear", () => { for (int f = 0; f < 8; f++) for (int r = 0; r < 8; r++) _edit[f, r] = '\0'; RenderEditBoard(); }));
        actions.Add(Tool("Start", () => { ParseFenIntoEdit(ChessGame.StartFen); RenderEditBoard(); }));
        panel.Add(actions);

        _editStatus = UiKit.Text_("", 12, UiKit.Danger, bold: true);
        _editStatus.style.whiteSpace = WhiteSpace.Normal; _editStatus.style.marginTop = 8;
        panel.Add(_editStatus);

        var confirm = UiKit.Row(); confirm.style.marginTop = 8;
        var apply = UiKit.Primary("Apply", () => ExitEdit(apply: true), 13); apply.style.marginRight = 8;
        confirm.Add(apply);
        confirm.Add(UiKit.Ghost("Cancel", () => ExitEdit(apply: false), 13));
        panel.Add(confirm);

        _editorHost.Add(panel);
    }

    private VisualElement PieceButton(char c)
    {
        var b = new VisualElement { name = "pal_" + c };
        b.style.width = 40; b.style.height = 40; b.style.marginRight = 5; b.style.marginBottom = 5;
        b.style.backgroundColor = UiKit.Hex(0xb5, 0x88, 0x63);
        UiKit.Radius(b, 6);
        b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        var edge = _paletteChar == c ? UiKit.Gold : UiKit.Line;
        b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = edge;
        var tex = PieceArt.Get(c);
        if (tex != null)
        {
            var img = new VisualElement { pickingMode = PickingMode.Ignore };
            img.style.position = Position.Absolute; img.style.left = 3; img.style.top = 3; img.style.right = 3; img.style.bottom = 3;
            img.style.backgroundImage = new StyleBackground(tex);
            img.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            b.Add(img);
        }
        b.RegisterCallback<ClickEvent>(_ => { _paletteChar = c; BuildEditorPanel(); });
        UiKit.Interactive(b, 1.06f);
        return b;
    }

    private VisualElement EraserButton()
    {
        var b = UiKit.Ghost("Erase", () => { _paletteChar = '\0'; BuildEditorPanel(); }, 12);
        b.style.height = 40; b.style.marginRight = 5; b.style.marginBottom = 5;
        b.style.backgroundColor = _paletteChar == '\0' ? UiKit.Green : UiKit.Panel2;
        return b;
    }

    private Button TogglePill(string label, bool on, Action onClick)
    {
        var b = UiKit.Ghost(label, onClick, 12);
        b.style.marginRight = 6; b.style.marginBottom = 4; UiKit.Pad(b, 6, 10, 6, 10);
        b.style.backgroundColor = on ? UiKit.Green : UiKit.Panel2;
        return b;
    }

    private void PlayFromHere()
    {
        EndgameRoute.Fen = _session.CurrentFen;
        SceneTransition.Go("Play");
    }

    private void ToggleThreats()
    {
        _threatsOn = !_threatsOn;
        _threatsBtn.text = _threatsOn ? "Threats: on" : "Threats: off";
        Evaluate(force: true); // re-eval the same position to add/remove the threat arrow
    }

    private void PlayLineFromEngine(int index)
    {
        if (index < 0 || index >= _current.Count) return;
        _session.PlayLine(_current[index].Moves);
        _lastMove = _current[index].Moves.Count > 0 ? _current[index].Moves[^1] : _lastMove;
        RenderCurrent();
    }

    // ---------------- render ----------------

    private void RenderCurrent()
    {
        if (_editing) return; // the editor drives the board directly while open
        _board.Render(_session.CurrentFen, canMove: true, lastMove: _lastMove, whiteBottom: _whiteBottom);
        UpdateOpening();
        RebuildMoves();
        Evaluate();
    }

    private void UpdateOpening()
    {
        var named = _book?.Name(_session.CurrentFen);
        _openingLabel.text = named != null ? $"{named.Eco}  {named.Name}" : "Analysis";
    }

    private void RebuildMoves()
    {
        if (_movesBody == null) return;
        _movesBody.Clear();
        var san = _session.LineSan();
        for (int i = 0; i < san.Count; i += 2)
        {
            int wply = i + 1, bply = i + 2;
            var num = Cell($"{i / 2 + 1}.", 34, UiKit.Mute);
            var wc = PlyCell(san[i], wply);
            var bc = i + 1 < san.Count ? PlyCell(san[i + 1], bply) : Cell("", 90, UiKit.Text);
            var row = UiKit.Row(num, wc, bc);
            if ((i / 2) % 2 == 1) row.style.backgroundColor = UiKit.Panel3;
            UiKit.Pad(row, 5, 10, 5, 10);
            _movesBody.Add(row);
        }
    }

    private Label PlyCell(string san, int ply)
    {
        var l = Cell(san, 90, ply == _session.Ply ? UiKit.Gold : UiKit.Text);
        l.name = "movecell";
        l.RegisterCallback<ClickEvent>(_ => { if (_editing) return; _session.GoToPly(ply); _lastMove = null; RenderCurrent(); });
        UiKit.Interactive(l, 1.05f);
        return l;
    }

    private static Label Cell(string s, float w, Color c) { var l = UiKit.Text_(s, 14, c); l.style.width = w; return l; }

    // ---------------- engine ----------------

    private IEnumerator StartEngine()
    {
        if (!EngineHub.Available) { _evalText.text = "Engine not found"; yield break; }
        var task = EngineHub.AnalysisEngineAsync();
        while (!task.IsCompleted) yield return null;
        if (task.IsFaulted) { _evalText.text = "Engine failed"; Debug.LogError(task.Exception); yield break; }
        _engine = KaissaAnalysis.Attach(task.Result);
        Evaluate();
    }

    private string _evalFen; // the position currently being (or already) evaluated

    private void Evaluate(bool force = false)
    {
        if (_engine == null) return;
        string fen = _session.CurrentFen;
        // A depth-18 search takes a couple of seconds; the board can trigger a redundant render (and
        // thus a redundant Evaluate) for the SAME position, which would cancel the in-flight search
        // before it finishes and leave the eval permanently blank. Skip if we are already on this FEN.
        if (!force && fen == _evalFen) return;
        _evalFen = fen;
        _depthLabel.text = $"Stockfish - depth {Depth} - {Lines} lines";
        RunEval(fen);
    }

    private async void RunEval(string fen)
    {
        _evalCts?.Cancel();
        _evalCts = new CancellationTokenSource();
        var ct = _evalCts.Token;
        try
        {
            var lines = await _engine.EvaluateLinesAsync(fen, Depth, Lines, ct);
            if (ct.IsCancellationRequested) return;
            _current = lines;
            RenderEval(fen, lines);
            await UpdateArrowsAsync(fen, lines, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            if (_evalFen == fen) _evalFen = null; // let a real failure be retried
            Debug.LogError(e);
        }
    }

    private void RenderEval(string fen, IReadOnlyList<AnalysisLine> lines)
    {
        bool whiteToMove = IsWhiteToMove(fen);
        var top = lines.Count > 0 ? lines[0] : null;
        _evalText.text = top == null || string.IsNullOrEmpty(top.Score) ? "Evaluation -" : $"Evaluation  {top.Score}";
        if (top != null)
        {
            int whiteCp = whiteToMove ? top.Centipawns : -top.Centipawns;
            _evalFill.style.height = Length.Percent((float)Kaissa.Training.Play.AccuracyModel.WinPercent(whiteCp));
        }

        _linesBody.Clear();
        if (lines.Count == 0) { _linesBody.Add(UiKit.Text_("-", 12, UiKit.Mute)); return; }
        for (int i = 0; i < lines.Count; i++)
        {
            int idx = i;
            var l = lines[i];
            var row = UiKit.Row();
            row.name = "engineline";
            UiKit.Pad(row, 6, 8, 6, 8); UiKit.Radius(row, 6);
            var score = UiKit.Text_(l.Score, 13, UiKit.Gold, bold: true); score.style.minWidth = 52;
            row.Add(score);
            var moves = UiKit.Text_(LineSan(fen, l.Moves, 8), 13, UiKit.Dim);
            moves.style.whiteSpace = WhiteSpace.Normal; moves.style.flexGrow = 1;
            row.Add(moves);
            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = UiKit.Panel2);
            row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = new Color(0, 0, 0, 0));
            row.RegisterCallback<ClickEvent>(_ => PlayLineFromEngine(idx));
            UiKit.Interactive(row, 1.02f);
            _linesBody.Add(row);
        }
    }

    private async System.Threading.Tasks.Task UpdateArrowsAsync(string fen, IReadOnlyList<AnalysisLine> lines, CancellationToken ct)
    {
        var arrows = new List<(string from, string to, Color color)>();
        var best = lines.Count > 0 ? lines[0].BestMove : null;
        if (!string.IsNullOrEmpty(best) && best.Length >= 4)
            arrows.Add((best.Substring(0, 2), best.Substring(2, 2), BestArrow));

        if (_threatsOn)
        {
            var threat = await ThreatMoveAsync(fen, ct);
            if (ct.IsCancellationRequested) return;
            if (!string.IsNullOrEmpty(threat) && threat.Length >= 4)
                arrows.Add((threat.Substring(0, 2), threat.Substring(2, 2), ThreatArrow));
        }
        _board.SetEngineArrows(arrows);
    }

    // The opponent's best move if it were their turn - a "what are they threatening" hint. Computed by
    // flipping the side to move (a null move) and asking the engine for the best reply. Best-effort.
    private async System.Threading.Tasks.Task<string> ThreatMoveAsync(string fen, CancellationToken ct)
    {
        try
        {
            var flipped = FlipSideToMove(fen);
            var lines = await _engine.EvaluateLinesAsync(flipped, 14, 1, ct);
            return lines.Count > 0 ? lines[0].BestMove : null;
        }
        catch { return null; }
    }

    private static string FlipSideToMove(string fen)
    {
        var p = fen.Split(' ');
        if (p.Length < 2) return fen;
        p[1] = p[1] == "w" ? "b" : "w";
        if (p.Length > 3) p[3] = "-"; // clear en passant; the null move invalidates it
        return string.Join(' ', p);
    }

    private static string LineSan(string fen, IReadOnlyList<string> uci, int maxPlies)
    {
        var g = ChessGame.FromFen(fen);
        var sb = new StringBuilder();
        bool whiteToMove = g.SideToMove == Side.White;
        int moveNo = int.TryParse(fen.Split(' ').ElementAtOrDefault(5), out var mn) ? mn : 1;
        for (int i = 0; i < uci.Count && i < maxPlies; i++)
        {
            if (whiteToMove) sb.Append(moveNo).Append(". ");
            else if (i == 0) sb.Append(moveNo).Append("... ");
            sb.Append(g.SanForUci(uci[i]) ?? uci[i]).Append(' ');
            if (!g.TryMakeMove(uci[i])) break;
            if (!whiteToMove) moveNo++;
            whiteToMove = !whiteToMove;
        }
        return sb.ToString().Trim();
    }

    private string BuildPgn()
    {
        var san = _session.LineSan();
        var sb = new StringBuilder();
        sb.Append("[Event \"Kaissa analysis\"]\n[Result \"*\"]\n");
        if (_session.StartFen != ChessGame.StartFen)
            sb.Append($"[FEN \"{_session.StartFen}\"]\n[SetUp \"1\"]\n");
        sb.Append('\n');
        for (int i = 0; i < san.Count; i++)
        {
            if (i % 2 == 0) sb.Append(i / 2 + 1).Append(". ");
            sb.Append(san[i]).Append(' ');
        }
        sb.Append('*');
        return sb.ToString();
    }

    private static bool IsWhiteToMove(string fen)
    {
        var p = fen.Split(' ');
        return p.Length < 2 || p[1] != "b";
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame) SceneTransition.Go("Menu");
        else if (kb.leftArrowKey.wasPressedThisFrame) { _session.StepBack(); RenderCurrent(); }
        else if (kb.rightArrowKey.wasPressedThisFrame) { _session.StepForward(); RenderCurrent(); }
        else if (kb.fKey.wasPressedThisFrame) { _whiteBottom = !_whiteBottom; RenderCurrent(); }
    }

    private void OnDestroy()
    {
        _evalCts?.Cancel();
        if (_engine != null) _ = _engine.DisposeAsync(); // no-op: shared engine, not owned
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    // ---------------- self-test ----------------

    private bool _recording, _pauseRec;
    private int _seq;

    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        string burst = System.IO.Path.Combine(dir, "burst");
        System.IO.Directory.CreateDirectory(dir);
        System.IO.Directory.CreateDirectory(burst);
        string tag = KaissaSettings.BoardView == 1 ? "3d" : "2d";
        KaissaSettings.AutoQueen = true;

        _recording = true;
        StartCoroutine(DenseRecord(burst, tag));

        yield return Shot(dir, tag, "warmup", 1.2f);

        // Play into a known opening via the real board input path (eval + lines + best-move arrow appear).
        foreach (var mv in new[] { "e2e4", "e7e5", "g1f3", "b8c6", "f1b5" })
        {
            _board.DebugClickMove(mv.Substring(0, 2), mv.Substring(2, 2));
            yield return new WaitForSeconds(0.5f);
        }
        yield return new WaitForSeconds(2.0f); // let the engine finish a depth-18 eval
        yield return Shot(dir, tag, "eval_lines", 0.8f);

        // Threats arrow on.
        UiAutomation.Click(_threatsBtn);
        yield return new WaitForSeconds(2.0f);
        yield return Shot(dir, tag, "threats", 0.8f);
        UiAutomation.Click(_threatsBtn);
        yield return new WaitForSeconds(0.5f);

        // Click an engine line to play it into the board.
        UiAutomation.Click(_root.Q("engineline"));
        yield return new WaitForSeconds(2.0f);
        yield return Shot(dir, tag, "played_line", 0.8f);

        // Move-list navigation: click an earlier ply, and the nav buttons.
        UiAutomation.Click(_root.Q("movecell"));
        yield return new WaitForSeconds(1.2f);
        yield return Shot(dir, tag, "ply_jump", 0.6f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "|<"));
        yield return new WaitForSeconds(0.6f);
        UiAutomation.Click(UiAutomation.FindButton(_root, ">|"));
        yield return new WaitForSeconds(1.5f);
        yield return Shot(dir, tag, "nav_end", 0.6f);

        // Load a FEN, then copy FEN/PGN (clipboard).
        _fenField.value = "r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R b KQkq - 3 3";
        UiAutomation.Click(UiAutomation.FindButton(_root, "Load FEN"));
        yield return new WaitForSeconds(2.0f);
        yield return Shot(dir, tag, "loaded_fen", 0.6f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "Copy FEN"));
        yield return new WaitForSeconds(0.3f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "Copy PGN"));
        yield return new WaitForSeconds(0.3f);

        // Board editor: open, load start, pick the white queen, stamp two squares (2D board tap), apply.
        // Works on 2D and 3D now that square-pick is wired for both; only the scripted stamp is 2D-only.
        UiAutomation.Click(UiAutomation.FindButton(_root, "Edit position"));
        yield return new WaitForSeconds(0.6f);
        yield return Shot(dir, tag, "editor_open", 0.6f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "Start"));
        yield return new WaitForSeconds(0.4f);
        UiAutomation.Click(_editorHost.Q("pal_Q"));
        yield return new WaitForSeconds(0.3f);
        if (_board is Board2D b2) { b2.DebugTapSquare("e5"); b2.DebugTapSquare("d5"); }
        yield return new WaitForSeconds(0.4f);
        yield return Shot(dir, tag, "editor_stamp", 0.6f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "Apply"));
        yield return new WaitForSeconds(1.8f);
        yield return Shot(dir, tag, "editor_applied", 0.6f);

        // Flip.
        UiAutomation.Click(UiAutomation.FindButton(_root, "Flip"));
        yield return new WaitForSeconds(1.5f);
        yield return Shot(dir, tag, "flip", 0.6f);

        _recording = false;
        yield return new WaitForSeconds(0.3f);
        Application.Quit();
    }

    private IEnumerator DenseRecord(string dir, string tag)
    {
        while (_recording)
        {
            if (!_pauseRec) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{tag}_{_seq:0000}.png")); _seq++; }
            yield return new WaitForSeconds(0.15f);
        }
    }

    private IEnumerator Shot(string dir, string tag, string label, float hold)
    {
        _pauseRec = true;
        yield return null;
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"an_{tag}_{label}.png"));
        yield return new WaitForSeconds(0.25f);
        _pauseRec = false;
        yield return new WaitForSeconds(Mathf.Max(0f, hold - 0.25f));
    }

    private static string ArgValue(string key)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == key) return args[i + 1];
        return null;
    }
}
