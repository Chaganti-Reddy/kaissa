using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Training screen (Puzzles), redesigned in UI Toolkit + the 2D board. Three modes share the screen:
// the adaptive spaced loop, the daily puzzle (DailyRoute), and a themed drill of one pattern
// (ThemeRoute). Keeps the grading, spacing, hint-as-lapse and streak logic — only the view changed.
public sealed class KaissaBoardController : MonoBehaviour
{
    private KaissaTrainer _trainer;
    private Board2D _board;
    private PieceAudio _audio;
    private bool _busy;
    private bool _whiteBottom = true;
    private bool _hintUsed;
    private float _cardShownTime;
    private string _patternDesc = "";
    private string _currentFen;

    private bool _dailyMode;
    private bool _themedMode;
    private ThemedSession _themed;
    private Scenario _themedScenario;
    private Scenario _dailyScenario;
    private string _themedPatternName = "";

    // session summary
    private int _answered, _correct;
    private double _ratingStart;
    private bool _summaryShown;

    private VisualElement _root;
    private Label _titleLabel;
    private Label _promptLabel;
    private Label _feedbackLabel;
    private Label _ratingLabel;

    private static readonly Color Good = new(0.30f, 0.85f, 0.45f, 0.55f);
    private static readonly Color Bad = new(0.92f, 0.33f, 0.33f, 0.55f);
    private static readonly Color HintCol = new(0.51f, 0.72f, 0.30f, 0.55f);

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }

        _trainer = KaissaTrainer.CreateDefault(KaissaProgress.Load());
        KaissaPractice.FoldInto(_trainer);
        _ratingStart = _trainer.PlayerRating;

        _audio = PieceAudio.Attach(gameObject);
        _board = new Board2D(uci => OnPlayerMove(uci));

        var doc = gameObject.AddComponent<UIDocument>();
        doc.panelSettings = Resources.Load<PanelSettings>("KaissaPanel");
        StartCoroutine(BuildUi(doc));
    }

    private IEnumerator BuildUi(UIDocument doc)
    {
        yield return null;
        _root = doc.rootVisualElement;
        _root.Clear();
        _root.style.flexDirection = FlexDirection.Row;
        _root.style.flexGrow = 1;
        _root.style.backgroundColor = UiKit.Bg;

        _root.Add(UiKit.NavRail("SampleScene"));
        _root.Add(BuildCenter());
        _root.Add(BuildRightRail());

        if (DailyRoute.Active) { DailyRoute.Active = false; StartDaily(); }
        else if (!string.IsNullOrEmpty(ThemeRoute.PatternId))
        {
            string pid = ThemeRoute.PatternId;
            _themedPatternName = string.IsNullOrEmpty(ThemeRoute.PatternName) ? pid : ThemeRoute.PatternName;
            ThemeRoute.PatternId = null; ThemeRoute.PatternName = null;
            StartThemed(pid);
        }
        else DealNext();
    }

    private VisualElement BuildCenter()
    {
        var center = new VisualElement();
        center.style.flexGrow = 1;
        center.style.alignItems = Align.Center;
        UiKit.Pad(center, 22, 24, 22, 24);

        _titleLabel = UiKit.Text_("Puzzles", 24, UiKit.Text, bold: true);
        center.Add(_titleLabel);
        _promptLabel = UiKit.Text_("", 15, UiKit.Dim);
        _promptLabel.style.marginBottom = 12; _promptLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        center.Add(_promptLabel);

        var host = new VisualElement();
        host.style.width = 480; host.style.height = 480; host.style.flexShrink = 0;
        host.Add(_board.Root);
        _board.Root.style.width = 480; _board.Root.style.height = 480;
        center.Add(host);

        _feedbackLabel = UiKit.Text_("", 16, UiKit.Dim, bold: true);
        _feedbackLabel.style.marginTop = 12; _feedbackLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        center.Add(_feedbackLabel);
        return center;
    }

    private VisualElement BuildRightRail()
    {
        var rail = new VisualElement();
        rail.style.width = 340;
        UiKit.Pad(rail, 24, 24, 24, 0);

        var panel = Panel();
        _ratingLabel = UiKit.Text_("", 15, UiKit.Text, bold: true);
        UiKit.Pad(_ratingLabel, 14, 16, 6, 16);
        panel.Add(_ratingLabel);
        var hint = UiKit.Ghost("Hint  (counts as assisted)", ShowHint, 13);
        hint.style.marginLeft = 16; hint.style.marginRight = 16; hint.style.marginBottom = 14;
        panel.Add(hint);
        rail.Add(panel);
        return rail;
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

    // ---- adaptive ----
    private void DealNext()
    {
        var card = _trainer.NextCard();
        if (card == null) { _titleLabel.text = "No more cards."; return; }
        _hintUsed = false;
        _patternDesc = card.PatternDescription;
        _cardShownTime = Time.time;
        _titleLabel.text = card.PatternName;
        _promptLabel.text = card.Prompt;
        _ratingLabel.text = $"Rating {card.PlayerRating:0}   ·   Solved {_correct}/{_answered}";
        RenderCard(card.Board);
    }

    private void RenderCard(BoardView board)
    {
        _currentFen = board.Fen;
        _whiteBottom = !KaissaSettings.Flip || board.WhiteToMove;
        _board.Render(board.Fen, canMove: true, lastMove: null, whiteBottom: _whiteBottom);
    }

    private void OnPlayerMove(string uci)
    {
        if (_summaryShown) return;
        if (_dailyMode) { OnDailyMove(uci); return; }
        if (_themedMode) { OnThemedMove(uci); return; }
        if (_busy) return;
        _busy = true;

        var thinkTime = TimeSpan.FromSeconds(Time.time - _cardShownTime);
        var result = _trainer.Answer(uci, thinkTime, _hintUsed);
        KaissaProgress.Save(_trainer.ExportProgress());
        _answered++;
        if (result.Correct) _correct++;
        KaissaStreak.RecordToday();

        var afterFen = ApplyMove(_currentFen, uci);
        if (afterFen != null) { _currentFen = afterFen; _board.Render(afterFen, false, uci, _whiteBottom); }

        var color = result.Correct ? Good : Bad;
        if (result.Solutions.Count > 0) HighlightSolution(result.Solutions[0], color);
        _feedbackLabel.style.color = result.Correct ? UiKit.GreenHi : UiKit.Danger;
        _feedbackLabel.text = _hintUsed
            ? $"With a hint — best was {string.Join(", ", result.Solutions)}. It'll come back soon."
            : result.Correct ? $"Correct!  {_patternDesc}" : $"Missed — best was {string.Join(", ", result.Solutions)}";
        _ratingLabel.text = $"Rating {result.PlayerRating:0}  ({result.PlayerRatingChange:+0;-0})   ·   Solved {_correct}/{_answered}";
        if (result.Correct) _audio.PlayCorrect(); else _audio.PlayWrong();

        StartCoroutine(NextAfter(result.Correct ? 0.9f : 1.6f));
    }

    private IEnumerator NextAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _feedbackLabel.text = "";
        _busy = false;
        DealNext();
    }

    // ---- themed ----
    private void StartThemed(string patternId)
    {
        _themedMode = true;
        _themed = new ThemedSession(ScenarioLibrary.LoadDefault(), new PatternId(patternId), _trainer.PlayerRating);
        _titleLabel.text = $"Practice: {_themedPatternName}";
        ThemedNext();
    }

    private void ThemedNext()
    {
        _themedScenario = _themed.Next();
        _hintUsed = false;
        _cardShownTime = Time.time;
        _promptLabel.text = _themedScenario.Prompt;
        _ratingLabel.text = $"Score {_themed.Score}/{_themed.Attempts}";
        var board = BoardView.FromFen(_themedScenario.Fen);
        RenderCard(board);
    }

    private void OnThemedMove(string uci)
    {
        if (_busy) return;
        _busy = true;
        var result = _themed.Submit(uci, TimeSpan.FromSeconds(Time.time - _cardShownTime));
        KaissaStreak.RecordToday();
        var afterFen = ApplyMove(_themedScenario.Fen, uci);
        if (afterFen != null) _board.Render(afterFen, false, uci, _whiteBottom);
        if (result.Solutions.Count > 0) HighlightSolution(result.Solutions[0], result.Correct ? Good : Bad);
        _feedbackLabel.style.color = result.Correct ? UiKit.GreenHi : UiKit.Danger;
        _feedbackLabel.text = result.Correct ? "Correct!" : $"Missed — best was {string.Join(", ", result.Solutions)}";
        if (result.Correct) _audio.PlayCorrect(); else _audio.PlayWrong();
        StartCoroutine(ThemedNextAfter(result.Correct ? 0.9f : 1.6f));
    }

    private IEnumerator ThemedNextAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _feedbackLabel.text = "";
        _busy = false;
        ThemedNext();
    }

    // ---- daily ----
    private void StartDaily()
    {
        _dailyMode = true;
        string today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _dailyScenario = DailyPuzzle.ForDate(ScenarioLibrary.LoadDefault(), DateTime.Today);
        _titleLabel.text = $"Daily puzzle — {today}";
        _promptLabel.text = _dailyScenario.Prompt;
        _ratingLabel.text = $"Puzzle {_dailyScenario.Rating}";
        var board = BoardView.FromFen(_dailyScenario.Fen);
        _whiteBottom = !KaissaSettings.Flip || board.WhiteToMove;
        _currentFen = _dailyScenario.Fen;

        bool done = KaissaSettings.DailyDone == today;
        _board.Render(_dailyScenario.Fen, canMove: !done, lastMove: null, whiteBottom: _whiteBottom);
        if (done) { _feedbackLabel.style.color = UiKit.GreenHi; _feedbackLabel.text = "Already solved today. Come back tomorrow."; }
    }

    private void OnDailyMove(string uci)
    {
        if (_busy) return;
        _busy = true;
        bool correct = false;
        foreach (var s in _dailyScenario.Solutions)
            if (string.Equals(s, uci, StringComparison.OrdinalIgnoreCase)) { correct = true; break; }

        var afterFen = ApplyMove(_dailyScenario.Fen, uci);
        if (afterFen != null) _board.Render(afterFen, false, uci, _whiteBottom);
        if (_dailyScenario.Solutions.Count > 0) HighlightSolution(_dailyScenario.Solutions[0], correct ? Good : Bad);
        _feedbackLabel.style.color = correct ? UiKit.GreenHi : UiKit.Danger;
        _feedbackLabel.text = correct
            ? "Correct! Daily solved. Come back tomorrow."
            : $"Missed — best was {string.Join(", ", _dailyScenario.Solutions)}.";
        if (correct) { KaissaSettings.DailyDone = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); _audio.PlayCorrect(); }
        else _audio.PlayWrong();
        KaissaStreak.RecordToday();
    }

    // ---- shared ----
    private void ShowHint()
    {
        if (_busy || _summaryShown || _themedMode || _dailyMode) return; // hint in the adaptive loop
        var sq = _trainer.Hint();
        if (sq != null) { _board.HighlightSquare(sq, HintCol); _hintUsed = true; }
    }

    private void HighlightSolution(string uci, Color color)
    {
        if (string.IsNullOrEmpty(uci) || uci.Length < 4) return;
        _board.HighlightSquare(uci.Substring(0, 2), color);
        _board.HighlightSquare(uci.Substring(2, 2), color);
    }

    private static string ApplyMove(string fen, string uci)
    {
        try { var g = ChessGame.FromFen(fen); if (g.TryMakeMove(uci)) return g.Fen; }
        catch { }
        return null;
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_answered > 0 && !_summaryShown && !_dailyMode) ShowSummary();
            else SceneManager.LoadScene("Menu");
        }
        else if (Keyboard.current.hKey.wasPressedThisFrame) ShowHint();
        else if (Keyboard.current.fKey.wasPressedThisFrame && _currentFen != null)
        {
            _whiteBottom = !_whiteBottom;
            _board.Render(_currentFen, !_busy, null, _whiteBottom);
        }
    }

    private void ShowSummary()
    {
        _summaryShown = true;
        var dim = new VisualElement();
        dim.style.position = Position.Absolute;
        dim.style.left = 0; dim.style.top = 0; dim.style.right = 0; dim.style.bottom = 0;
        dim.style.backgroundColor = new Color(0, 0, 0, 0.78f);
        dim.style.alignItems = Align.Center; dim.style.justifyContent = Justify.Center;

        var panel = Panel();
        UiKit.Pad(panel, 28); panel.style.alignItems = Align.Center;
        int pct = _answered > 0 ? Mathf.RoundToInt(100f * _correct / _answered) : 0;
        int delta = Mathf.RoundToInt((float)(_trainer.PlayerRating - _ratingStart));
        panel.Add(UiKit.Text_("Session summary", 26, UiKit.Text, bold: true));
        var s1 = UiKit.Text_($"Solved {_correct}/{_answered}   ({pct}%)", 17, UiKit.Dim); s1.style.marginTop = 10; panel.Add(s1);
        var s2 = UiKit.Text_($"Rating {_ratingStart:0} → {_trainer.PlayerRating:0}   ({delta:+0;-0})", 17, UiKit.Dim);
        s2.style.marginTop = 4; s2.style.marginBottom = 18; panel.Add(s2);
        var keep = UiKit.Primary("Keep training", () => { _root.Remove(dim); _summaryShown = false; }, 15);
        keep.style.width = 300; keep.style.marginBottom = 8; panel.Add(keep);
        var back = UiKit.Ghost("Back to menu", () => SceneManager.LoadScene("Menu")); back.style.width = 300; panel.Add(back);
        dim.Add(panel);
        _root.Add(dim);
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
