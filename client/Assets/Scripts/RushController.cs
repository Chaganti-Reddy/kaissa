using System;
using System.Collections;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Puzzle Blitz: solve as many as possible before three misses. Redesigned in UI Toolkit + the 2D
// board. Drives RushSession; a hint scores nothing and breaks the streak (but costs no life).
public sealed class RushController : MonoBehaviour
{
    private RushSession _rush;
    private Board2D _board;
    private PieceAudio _audio;
    private Scenario _current;
    private float _shownTime;
    private bool _busy;
    private bool _whiteBottom = true;
    private bool _hintUsed;
    private string _currentFen;

    private VisualElement _root;
    private Label _scoreLabel;
    private Label _livesLabel;
    private Label _streakLabel;
    private Label _feedbackLabel;

    private static readonly Color Good = new(0.30f, 0.85f, 0.45f, 0.55f);
    private static readonly Color Bad = new(0.92f, 0.33f, 0.33f, 0.55f);
    private static readonly Color HintCol = new(0.51f, 0.72f, 0.30f, 0.55f);

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }

        _audio = PieceAudio.Attach(gameObject);
        _board = new Board2D(uci => OnPlayerMove(uci));
        _rush = RushSession.CreateDefault(startRating: 800, lives: 3);

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

        _root.Add(UiKit.NavRail("Rush"));
        _root.Add(BuildCenter());
        _root.Add(BuildRightRail());
        DealNext();
    }

    private VisualElement BuildCenter()
    {
        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center;
        UiKit.Pad(center, 22, 24, 22, 24);
        var title = UiKit.Text_("Puzzle Blitz", 24, UiKit.Text, bold: true);
        title.style.marginBottom = 12; center.Add(title);

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
        rail.style.width = 340; UiKit.Pad(rail, 24, 24, 24, 0);

        var panel = new VisualElement();
        panel.style.backgroundColor = UiKit.Panel;
        panel.style.borderTopWidth = panel.style.borderBottomWidth = panel.style.borderLeftWidth = panel.style.borderRightWidth = 1;
        panel.style.borderTopColor = panel.style.borderBottomColor = panel.style.borderLeftColor = panel.style.borderRightColor = UiKit.Line;
        UiKit.Radius(panel, 12); UiKit.Pad(panel, 16);

        var row = UiKit.Row(Stat("Score", out _scoreLabel), Stat("Lives", out _livesLabel), Stat("Streak", out _streakLabel));
        row.style.justifyContent = Justify.SpaceBetween;
        panel.Add(row);

        var hint = UiKit.Ghost("Hint  (no score, breaks streak)", ShowHint, 13);
        hint.style.marginTop = 16;
        panel.Add(hint);
        rail.Add(panel);
        return rail;
    }

    private VisualElement Stat(string key, out Label value)
    {
        var k = UiKit.Text_(key.ToUpperInvariant(), 11, UiKit.Mute, bold: true);
        value = UiKit.Text_("0", 24, UiKit.Text, bold: true);
        return UiKit.Col(k, value);
    }

    private void DealNext()
    {
        var scenario = _rush.Next();
        if (scenario == null) return;
        _current = scenario;
        _shownTime = Time.time;
        _hintUsed = false;
        _currentFen = scenario.Fen;
        var board = BoardView.FromFen(scenario.Fen);
        _whiteBottom = !KaissaSettings.Flip || board.WhiteToMove;
        _board.Render(scenario.Fen, canMove: true, lastMove: null, whiteBottom: _whiteBottom);
        UpdateHud();
    }

    private void OnPlayerMove(string uci)
    {
        if (_busy || _rush.IsOver) return;
        var result = _rush.Submit(uci, TimeSpan.FromSeconds(Time.time - _shownTime), _hintUsed);
        KaissaStreak.RecordToday();

        var afterFen = ApplyMove(_currentFen, uci);
        if (afterFen != null) _board.Render(afterFen, false, uci, _whiteBottom);
        if (result.Solutions.Count > 0) HighlightSolution(result.Solutions[0], result.Correct ? Good : Bad);
        _feedbackLabel.style.color = result.Correct ? UiKit.GreenHi : UiKit.Danger;
        _feedbackLabel.text = result.Correct ? "Correct!" : $"Missed — {string.Join(", ", result.Solutions)}";
        if (result.Correct) _audio.PlayCorrect(); else _audio.PlayWrong();
        UpdateHud();

        StartCoroutine(FeedbackThenNext(result.Correct ? 0.6f : 1.3f, result.IsOver, result.Score));
    }

    private IEnumerator FeedbackThenNext(float seconds, bool over, int score)
    {
        _busy = true;
        yield return new WaitForSeconds(seconds);
        _feedbackLabel.text = "";
        _busy = false;
        if (over)
        {
            _audio.PlayGameEnd();
            _feedbackLabel.style.color = UiKit.Gold;
            _feedbackLabel.text = $"Game over!  Score {score}.   Esc — menu";
        }
        else DealNext();
    }

    private void ShowHint()
    {
        if (_busy || _rush.IsOver) return;
        var sq = _rush.Hint();
        if (sq != null) { _board.HighlightSquare(sq, HintCol); _hintUsed = true; }
    }

    private void HighlightSolution(string uci, Color color)
    {
        if (string.IsNullOrEmpty(uci) || uci.Length < 4) return;
        _board.HighlightSquare(uci.Substring(0, 2), color);
        _board.HighlightSquare(uci.Substring(2, 2), color);
    }

    private void UpdateHud()
    {
        _scoreLabel.text = _rush.Score.ToString();
        _livesLabel.text = _rush.Lives.ToString();
        _streakLabel.text = _rush.Streak.ToString();
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
        if (Keyboard.current.escapeKey.wasPressedThisFrame) SceneManager.LoadScene("Menu");
        else if (Keyboard.current.hKey.wasPressedThisFrame) ShowHint();
        else if (Keyboard.current.fKey.wasPressedThisFrame && _currentFen != null)
        {
            _whiteBottom = !_whiteBottom;
            _board.Render(_currentFen, !_busy, null, _whiteBottom);
        }
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
