using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Visualization / blindfold trainer: solve a short tactic while the pieces are faded. Each solve fades
// them further (50% -> 30% -> 15% -> fully blind), so you learn to hold the position in your mind's eye
// and calculate without seeing it - the pattern-chunk recall the whole app is built around. A wrong move
// reveals the board and ends the run; the best run is kept. 2D board only (the flat board is clearest).
public sealed class VisualizationController : MonoBehaviour
{
    // Graded transparency ladder (Lucas Chess R style): faint -> fainter -> gone.
    private static readonly float[] Ladder = { 0.5f, 0.3f, 0.15f, 0.0f };

    private Board2D _board;
    private List<Scenario> _pool = new();
    private int _poolAt;
    private Scenario _scenario;
    private string _from;
    private int _score;
    private bool _solving;

    private VisualElement _root, _overlayHost, _boardHost;
    private Label _phase, _visLabel, _scoreLabel, _bestLabel, _feedback, _sideLabel;

    private static readonly Color Sel = new(0.36f, 0.72f, 0.42f, 0.55f);
    private static readonly Color Miss = new(0.86f, 0.30f, 0.28f, 0.60f);

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
        _root.Add(UiKit.NavRail("Visualization"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 18, 24, 18, 24);

        var head = UiKit.Row();
        head.style.width = 480; head.style.justifyContent = Justify.SpaceBetween; head.style.marginBottom = 8;
        _phase = UiKit.Text_("", 22, UiKit.Text, bold: true);
        _visLabel = UiKit.Text_("", 22, UiKit.Gold, bold: true);
        head.Add(_phase); head.Add(_visLabel);
        center.Add(head);

        _sideLabel = UiKit.Text_("", 14, UiKit.Dim, bold: true);
        _sideLabel.style.marginBottom = 6;
        center.Add(_sideLabel);

        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        _board = new Board2D(null);
        _board.Root.style.width = 480; _board.Root.style.height = 480;
        _boardHost.Add(_board.Root);
        center.Add(_boardHost);

        _feedback = UiKit.Text_("", 16, UiKit.Dim, bold: true);
        _feedback.style.marginTop = 10; _feedback.style.unityTextAlign = TextAnchor.MiddleCenter;
        UiKit.Pad(_feedback, 4, 12, 4, 12); UiKit.Radius(_feedback, 8);
        center.Add(_feedback);
        _root.Add(center);

        var rail = new VisualElement();
        rail.style.width = 300; UiKit.Pad(rail, 18, 24, 18, 8);
        var panel = Panel(); UiKit.Pad(panel, 16);
        panel.Add(UiKit.Text_("SOLVED", 11, UiKit.Mute, bold: true));
        _scoreLabel = UiKit.Text_("-", 40, UiKit.Text, bold: true);
        panel.Add(_scoreLabel);
        _bestLabel = UiKit.Text_($"Best run {KaissaSettings.VisualizationBest}", 13, UiKit.Dim, bold: true);
        panel.Add(_bestLabel);
        rail.Add(panel);
        _root.Add(rail);

        _board.Render(ChessGame.StartFen, canMove: false, lastMove: null, whiteBottom: true);

        _overlayHost = new VisualElement();
        _overlayHost.style.position = Position.Absolute;
        _overlayHost.style.left = 0; _overlayHost.style.top = 0; _overlayHost.style.right = 0; _overlayHost.style.bottom = 0;
        _root.Add(_overlayHost);

        LoadPool();

        if (Environment.GetCommandLineArgs().Contains("-kaissa-visualizationtest"))
            StartCoroutine(AutoDemo());
        else
            ShowIntro();
    }

    private void LoadPool()
    {
        try
        {
            var trainer = KaissaTrainer.CreateDefault(KaissaProgress.Load());
            // Any easy tactic works - the drill only asks for the first move, graded against Solutions[0].
            _pool = trainer.Library.ByRatingRange(500, 1200)
                .Where(s => s.Solutions.Count > 0)
                .ToList();
        }
        catch { _pool = new List<Scenario>(); }
        Shuffle(_pool);
    }

    private void ShowIntro()
    {
        var dim = Overlay();
        var panel = OverlayPanel(); panel.style.width = 470;
        panel.Add(UiKit.Text_("Visualization", 28, UiKit.Text, bold: true));
        var sub = UiKit.Text_("Solve a tactic while the pieces are faded. Every solve fades them further - 50%, then 30%, then almost nothing, then fully blind - so you learn to see the board in your mind. One wrong move ends the run. How far can you go?", 14, UiKit.Dim);
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
        KaissaStreak.RecordToday(); // visualization counts toward the daily training streak
        if (_pool.Count == 0) { SetFeedback("No puzzles available.", UiKit.Danger); return; }
        _score = 0;
        NextTactic();
    }

    private void NextTactic()
    {
        _scenario = _pool[_poolAt % _pool.Count];
        _poolAt++;
        _from = null;
        _solving = true;

        bool whiteBottom = IsWhiteToMove(_scenario.Fen);
        _scoreLabel.text = _score.ToString();
        _phase.text = "Find the move";
        _sideLabel.text = whiteBottom ? "White to move" : "Black to move";
        float opacity = Ladder[Mathf.Min(_score, Ladder.Length - 1)];
        _visLabel.text = opacity <= 0f ? "blindfold" : $"{Mathf.RoundToInt(opacity * 100)}% visible";
        SetFeedback("", UiKit.Dim);

        _board.SquareClickHandler = OnPick;
        _board.Render(_scenario.Fen, canMove: false, lastMove: null, whiteBottom: whiteBottom);
        _board.SetPieceOpacity(opacity);
    }

    private void OnPick(string sq)
    {
        if (!_solving || string.IsNullOrEmpty(sq)) return;
        if (_from == null)
        {
            _from = sq;
            _board.HighlightSquare(sq, Sel);
            return;
        }
        if (sq == _from) { _from = null; _board.Render(_scenario.Fen, false, null, IsWhiteToMove(_scenario.Fen)); _board.SetPieceOpacity(Ladder[Mathf.Min(_score, Ladder.Length - 1)]); return; }

        string uci = _from + sq;
        bool correct = _scenario.Solutions.Any(s =>
            string.Equals(s, uci, StringComparison.OrdinalIgnoreCase) ||
            (s.Length == uci.Length + 1 && s.StartsWith(uci, StringComparison.OrdinalIgnoreCase))); // promotion (default queen)
        _from = null;

        if (correct)
        {
            _score++;
            SetFeedback("Correct", UiKit.GreenHi);
            NextTactic();
        }
        else
        {
            _solving = false;
            _board.SquareClickHandler = null;
            // Reveal the board and mark the move that was expected.
            _board.Render(_scenario.Fen, canMove: false, lastMove: null, whiteBottom: IsWhiteToMove(_scenario.Fen));
            _board.SetPieceOpacity(1f);
            var best = _scenario.Solutions[0];
            if (best.Length >= 4) { _board.HighlightSquare(best.Substring(0, 2), Miss); _board.HighlightSquare(best.Substring(2, 2), Miss); }
            GameOver();
        }
    }

    private void GameOver()
    {
        if (_score > KaissaSettings.VisualizationBest) KaissaSettings.VisualizationBest = _score;
        _bestLabel.text = $"Best run {KaissaSettings.VisualizationBest}";

        var dim = Overlay();
        var panel = OverlayPanel(); panel.style.width = 420;
        panel.Add(UiKit.Text_(_score > 0 ? "Run over" : "Not quite", 24, UiKit.Text, bold: true));
        string msg = _score > 0
            ? $"You solved {_score} in a row. The move you needed is marked on the board."
            : "The move you needed is marked on the board. Have another go!";
        var sub = UiKit.Text_(msg, 14, UiKit.Dim);
        sub.style.whiteSpace = WhiteSpace.Normal; sub.style.marginTop = 8; sub.style.marginBottom = 16;
        sub.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(sub);
        var again = UiKit.Primary("Play again", StartRun, 15); again.style.width = 300; again.style.marginBottom = 8;
        panel.Add(again);
        panel.Add(UiKit.Ghost("Back to menu", () => SceneTransition.Go("Menu"), 14));
        dim.Add(panel); _overlayHost.Add(dim);
    }

    private static bool IsWhiteToMove(string fen)
    {
        var parts = fen.Split(' ');
        return parts.Length < 2 || parts[1] == "w";
    }

    private void Shuffle<T>(IList<T> list)
    {
        var rng = new System.Random();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
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

    private VisualElement OverlayPanel() => UiKit.OverlayCard(); // scrolling, height-capped modal card

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneTransition.Go("Menu");
    }

    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "vis_warmup.png"));
        yield return new WaitForSeconds(0.6f);
        ShowIntro();
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "vis_intro.png"));
        StartRun();
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "vis_faded.png")); // first tactic at 50%
        // Solve the first tactic correctly to fade further, then screenshot the fainter board.
        if (_scenario != null && _scenario.Solutions.Count > 0)
        {
            var m = _scenario.Solutions[0];
            OnPick(m.Substring(0, 2));
            yield return new WaitForSeconds(0.2f);
            OnPick(m.Substring(2, 2));
            yield return new WaitForSeconds(0.5f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "vis_fainter.png")); // second tactic at 30%
        }
        // Now play a deliberately wrong move to trigger the reveal + game over.
        OnPick("a1"); yield return new WaitForSeconds(0.1f); OnPick("a2");
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "vis_gameover.png"));
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
