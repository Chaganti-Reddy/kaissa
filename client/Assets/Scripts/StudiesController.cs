using System;
using System.Collections;
using System.Linq;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Studies: step through an annotated master line move by move, reading the comment on each move. The
// lessons are authored as PGN (StudyLibrary) and parsed by the pure core, so more can be added as data.
// A flat 2D board keeps the focus on the moves and the notes. No engine required.
public sealed class StudiesController : MonoBehaviour
{
    private Board2D _board;
    private System.Collections.Generic.IReadOnlyList<StudyChapter> _chapters;
    private StudyChapter _chapter;
    private int _chapterIndex;
    private int _ply; // 0 = start position, 1..N after each move

    private VisualElement _root, _overlayHost, _boardHost;
    private Label _title, _counter, _comment, _movePair;
    private Button _prev, _next;

    // Per-move recall mode (MoveTrainer): replay the line from memory; missed moves come back until they stick.
    private bool _recall;
    private MoveTrainer _mt;
    private int _recallDay, _recallPly, _recallGuard;
    private string _from;
    private static readonly Color RecallSel = new(0.36f, 0.72f, 0.42f, 0.55f);

    private static readonly Color LastMoveTint = new(0.36f, 0.72f, 0.42f, 0.35f);

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
        _root.Add(UiKit.NavRail("Studies"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 18, 24, 18, 24);

        var head = UiKit.Row();
        head.style.width = 480; head.style.justifyContent = Justify.SpaceBetween; head.style.marginBottom = 6;
        _title = UiKit.Text_("", 18, UiKit.Text, bold: true);
        _counter = UiKit.Text_("", 14, UiKit.Dim, bold: true);
        head.Add(_title); head.Add(_counter);
        center.Add(head);

        _movePair = UiKit.Text_("", 13, UiKit.Gold, bold: true);
        _movePair.style.marginBottom = 6;
        center.Add(_movePair);

        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        _board = new Board2D(null);
        _board.Root.style.width = 480; _board.Root.style.height = 480;
        _boardHost.Add(_board.Root);
        center.Add(_boardHost);

        var ctrls = UiKit.Row(); ctrls.style.marginTop = 12;
        _prev = UiKit.Ghost("< Prev", () => Step(-1), 14);
        _next = UiKit.Primary("Next >", () => Step(1), 14); _next.style.width = 140;
        ctrls.Add(_prev); ctrls.Add(_next);
        ctrls.Add(UiKit.Ghost("Recall", StartRecall, 14));
        ctrls.Add(UiKit.Ghost("Next study", NextChapter, 14));
        center.Add(ctrls);

        _comment = UiKit.Text_("", 15, UiKit.Text, bold: false);
        _comment.style.marginTop = 12; _comment.style.whiteSpace = WhiteSpace.Normal;
        _comment.style.maxWidth = 480; _comment.style.unityTextAlign = TextAnchor.MiddleCenter;
        _comment.style.backgroundColor = UiKit.Panel2; UiKit.Pad(_comment, 12, 16, 12, 16); UiKit.Radius(_comment, 10);
        _comment.style.minHeight = 64;
        center.Add(_comment);
        _root.Add(center);

        _overlayHost = new VisualElement();
        _overlayHost.style.position = Position.Absolute;
        _overlayHost.style.left = 0; _overlayHost.style.top = 0; _overlayHost.style.right = 0; _overlayHost.style.bottom = 0;
        _root.Add(_overlayHost);

        _chapters = StudyLibrary.Chapters;
        LoadChapter(0);

        if (Environment.GetCommandLineArgs().Contains("-kaissa-studytest"))
            StartCoroutine(AutoDemo());
    }

    private void LoadChapter(int index)
    {
        if (_chapters == null || _chapters.Count == 0) return;
        _recall = false;
        if (_board != null) _board.SquareClickHandler = null;
        _chapterIndex = ((index % _chapters.Count) + _chapters.Count) % _chapters.Count;
        _chapter = _chapters[_chapterIndex];
        _ply = 0;
        Render();
    }

    private void NextChapter() => LoadChapter(_chapterIndex + 1);

    private void Step(int delta)
    {
        if (_recall) return; // navigation is disabled while recalling
        int max = _chapter?.Moves.Count ?? 0;
        _ply = Math.Clamp(_ply + delta, 0, max);
        Render();
    }

    // -- recall mode (per-move spaced repetition) ---------------------------

    private void StartRecall()
    {
        if (_chapter == null || _chapter.Moves.Count == 0) return;
        _recall = true; _recallDay = 0; _recallGuard = 0; _from = null;
        _mt = new MoveTrainer();
        _mt.Add(_chapter.Title, _chapter.Moves.Select(m => m.Uci).ToList());
        _prev.SetEnabled(false); _next.SetEnabled(false);
        NextRecall();
    }

    private void NextRecall()
    {
        if (!_recall) return;
        var item = _mt.NextDue(_recallDay);
        if (item == null || _recallGuard++ > 300) { EndRecall(); return; }

        _recallPly = item.MoveIndex;
        var game = ChessGame.Start();
        for (int i = 0; i < _recallPly; i++) game.TryMakeMove(_chapter.Moves[i].Uci);
        _from = null;

        _title.text = _chapter.Title + " - recall";
        _counter.text = $"Move {_recallPly + 1} / {_chapter.Moves.Count}";
        _movePair.text = $"Play {(_recallPly % 2 == 0 ? "White" : "Black")}'s move";
        _comment.style.color = UiKit.Text;
        _comment.text = "Recall the move by playing it. Missed moves come back until they stick.";
        _board.SquareClickHandler = OnRecallPick;
        _board.Render(game.Fen, canMove: false, lastMove: null, whiteBottom: true);
    }

    private void OnRecallPick(string sq)
    {
        if (!_recall || string.IsNullOrEmpty(sq)) return;
        if (_from == null) { _from = sq; _board.HighlightSquare(sq, RecallSel); return; }
        if (sq == _from) { _from = null; NextRecall(); return; }

        string want = _chapter.Moves[_recallPly].Uci;
        string got = _from + sq;
        bool correct = string.Equals(got, want, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(got + "q", want, StringComparison.OrdinalIgnoreCase);
        _mt.Review(_chapter.Title, _recallPly, correct, _recallDay);
        _recallDay++;
        _from = null;

        _comment.style.color = correct ? UiKit.GreenHi : UiKit.Danger;
        _comment.text = correct ? $"Correct - {SanAt(_recallPly)}" : $"That was {SanAt(_recallPly)}";
        NextRecall();
    }

    private string SanAt(int ply)
    {
        var game = ChessGame.Start();
        for (int i = 0; i < ply; i++) game.TryMakeMove(_chapter.Moves[i].Uci);
        return game.SanForUci(_chapter.Moves[ply].Uci) ?? _chapter.Moves[ply].San;
    }

    private void EndRecall()
    {
        _recall = false;
        _board.SquareClickHandler = null;
        _comment.style.color = UiKit.Text;
        _ply = _chapter.Moves.Count;
        Render();
        _comment.text = "Recall complete - the whole line remembered.";
    }

    // Replay the mainline up to _ply and render the resulting position.
    private void Render()
    {
        if (_chapter == null) return;
        var game = ChessGame.Start();
        string lastUci = null;
        for (int i = 0; i < _ply && i < _chapter.Moves.Count; i++)
        {
            lastUci = _chapter.Moves[i].Uci;
            game.TryMakeMove(lastUci);
        }

        _title.text = _chapter.Title;
        int total = _chapter.Moves.Count;
        _counter.text = $"Move {_ply} / {total}";
        _board.Render(game.Fen, canMove: false, lastMove: lastUci, whiteBottom: true);

        if (_ply == 0)
        {
            _movePair.text = "Starting position";
            _comment.text = "Step through the line with Next. Each move carries a short note.";
        }
        else
        {
            var mv = _chapter.Moves[_ply - 1];
            int moveNo = (_ply + 1) / 2;
            bool whiteMove = _ply % 2 == 1;
            _movePair.text = $"{moveNo}{(whiteMove ? "." : "...")} {mv.San}";
            _comment.text = string.IsNullOrEmpty(mv.Comment) ? "(no note on this move)" : mv.Comment;
        }

        _prev.SetEnabled(_ply > 0);
        _next.SetEnabled(_ply < total);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame) SceneTransition.Go("Menu");
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame) Step(1);
        else if (Keyboard.current.leftArrowKey.wasPressedThisFrame) Step(-1);
    }

    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);
        LoadChapter(0);
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "studies_start.png"));
        for (int i = 0; i < 4; i++) { Step(1); yield return new WaitForSeconds(0.3f); }
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "studies_midline.png"));
        NextChapter();
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "studies_chapter2.png"));
        yield return new WaitForSeconds(0.3f);
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
