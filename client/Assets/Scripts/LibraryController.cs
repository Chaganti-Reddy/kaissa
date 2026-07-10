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

// Learn: a guided lesson trainer. Each lesson explains an idea in our own words, then drills it on
// real positions (drawn from the puzzle library by pattern) that the player solves on the board with
// feedback and commentary. Completed lessons are saved. Works on the 2D or 3D board via IBoardView.
public sealed class LibraryController : MonoBehaviour
{
    private ScenarioLibrary _lib;
    private IBoardView _board;
    private VisualElement _boardHost;
    private PieceAudio _audio;

    private Lesson _lesson;
    private LessonSession _session;
    private LessonStep _step;
    private int _stepIndex, _retry;
    private bool _busy, _completed, _whiteBottom = true;

    private VisualElement _root, _list;
    private Label _title, _stepLabel, _text, _feedback;
    private Button _nextBtn, _retryBtn, _hintBtn;

    private static readonly Color Good = new(0.30f, 0.85f, 0.45f, 0.55f);
    private static readonly Color Bad = new(0.92f, 0.33f, 0.33f, 0.55f);
    private static readonly Color HintCol = new(0.51f, 0.72f, 0.30f, 0.60f);

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }

        _lib = ScenarioLibrary.LoadDefault();
        _audio = PieceAudio.Attach(gameObject);

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
        _root.Add(UiKit.NavRail("Library"));
        _root.Add(BuildCenter());
        _root.Add(BuildRightRail());

        _board = BoardMount.Create(gameObject, _boardHost, _root, uci => OnMove(uci), _audio);

        SelectLesson(LessonLibrary.All[0]);

        if (Environment.GetCommandLineArgs().Contains("-kaissa-learntest"))
            StartCoroutine(AutoDemo());
    }

    // ---------------- layout ----------------

    private VisualElement BuildCenter()
    {
        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 18, 24, 18, 24);

        _title = UiKit.Text_("Learn", 24, UiKit.Text, bold: true);
        center.Add(_title);
        _stepLabel = UiKit.Text_("", 13, UiKit.Gold, bold: true);
        _stepLabel.style.marginBottom = 6;
        center.Add(_stepLabel);

        _text = UiKit.Text_("", 14, UiKit.Dim);
        _text.style.width = 480; _text.style.whiteSpace = WhiteSpace.Normal;
        _text.style.unityTextAlign = TextAnchor.MiddleCenter; _text.style.marginBottom = 10; _text.style.minHeight = 44;
        center.Add(_text);

        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        center.Add(_boardHost);

        _feedback = UiKit.Text_("", 16, UiKit.Dim, bold: true);
        _feedback.style.marginTop = 10; _feedback.style.unityTextAlign = TextAnchor.MiddleCenter;
        UiKit.Pad(_feedback, 6, 14, 6, 14); UiKit.Radius(_feedback, 8);
        center.Add(_feedback);

        var controls = UiKit.Row(); controls.style.marginTop = 8;
        _nextBtn = Ctrl("Next", NextPressed);
        _retryBtn = Ctrl("Restart", () => { _retry++; SelectLesson(_lesson); });
        _hintBtn = Ctrl("Hint", ShowHint);
        controls.Add(_nextBtn); controls.Add(_retryBtn); controls.Add(_hintBtn); controls.Add(Ctrl("Flip", Flip));
        center.Add(controls);
        return center;
    }

    private Button Ctrl(string label, Action onClick)
    {
        var b = UiKit.Ghost(label, onClick, 14);
        b.style.marginLeft = 5; b.style.marginRight = 5; b.style.minWidth = 84;
        return b;
    }

    private VisualElement BuildRightRail()
    {
        var rail = new VisualElement();
        rail.style.width = 340; UiKit.Pad(rail, 18, 24, 18, 8);
        var panel = Panel(); UiKit.Pad(panel, 12, 14, 12, 14);
        panel.Add(UiKit.Text_("Lessons", 12, UiKit.Mute, bold: true));
        var scroll = new ScrollView(); scroll.style.maxHeight = 640; scroll.style.marginTop = 6;
        _list = scroll.contentContainer;
        panel.Add(scroll);
        rail.Add(panel);
        PopulateList();
        return rail;
    }

    private void PopulateList()
    {
        if (_list == null) return;
        _list.Clear();
        foreach (var topic in LessonLibrary.Topics)
        {
            var header = UiKit.Text_(topic, 12, UiKit.Mute, bold: true);
            header.style.marginTop = 8; header.style.marginBottom = 2;
            _list.Add(header);
            foreach (var l in LessonLibrary.ByTopic(topic))
            {
                var lesson = l;
                bool done = KaissaSettings.IsLessonDone(lesson.Id);
                bool active = lesson.Id == _lesson?.Id;
                var mark = new VisualElement();
                mark.style.width = 8; mark.style.height = 8; UiKit.Radius(mark, 4); mark.style.marginRight = 8; mark.style.flexShrink = 0;
                mark.style.backgroundColor = done ? UiKit.Green : UiKit.Mute;
                var row = UiKit.Row(mark, UiKit.Text_(lesson.Title, 14, active ? UiKit.Text : UiKit.Dim, bold: active));
                row.name = "lesson-" + lesson.Id;
                UiKit.Pad(row, 7, 10, 7, 10); UiKit.Radius(row, 6);
                if (active) row.style.backgroundColor = UiKit.Panel2;
                row.RegisterCallback<MouseEnterEvent>(_ => { if (lesson.Id != _lesson?.Id) row.style.backgroundColor = UiKit.Panel2; });
                row.RegisterCallback<MouseLeaveEvent>(_ => { if (lesson.Id != _lesson?.Id) row.style.backgroundColor = new Color(0, 0, 0, 0); });
                row.RegisterCallback<ClickEvent>(_ => { _retry = 0; SelectLesson(lesson); });
                UiKit.Interactive(row, 1.02f);
                _list.Add(row);
            }
        }
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

    // ---------------- lesson flow ----------------

    private void SelectLesson(Lesson lesson)
    {
        _lesson = lesson;
        _session = new LessonSession(lesson, _lib, seed: lesson.Id.GetHashCode() ^ (_retry * 7919));
        _stepIndex = 0; _completed = false; _busy = false;
        if (_nextBtn != null) _nextBtn.text = "Next";
        PopulateList();
        ShowStep();
    }

    private void ShowStep()
    {
        if (_session == null || _session.Count == 0) { _text.text = "No content for this lesson yet."; return; }
        _step = _session[_stepIndex];
        _title.text = _lesson.Title;
        _stepLabel.text = _step.Interactive ? $"Challenge {_step.Index} / {_step.Total - 1}" : "Introduction";
        _text.text = _step.Text;
        _whiteBottom = _step.WhiteBottom;
        _board.Render(_step.Fen, canMove: _step.Interactive, lastMove: null, whiteBottom: _whiteBottom);
        SetFeedback("", UiKit.Dim);
        Enable(_nextBtn, !_step.Interactive);   // intro advances with Next; challenges advance on a correct move
        Enable(_hintBtn, _step.Interactive);
        _busy = false;
    }

    private void NextPressed()
    {
        if (_completed) { NextLesson(); return; }
        if (_step is { Interactive: false }) Advance();
    }

    private void NextLesson()
    {
        int i = 0;
        for (int k = 0; k < LessonLibrary.All.Count; k++) if (LessonLibrary.All[k].Id == _lesson.Id) { i = k; break; }
        _retry = 0;
        SelectLesson(LessonLibrary.All[(i + 1) % LessonLibrary.All.Count]);
    }

    private void Advance()
    {
        _stepIndex++;
        if (_stepIndex >= _session.Count) CompleteLesson();
        else ShowStep();
    }

    private void CompleteLesson()
    {
        _completed = true; _busy = false;
        KaissaSettings.MarkLessonDone(_lesson.Id);
        _stepLabel.text = "Lesson complete";
        _text.text = $"You have finished \"{_lesson.Title}\". Keep the pattern in mind - it will keep coming up.";
        SetFeedback("Lesson complete", UiKit.GreenHi);
        _audio.PlayGameEnd();
        Enable(_nextBtn, true); Enable(_hintBtn, false);
        _nextBtn.text = "Next lesson";
        PopulateList();
    }

    private void OnMove(string uci)
    {
        if (_busy || _step == null || !_step.Interactive || _completed) return;
        _busy = true;

        bool correct = SameMove(_step.Fen, uci, _step.ExpectedMove);
        var afterFen = ApplyMove(_step.Fen, uci);
        if (afterFen != null) _board.Render(afterFen, canMove: false, lastMove: uci, whiteBottom: _whiteBottom);

        if (correct)
        {
            TintMove(uci, Good);
            SetFeedback(string.IsNullOrEmpty(_step.SuccessText) ? "Correct." : _step.SuccessText, UiKit.GreenHi);
            _audio.PlayCorrect();
            StartCoroutine(AdvanceAfter(0.9f));
        }
        else
        {
            TintMove(uci, Bad);
            SetFeedback("Not the key move - try again.", UiKit.Danger);
            _audio.PlayWrong();
            StartCoroutine(ResetStepAfter(1.1f));
        }
    }

    private IEnumerator AdvanceAfter(float s) { yield return new WaitForSeconds(s); Advance(); }

    private IEnumerator ResetStepAfter(float s)
    {
        yield return new WaitForSeconds(s);
        if (_completed) yield break;
        _board.Render(_step.Fen, canMove: true, lastMove: null, whiteBottom: _whiteBottom);
        SetFeedback("", UiKit.Dim);
        _busy = false;
    }

    // ---------------- controls / helpers ----------------

    private void ShowHint()
    {
        if (_step is not { Interactive: true } || string.IsNullOrEmpty(_step.ExpectedMove)) return;
        _board.HighlightSquare(_step.ExpectedMove.Substring(0, 2), HintCol);
        SetFeedback("Look at the highlighted piece.", UiKit.Dim);
    }

    private void Flip()
    {
        if (_step == null) return;
        _whiteBottom = !_whiteBottom;
        _board.Render(_step.Fen, canMove: _step.Interactive && !_busy && !_completed, lastMove: null, whiteBottom: _whiteBottom);
    }

    private void Enable(Button b, bool on) { if (b != null) { b.SetEnabled(on); b.style.opacity = on ? 1f : 0.45f; } }

    private void SetFeedback(string text, Color color)
    {
        _feedback.text = text; _feedback.style.color = color;
        _feedback.style.backgroundColor = string.IsNullOrEmpty(text) ? new Color(0, 0, 0, 0) : new Color(0, 0, 0, 0.55f);
    }

    private void TintMove(string uci, Color c)
    {
        if (string.IsNullOrEmpty(uci) || uci.Length < 4) return;
        _board.HighlightSquare(uci.Substring(0, 2), c);
        _board.HighlightSquare(uci.Substring(2, 2), c);
    }

    private static bool SameMove(string fen, string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        try
        {
            var g = ChessGame.FromFen(fen);
            var ra = g.ResolveToUci(a) ?? a;
            var rb = g.ResolveToUci(b) ?? b;
            return string.Equals(ra, rb, StringComparison.OrdinalIgnoreCase);
        }
        catch { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    }

    private static string ApplyMove(string fen, string uci)
    {
        try { var g = ChessGame.FromFen(fen); if (g.TryMakeMove(uci)) return g.Fen; }
        catch { }
        return null;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame) SceneManager.LoadScene("Menu");
        else if (kb.fKey.wasPressedThisFrame) Flip();
        else if (kb.rightArrowKey.wasPressedThisFrame) NextPressed();
    }

    // ---------------- self-test ----------------

    private bool _recording, _pauseRec;
    private int _seq;

    // End-to-end self-test driven by REAL UI events (not controller method calls): clicks the lesson
    // rows and the Next/Hint/Restart buttons through the panel, and plays each challenge move through
    // the board's real click-to-move input path. A dense frame recorder runs the whole time so the run
    // can be reviewed like a video, and labeled milestone frames are captured at each step.
    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        string burst = System.IO.Path.Combine(dir, "burst");
        System.IO.Directory.CreateDirectory(dir);
        System.IO.Directory.CreateDirectory(burst);
        string tag = KaissaSettings.BoardView == 1 ? "3d" : "2d";
        KaissaSettings.AutoQueen = true; // never let a promotion picker block the scripted solve

        _recording = true;
        StartCoroutine(DenseRecord(burst, tag));

        yield return Shot(dir, tag, "warmup", 1.0f);

        // Real click on a lesson row to prove list selection wiring.
        UiAutomation.Click(_root.Q("lesson-skewer"));
        yield return Shot(dir, tag, "select_skewer", 0.9f);
        UiAutomation.Click(_root.Q("lesson-fork"));
        yield return Shot(dir, tag, "intro", 0.9f);

        // Real click on Next advances the intro to the first challenge.
        UiAutomation.Click(_nextBtn);
        yield return Shot(dir, tag, "challenge", 0.8f);

        // Real click on Hint highlights the key piece.
        UiAutomation.Click(_hintBtn);
        yield return Shot(dir, tag, "hint", 0.8f);

        // Solve each challenge through the board's real click-to-move path.
        int guard = 0;
        while (!_completed && _step is { Interactive: true } && !string.IsNullOrEmpty(_step.ExpectedMove) && guard++ < 12)
        {
            string uci = _step.ExpectedMove;
            if (uci.Length == 4) _board.DebugClickMove(uci.Substring(0, 2), uci.Substring(2, 2));
            else OnMove(uci); // promotions: use the move path directly (picker is bypassed by AutoQueen anyway)
            yield return Shot(dir, tag, $"solve_{guard}", 1.1f);
        }

        yield return Shot(dir, tag, "complete", 0.6f);

        // Real click on Restart replays the lesson (proves the button path + fresh session).
        UiAutomation.Click(_retryBtn);
        yield return Shot(dir, tag, "restart", 0.8f);

        _recording = false;
        yield return new WaitForSeconds(0.3f);
        Application.Quit();
    }

    private IEnumerator DenseRecord(string dir, string tag)
    {
        while (_recording)
        {
            // ScreenCapture honors only one capture per frame, so yield the slot while a milestone frame
            // is being taken - otherwise the two collide and one of the PNGs is silently dropped.
            if (!_pauseRec)
            {
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{tag}_{_seq:0000}.png"));
                _seq++;
            }
            yield return new WaitForSeconds(0.15f);
        }
    }

    private IEnumerator Shot(string dir, string tag, string label, float hold)
    {
        _pauseRec = true;
        yield return null; // let the frame settle after the action, with the dense recorder paused
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"learn_{tag}_{label}.png"));
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

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
