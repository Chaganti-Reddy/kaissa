using System;
using System.Collections;
using System.Linq;
using Kaissa.Training;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Solo Chess (the chess.com single-player puzzle): every move must capture, no piece captures more than
// twice (twice-captured pieces turn dark and are stuck), and a king can never be taken - so it is forced
// to be the last piece. Clear the board to one piece to level up; more pieces each level, a king from
// level six. A wrong capture can dead-end the board - reset and try the same one again. 2D board only.
public sealed class SoloChessController : MonoBehaviour
{
    private Board2D _board;
    private SoloChess _solo;
    private string _placement;   // the starting board for the current level, for Reset
    private string _from;
    private int _level;          // = piece count of the current board
    private int _moves;
    private readonly System.Random _rng = new();

    private VisualElement _root, _overlayHost, _boardHost;
    private Label _phase, _movesLabel, _scoreLabel, _bestLabel, _feedback;

    private static readonly Color Sel = new(0.36f, 0.72f, 0.42f, 0.55f);

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }
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
        _root.Add(UiKit.NavRail("Solo Chess"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 18, 24, 18, 24);

        var head = UiKit.Row();
        head.style.width = 480; head.style.justifyContent = Justify.SpaceBetween; head.style.marginBottom = 8;
        _phase = UiKit.Text_("Capture down to one piece", 20, UiKit.Text, bold: true);
        _movesLabel = UiKit.Text_("", 20, UiKit.Gold, bold: true);
        head.Add(_phase); head.Add(_movesLabel);
        center.Add(head);

        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        _board = new Board2D(null);
        _board.Root.style.width = 480; _board.Root.style.height = 480;
        _boardHost.Add(_board.Root);
        center.Add(_boardHost);

        var ctrls = UiKit.Row(); ctrls.style.marginTop = 12;
        var reset = UiKit.Ghost("Reset board", ResetBoard, 14); reset.style.marginRight = 8;
        var skip = UiKit.Ghost("New board", NewBoard, 14);
        ctrls.Add(reset); ctrls.Add(skip);
        center.Add(ctrls);

        _feedback = UiKit.Text_("", 16, UiKit.Dim, bold: true);
        _feedback.style.marginTop = 8; _feedback.style.unityTextAlign = TextAnchor.MiddleCenter;
        UiKit.Pad(_feedback, 4, 12, 4, 12); UiKit.Radius(_feedback, 8);
        center.Add(_feedback);
        _root.Add(center);

        var rail = new VisualElement();
        rail.style.width = 300; UiKit.Pad(rail, 18, 24, 18, 8);
        var panel = Panel(); UiKit.Pad(panel, 16);
        panel.Add(UiKit.Text_("LEVEL", 11, UiKit.Mute, bold: true));
        _scoreLabel = UiKit.Text_("-", 40, UiKit.Text, bold: true);
        panel.Add(_scoreLabel);
        _bestLabel = UiKit.Text_($"Best level {KaissaSettings.SoloBest}", 13, UiKit.Dim, bold: true);
        panel.Add(_bestLabel);
        rail.Add(panel);
        _root.Add(rail);

        _board.Render(ChessGameStart, canMove: false, lastMove: null, whiteBottom: true);

        _overlayHost = new VisualElement();
        _overlayHost.style.position = Position.Absolute;
        _overlayHost.style.left = 0; _overlayHost.style.top = 0; _overlayHost.style.right = 0; _overlayHost.style.bottom = 0;
        _root.Add(_overlayHost);

        if (Environment.GetCommandLineArgs().Contains("-kaissa-solotest"))
            StartCoroutine(AutoDemo());
        else
            ShowIntro();
    }

    private const string ChessGameStart = "8/8/8/8/8/8/8/8 w - - 0 1";

    private void ShowIntro()
    {
        var dim = Overlay();
        var panel = OverlayPanel(); panel.style.width = 470;
        panel.Add(UiKit.Text_("Solo Chess", 28, UiKit.Text, bold: true));
        var sub = UiKit.Text_("Every move must capture. No piece may capture more than twice (it darkens and is stuck), and a king can never be taken - so it must be the last piece. Clear the board down to one piece to level up.", 14, UiKit.Dim);
        sub.style.whiteSpace = WhiteSpace.Normal; sub.style.marginTop = 10; sub.style.marginBottom = 18;
        sub.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(sub);
        var start = UiKit.Primary("Start", StartRun, 16); start.style.width = 320;
        panel.Add(start);
        dim.Add(panel); _overlayHost.Add(dim);
    }

    private void StartRun()
    {
        _overlayHost.Clear();
        _level = 4;
        NewBoard();
    }

    private void NewBoard()
    {
        _overlayHost.Clear();
        bool withKing = _level >= 6;
        string placement = null;
        for (int tries = 0; tries < 4 && placement == null; tries++)
            placement = SoloChess.Generate(_level, withKing, _rng.Next());
        if (placement == null) placement = SoloChess.Generate(Math.Max(3, _level - 1), false, _rng.Next());
        if (placement == null) placement = "8/8/8/8/8/8/R6R/8"; // last resort: a trivially solvable two-rook board
        LoadPlacement(placement);
    }

    private void ResetBoard()
    {
        _overlayHost.Clear();
        if (_placement != null) LoadPlacement(_placement);
    }

    private void LoadPlacement(string placement)
    {
        KaissaStreak.RecordToday(); // Solo Chess counts toward the daily training streak
        _placement = placement;
        _solo = new SoloChess(placement);
        _from = null;
        _moves = 0;
        _scoreLabel.text = _level.ToString();
        _movesLabel.text = "";
        SetFeedback("", UiKit.Dim);
        _board.SquareClickHandler = OnPick;
        _board.Render(_solo.DisplayPlacement() + " w - - 0 1", canMove: false, lastMove: null, whiteBottom: true);
    }

    private void OnPick(string sq)
    {
        if (_solo == null || string.IsNullOrEmpty(sq)) return;
        if (_from == null)
        {
            if (_solo.PieceAt(sq) == '\0') return; // must start from a piece
            _from = sq;
            _board.HighlightSquare(sq, Sel);
            return;
        }
        if (sq == _from) { _from = null; Rerender(); return; }

        if (_solo.TryApply(new SoloMove(_from, sq)))
        {
            _from = null;
            _moves++;
            _movesLabel.text = $"{_moves} moves";
            Rerender();
            if (_solo.Solved) { Solved(); return; }
            if (_solo.LegalMoves().Count == 0) SetFeedback("No captures left - Reset and try again.", UiKit.Danger);
        }
        else
        {
            // Not a legal capture from the selected piece; treat the tap as picking a new piece.
            _from = _solo.PieceAt(sq) != '\0' ? sq : null;
            Rerender();
            if (_from != null) _board.HighlightSquare(_from, Sel);
        }
    }

    private void Rerender() =>
        _board.Render(_solo.DisplayPlacement() + " w - - 0 1", canMove: false, lastMove: null, whiteBottom: true);

    private void Solved()
    {
        SetFeedback("Solved!", UiKit.GreenHi);
        if (_level > KaissaSettings.SoloBest) KaissaSettings.SoloBest = _level;
        _bestLabel.text = $"Best level {KaissaSettings.SoloBest}";
        _level++;
        StartCoroutine(NextAfter(0.9f));
    }

    private IEnumerator NextAfter(float s)
    {
        yield return new WaitForSeconds(s);
        NewBoard();
    }

    private void SetFeedback(string text, Color color)
    {
        _feedback.text = text;
        _feedback.style.color = color;
        _feedback.style.backgroundColor = string.IsNullOrEmpty(text) ? new Color(0, 0, 0, 0) : new Color(color.r, color.g, color.b, 0.14f);
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

    private VisualElement Overlay()
    {
        var dim = new VisualElement();
        dim.style.position = Position.Absolute;
        dim.style.left = 0; dim.style.top = 0; dim.style.right = 0; dim.style.bottom = 0;
        dim.style.backgroundColor = new Color(0, 0, 0, 0.72f);
        dim.style.alignItems = Align.Center; dim.style.justifyContent = Justify.Center;
        return dim;
    }

    private VisualElement OverlayPanel() => UiKit.OverlayCard();

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneTransition.Go("Menu");
    }

    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "solo_warmup.png"));
        yield return new WaitForSeconds(0.6f);
        ShowIntro();
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "solo_intro.png"));
        StartRun();
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "solo_board.png"));
        // Play the first capture of a solving line to show a move + the moves counter.
        if (_solo != null)
        {
            var m = _solo.LegalMoves().FirstOrDefault(mv => { var p = _solo.Clone(); p.TryApply(mv); return p.IsSolvable(); });
            if (m != null) { OnPick(m.From); yield return new WaitForSeconds(0.3f); OnPick(m.To); }
        }
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "solo_move.png"));

        // Finish the board along a solving line and capture the solved state (before it auto-advances).
        int guard = 30;
        while (_solo != null && !_solo.Solved && guard-- > 0)
        {
            var mv = _solo.LegalMoves().FirstOrDefault(x => { var p = _solo.Clone(); p.TryApply(x); return p.IsSolvable(); });
            if (mv == null) break;
            OnPick(mv.From); yield return new WaitForSeconds(0.12f);
            OnPick(mv.To); yield return new WaitForSeconds(0.12f);
        }
        yield return new WaitForSeconds(0.15f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "solo_win.png"));
        yield return new WaitForSeconds(0.4f);
        Application.Quit();
    }

    private static string ArgValue(string key)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
        return null;
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
