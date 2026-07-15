using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Three modes share the board:
//   Explore - play any moves; the position is named (ECO + opening) with its book continuations.
//   Learn   - browse named openings grouped by first move, search, and step a mainline.
//   Drill   - spaced-repetition recall of your repertoire lines.
// The opening book is the CC0 Lichess dataset.
public sealed class OpeningController : MonoBehaviour
{
    private enum Mode { Explore, Learn, Drill }

    private static OpeningBook _sharedBook; // built once per app run, reused across scene entries
    private OpeningBook _book;
    private IBoardView _board;
    private VisualElement _boardHost;
    private PieceAudio _audio;
    private bool _whiteBottom = true;

    private readonly List<string> _path = new(); // UCI from the start, shared by explore and learn
    private string _fen = ChessGame.StartFen;

    private OpeningEntry _learnLine;
    private int _learnPly;

    private OpeningProgress _progress;
    private RepertoireSession _repertoire;
    private RepertoireCard _card;
    private float _shownTime;
    private bool _drillBusy;
    private Label _drillMeta;

    private Mode _mode = Mode.Explore;

    private VisualElement _root, _rightRail;
    private Label _title, _nameLabel, _ecoLabel, _feedback, _statusHint, _historyBar;
    private VisualElement _tabExplore, _tabLearn, _tabDrill;

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }

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
        _root.Add(UiKit.NavRail("Opening"));
        _root.Add(BuildCenter());
        _root.Add(BuildRightRail());

        _board = BoardMount.Create(gameObject, _boardHost, _root, uci => OnPlayerMove(uci), _audio);
        _board.Render(ChessGame.StartFen, canMove: true, lastMove: null, whiteBottom: true);
        // The position index is precomputed offline (tools/OpeningImporter), so loading is just a fast
        // deserialize - no replaying openings, no freeze. Cache it so only the first entry per run pays it.
        if (_sharedBook == null)
        {
            var loading = UiKit.Text_("Loading openings...", 15, UiKit.Dim);
            _rightRail.Add(loading);
            yield return null;
            try { _sharedBook = OpeningBook.LoadDefault(); }
            catch (Exception e) { loading.text = "Opening book failed to load."; Debug.LogError(e); yield break; }
        }
        _book = _sharedBook;

        _progress = KaissaOpenings.Load();
        _repertoire = new RepertoireSession(OpeningRepertoire.Default, _progress, new SystemClock());

        SetMode(Mode.Explore);

        if (Environment.GetCommandLineArgs().Contains("-kaissa-openingtest"))
            StartCoroutine(AutoDemo());
    }

    // Self-test: exercises every mode and control through the real input paths, with burst recording.
    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);
        string tag = KaissaSettings.BoardView == 1 ? "3d" : "2d";

        KaissaSettings.AutoQueen = true;
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"op_{tag}_warmup.png"));
        yield return new WaitForSeconds(0.6f);

        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"op_{tag}_explore_start.png"));
        yield return new WaitForSeconds(0.5f);

        foreach (var mv in new[] { "e2e4", "e7e5", "g1f3", "b8c6", "f1c4" })
        {
            _board.DebugClickMove(mv.Substring(0, 2), mv.Substring(2, 2));
            yield return Burst(dir, $"op_{tag}_explore_{mv}", 5, 0.05f);
        }
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"op_{tag}_explore_named.png"));
        yield return new WaitForSeconds(0.4f);

        UiAutomation.Click(_root.Q("bookrow"));
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"op_{tag}_explore_bookrow.png"));
        yield return new WaitForSeconds(0.3f);

        UiAutomation.Click(UiAutomation.FindButton(_root, "Back"));
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"op_{tag}_explore_back.png"));
        UiAutomation.Click(UiAutomation.FindButton(_root, "Reset"));
        yield return new WaitForSeconds(0.5f);

        UiAutomation.Click(_tabLearn);
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"op_{tag}_learn_list.png"));
        yield return new WaitForSeconds(0.5f);

        // Setting the field value fires the same value-changed callback typing does.
        var search = _root.Q<TextField>();
        if (search != null) search.value = "Sicilian";
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"op_{tag}_learn_search.png"));
        yield return new WaitForSeconds(0.4f);

        UiAutomation.Click(_root.Q("openrow"));
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"op_{tag}_learn_loaded.png"));
        yield return new WaitForSeconds(0.4f);

        UiAutomation.Click(UiAutomation.FindButton(_root, "Prev"));
        yield return new WaitForSeconds(0.4f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "Prev"));
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"op_{tag}_learn_prev.png"));
        yield return new WaitForSeconds(0.4f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "Next"));
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"op_{tag}_learn_next.png"));
        yield return new WaitForSeconds(0.4f);

        UiAutomation.Click(_tabDrill);
        yield return new WaitForSeconds(0.7f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"op_{tag}_drill_prompt.png"));
        yield return new WaitForSeconds(0.4f);
        if (_card?.ExpectedMove is { } exp && exp.Length >= 4)
        {
            _board.DebugClickMove(exp.Substring(0, 2), exp.Substring(2, 2));
            yield return Burst(dir, $"op_{tag}_drill_correct", 6, 0.06f);
        }
        yield return new WaitForSeconds(1.0f);
        if (_card != null)
        {
            var wrong = FirstLegalOtherThan(_card.Fen, _card.ExpectedMove);
            if (wrong is { Length: >= 4 }) _board.DebugClickMove(wrong.Substring(0, 2), wrong.Substring(2, 2));
            yield return Burst(dir, $"op_{tag}_drill_wrong", 6, 0.06f);
        }
        yield return new WaitForSeconds(0.8f);

        UiAutomation.Click(_tabExplore);
        yield return new WaitForSeconds(0.4f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "Flip"));
        yield return new WaitForSeconds(0.7f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"op_{tag}_flip.png"));
        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }

    private IEnumerator Burst(string dir, string prefix, int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{prefix}_{i:000}.png"));
            yield return new WaitForSeconds(interval);
        }
    }

    private static string FirstLegalOtherThan(string fen, string notUci)
    {
        foreach (var mv in ChessGame.FromFen(fen).LegalUciMoves())
            if (!string.Equals(mv, notUci, StringComparison.OrdinalIgnoreCase))
                return mv;
        return null;
    }

    private static string ArgValue(string key)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == key) return args[i + 1];
        return null;
    }

    private VisualElement BuildCenter()
    {
        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 18, 24, 18, 24);

        var head = UiKit.Row();
        head.style.width = 480; head.style.justifyContent = Justify.SpaceBetween; head.style.marginBottom = 10;
        _title = UiKit.Text_("Openings", 24, UiKit.Text, bold: true);
        head.Add(_title);
        var tabs = UiKit.Row();
        _tabExplore = Tab("Explore", () => SetMode(Mode.Explore));
        _tabLearn = Tab("Learn", () => SetMode(Mode.Learn));
        _tabDrill = Tab("Drill", () => SetMode(Mode.Drill));
        tabs.Add(_tabExplore); tabs.Add(_tabLearn); tabs.Add(_tabDrill);
        head.Add(tabs);
        center.Add(head);

        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        center.Add(_boardHost);

        _historyBar = UiKit.Text_("", 14, UiKit.Dim);
        _historyBar.style.marginTop = 10; _historyBar.style.width = 480; _historyBar.style.whiteSpace = WhiteSpace.Normal;
        _historyBar.style.unityTextAlign = TextAnchor.MiddleCenter; _historyBar.style.minHeight = 20;
        center.Add(_historyBar);

        _feedback = UiKit.Text_("", 15, UiKit.Dim, bold: true);
        _feedback.style.marginTop = 6; _feedback.style.unityTextAlign = TextAnchor.MiddleCenter; _feedback.style.minHeight = 20;
        center.Add(_feedback);

        var controls = UiKit.Row();
        controls.style.marginTop = 8;
        controls.Add(Ctrl("Back", Back));
        controls.Add(Ctrl("Reset", ResetToStart));
        controls.Add(Ctrl("Flip", Flip));
        center.Add(controls);
        return center;
    }

    private VisualElement Tab(string label, Action onClick)
    {
        var b = UiKit.Ghost(label, onClick, 13);
        b.style.marginLeft = 6; UiKit.Pad(b, 6, 12, 6, 12);
        return b;
    }

    private Button Ctrl(string label, Action onClick)
    {
        var b = UiKit.Ghost(label, onClick, 14);
        b.style.marginLeft = 5; b.style.marginRight = 5; b.style.minWidth = 84;
        return b;
    }

    private VisualElement BuildRightRail()
    {
        _rightRail = new VisualElement();
        _rightRail.style.width = 360; UiKit.Pad(_rightRail, 18, 24, 18, 8);
        return _rightRail;
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

    private void SetMode(Mode m)
    {
        _mode = m;
        HighlightTab(_tabExplore, m == Mode.Explore);
        HighlightTab(_tabLearn, m == Mode.Learn);
        HighlightTab(_tabDrill, m == Mode.Drill);
        _feedback.text = "";
        _rightRail.Clear();

        switch (m)
        {
            case Mode.Explore: EnterExplore(); break;
            case Mode.Learn: EnterLearn(); break;
            case Mode.Drill: EnterDrill(); break;
        }
    }

    private static void HighlightTab(VisualElement tab, bool on)
    {
        tab.style.backgroundColor = on ? UiKit.Green : UiKit.Panel2;
    }

    private VisualElement _bookList;

    private void EnterExplore()
    {
        ResetToStart();

        var info = Panel(); UiKit.Pad(info, 14, 16, 14, 16);
        _nameLabel = UiKit.Text_("Starting position", 18, UiKit.Text, bold: true);
        _nameLabel.style.whiteSpace = WhiteSpace.Normal;
        info.Add(_nameLabel);
        _ecoLabel = UiKit.Text_("", 13, UiKit.Gold, bold: true);
        info.Add(_ecoLabel);
        _rightRail.Add(info);

        var book = Panel(); book.style.marginTop = 14; UiKit.Pad(book, 12, 14, 12, 14);
        book.Add(UiKit.Text_("Book moves", 12, UiKit.Mute, bold: true));
        var scroll = UiKit.Scroll(); scroll.style.maxHeight = 460; scroll.style.marginTop = 6;
        _bookList = scroll.contentContainer;
        book.Add(scroll);
        _rightRail.Add(book);

        RefreshExplore();
    }

    private void RefreshExplore()
    {
        var named = _book.Name(_fen);
        if (named != null) { _nameLabel.text = named.Name; _ecoLabel.text = named.Eco; }
        else if (_path.Count == 0) { _nameLabel.text = "Starting position"; _ecoLabel.text = ""; }
        // else: keep the last known name (we're in a sub-line the book doesn't name)

        _bookList.Clear();
        var conts = _book.Continuations(_fen);
        if (conts.Count == 0)
            _bookList.Add(UiKit.Text_("Out of book - free play.", 13, UiKit.Mute));
        foreach (var c in conts.OrderByDescending(c => c.Name != null))
            _bookList.Add(BookRow(c));

        UpdateHistory();
    }

    private VisualElement BookRow(OpeningMove c)
    {
        var row = UiKit.Row();
        row.name = "bookrow";
        row.style.justifyContent = Justify.SpaceBetween;
        UiKit.Pad(row, 7, 10, 7, 10); UiKit.Radius(row, 6);
        var san = UiKit.Text_(c.San, 14, UiKit.Text, bold: true); san.style.minWidth = 54;
        row.Add(san);
        var name = UiKit.Text_(c.Name ?? "", 12, UiKit.Dim); name.style.whiteSpace = WhiteSpace.Normal; name.style.flexGrow = 1;
        name.style.unityTextAlign = TextAnchor.MiddleRight;
        row.Add(name);
        row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = UiKit.Panel2);
        row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = new Color(0, 0, 0, 0));
        row.RegisterCallback<ClickEvent>(_ => PlayUci(c.Uci));
        UiKit.Interactive(row, 1.02f);
        return row;
    }

    private VisualElement _openingList;

    private void EnterLearn()
    {
        ResetToStart();
        _learnLine = null; _learnPly = 0;

        var pick = Panel(); UiKit.Pad(pick, 12, 14, 12, 14);
        pick.Add(UiKit.Text_("Choose an opening", 12, UiKit.Mute, bold: true));
        var search = new TextField { value = "" };
        search.style.marginTop = 6; search.style.marginBottom = 6;
        search.RegisterValueChangedCallback(e => PopulateOpenings(e.newValue));
        pick.Add(search);
        var scroll = UiKit.Scroll(); scroll.style.maxHeight = 420;
        _openingList = scroll.contentContainer;
        pick.Add(scroll);
        _rightRail.Add(pick);

        var info = Panel(); info.style.marginTop = 14; UiKit.Pad(info, 12, 14, 12, 14);
        _nameLabel = UiKit.Text_("Pick an opening to study", 16, UiKit.Text, bold: true);
        _nameLabel.style.whiteSpace = WhiteSpace.Normal;
        info.Add(_nameLabel);
        _ecoLabel = UiKit.Text_("", 13, UiKit.Gold, bold: true);
        info.Add(_ecoLabel);
        var stepRow = UiKit.Row(); stepRow.style.marginTop = 8;
        stepRow.Add(Ctrl("Prev", LearnPrev));
        stepRow.Add(Ctrl("Next", LearnNext));
        info.Add(stepRow);
        _rightRail.Add(info);

        PopulateOpenings("");
        UpdateHistory();
    }

    private void PopulateOpenings(string filter)
    {
        _openingList.Clear();
        filter = filter?.Trim() ?? "";
        int shown = 0;
        foreach (var (group, entries) in _book.Grouped())
        {
            var matches = string.IsNullOrEmpty(filter)
                ? entries
                : entries.Where(e => e.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (matches.Count == 0) continue;

            var header = UiKit.Text_(group, 12, UiKit.Mute, bold: true);
            header.style.marginTop = 6; header.style.marginBottom = 2;
            _openingList.Add(header);

            foreach (var e in matches)
            {
                if (shown++ > 400) return; // keep the list responsive; searching narrows it
                _openingList.Add(OpeningRow(e));
            }
        }
    }

    private VisualElement OpeningRow(OpeningEntry e)
    {
        var row = UiKit.Row();
        row.name = "openrow";
        UiKit.Pad(row, 6, 8, 6, 8); UiKit.Radius(row, 5);
        var eco = UiKit.Text_(e.Eco, 11, UiKit.Gold, bold: true); eco.style.minWidth = 34;
        row.Add(eco);
        var name = UiKit.Text_(e.Name, 13, UiKit.Text); name.style.whiteSpace = WhiteSpace.Normal; name.style.flexGrow = 1;
        row.Add(name);
        row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = UiKit.Panel2);
        row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = new Color(0, 0, 0, 0));
        row.RegisterCallback<ClickEvent>(_ => LoadLearnLine(e));
        UiKit.Interactive(row, 1.02f);
        return row;
    }

    private void LoadLearnLine(OpeningEntry e)
    {
        _learnLine = e; _learnPly = e.Uci.Count;
        _nameLabel.text = e.Name; _ecoLabel.text = e.Eco;
        RebuildFromLearn();
    }

    private void LearnNext()
    {
        if (_learnLine == null || _learnPly >= _learnLine.Uci.Count) return;
        _learnPly++; RebuildFromLearn();
    }

    private void LearnPrev()
    {
        if (_learnLine == null || _learnPly <= 0) return;
        _learnPly--; RebuildFromLearn();
    }

    private void RebuildFromLearn()
    {
        _path.Clear();
        for (int i = 0; i < _learnPly && i < _learnLine.Uci.Count; i++) _path.Add(_learnLine.Uci[i]);
        _fen = FenOf(_path);
        _whiteBottom = _learnLine.Uci.Count == 0 || _learnLine.Uci[0][1] != '7'; // white lines bottom; simple heuristic
        var last = _path.Count > 0 ? _path[^1] : null;
        _board.Render(_fen, canMove: false, lastMove: last, whiteBottom: _whiteBottom);
        _feedback.style.color = UiKit.Dim;
        _feedback.text = $"Move {_learnPly} / {_learnLine.Uci.Count}";
        UpdateHistory();
    }

    private void EnterDrill()
    {
        var panel = Panel(); UiKit.Pad(panel, 14, 16, 14, 16);
        panel.Add(UiKit.Text_("Repertoire drill", 16, UiKit.Text, bold: true));
        var sub = UiKit.Text_("Recall your book move. Misses come back sooner.", 13, UiKit.Dim);
        sub.style.whiteSpace = WhiteSpace.Normal; sub.style.marginTop = 4;
        panel.Add(sub);
        _statusHint = UiKit.Text_("", 14, UiKit.Gold, bold: true); _statusHint.style.marginTop = 10;
        panel.Add(_statusHint);
        _drillMeta = UiKit.Text_("", 12, UiKit.Dim); _drillMeta.style.marginTop = 4;
        _drillMeta.style.whiteSpace = WhiteSpace.Normal;
        panel.Add(_drillMeta);
        _rightRail.Add(panel);

        _drillBusy = false;
        DrillNext();
    }

    private void DrillNext()
    {
        if (_mode != Mode.Drill) return; // guard: a late coroutine must not clobber another mode
        _card = _repertoire.Next();
        if (_card == null) { _feedback.text = "No repertoire lines due."; return; }
        _shownTime = Time.time;
        _whiteBottom = _card.WhiteToMove;
        _fen = _card.Fen; _path.Clear();
        _board.Render(_card.Fen, canMove: true, lastMove: null, whiteBottom: _whiteBottom);
        _feedback.style.color = UiKit.Dim;
        _feedback.text = $"{_card.LineName} - your move";
        _statusHint.text = $"{_repertoire.DueCount} due   -   {_repertoire.MasteredCount}/{_repertoire.Total} learned";
        _drillMeta.text = $"Chunk: {_card.Chunk}   -   Level {_card.Level}/{SrLevel.MaxLevel}";
        UpdateHistory();
    }

    private void OnDrillMove(string uci)
    {
        if (_drillBusy || _card == null) return;
        _drillBusy = true;
        var result = _repertoire.Submit(uci, TimeSpan.FromSeconds(Time.time - _shownTime));
        KaissaOpenings.Save(_progress);
        var after = ApplyMove(_card.Fen, uci);
        if (after != null) _board.Render(after, false, uci, _whiteBottom);
        _feedback.style.color = result.Correct ? UiKit.GreenHi : UiKit.Danger;
        _feedback.text = result.Correct
            ? $"{_card.LineName} - correct   -   next in {result.NextLabel} (Level {result.Level}/{SrLevel.MaxLevel})"
            : $"Book move was {result.ExpectedMove} - back to Level 1";
        if (result.Correct) _audio.PlayCorrect(); else _audio.PlayWrong();
        StartCoroutine(DrillNextAfter(result.Correct ? 0.8f : 1.7f));
    }

    private IEnumerator DrillNextAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (_mode != Mode.Drill) yield break; // mode switched while waiting; don't touch the board
        _feedback.text = "";
        _drillBusy = false;
        DrillNext();
    }

    private void OnPlayerMove(string uci)
    {
        if (_book == null) return;
        if (_mode == Mode.Drill) { OnDrillMove(uci); return; }
        PlayUci(uci);
    }

    private void PlayUci(string uci)
    {
        var after = ApplyMove(_fen, uci);
        if (after == null) return;
        _path.Add(uci);
        _fen = after;
        if (_mode == Mode.Learn) { _mode = Mode.Explore; SetMode(Mode.Explore); RebuildPath(); return; }
        _board.Render(_fen, canMove: true, lastMove: uci, whiteBottom: _whiteBottom);
        RefreshExplore();
    }

    private void Back()
    {
        if (_mode == Mode.Learn) { LearnPrev(); return; }
        if (_mode == Mode.Drill || _path.Count == 0) return;
        _path.RemoveAt(_path.Count - 1);
        _fen = FenOf(_path);
        var last = _path.Count > 0 ? _path[^1] : null;
        _board.Render(_fen, canMove: true, lastMove: last, whiteBottom: _whiteBottom);
        RefreshExplore();
    }

    private void ResetToStart()
    {
        _path.Clear();
        _fen = ChessGame.StartFen;
        _whiteBottom = true;
        if (_board != null) _board.Render(_fen, canMove: _mode != Mode.Learn, lastMove: null, whiteBottom: true);
        if (_mode == Mode.Explore && _bookList != null) RefreshExplore();
        else UpdateHistory();
    }

    private void RebuildPath()
    {
        _board.Render(_fen, canMove: true, lastMove: _path.Count > 0 ? _path[^1] : null, whiteBottom: _whiteBottom);
        RefreshExplore();
    }

    private void Flip()
    {
        _whiteBottom = !_whiteBottom;
        _board.Render(_fen, canMove: _mode != Mode.Learn, lastMove: _path.Count > 0 ? _path[^1] : null, whiteBottom: _whiteBottom);
    }

    private void UpdateHistory() => _historyBar.text = SanPath(_path);

    private static string SanPath(IReadOnlyList<string> path)
    {
        if (path.Count == 0) return "";
        var g = ChessGame.Start();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < path.Count; i++)
        {
            if (i % 2 == 0) sb.Append($"{i / 2 + 1}. ");
            sb.Append(g.SanForUci(path[i]) ?? path[i]).Append(' ');
            g.TryMakeMove(path[i]);
        }
        return sb.ToString().Trim();
    }

    private static string FenOf(IReadOnlyList<string> path)
    {
        var g = ChessGame.Start();
        foreach (var m in path) g.TryMakeMove(m);
        return g.Fen;
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
        if (kb.escapeKey.wasPressedThisFrame) SceneTransition.Go("Menu");
        else if (kb.fKey.wasPressedThisFrame && _book != null) Flip();
        else if (kb.leftArrowKey.wasPressedThisFrame) Back();
        else if (kb.rightArrowKey.wasPressedThisFrame && _mode == Mode.Learn) LearnNext();
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
