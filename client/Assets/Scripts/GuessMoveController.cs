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

// Guess the Move: replay a famous game as one side and predict each move; you score when you find the
// master's move, and the real move is revealed either way so you always follow the actual game. A study
// aid over out-of-copyright master games. 2D board only (a study view; the flat board is clearest).
public sealed class GuessMoveController : MonoBehaviour
{
    private Board2D _board;
    private GuessMoveSession _session;
    private int _gameIndex;
    private string _from;

    private VisualElement _root, _overlayHost, _boardHost;
    private Label _title, _score, _feedback, _sideLabel;

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
        _root.Add(UiKit.NavRail("Guess the Move"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 18, 24, 18, 24);

        var head = UiKit.Row();
        head.style.width = 480; head.style.justifyContent = Justify.SpaceBetween; head.style.marginBottom = 6;
        _title = UiKit.Text_("", 18, UiKit.Text, bold: true);
        _score = UiKit.Text_("", 18, UiKit.Gold, bold: true);
        head.Add(_title); head.Add(_score);
        center.Add(head);

        _sideLabel = UiKit.Text_("", 13, UiKit.Dim, bold: true);
        _sideLabel.style.marginBottom = 6;
        center.Add(_sideLabel);

        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        _board = new Board2D(null);
        _board.Root.style.width = 480; _board.Root.style.height = 480;
        _boardHost.Add(_board.Root);
        center.Add(_boardHost);

        var ctrls = UiKit.Row(); ctrls.style.marginTop = 12;
        ctrls.Add(UiKit.Ghost("Next game", NextGame, 14));
        center.Add(ctrls);

        _feedback = UiKit.Text_("", 15, UiKit.Dim, bold: true);
        _feedback.style.marginTop = 8; _feedback.style.unityTextAlign = TextAnchor.MiddleCenter;
        UiKit.Pad(_feedback, 4, 12, 4, 12); UiKit.Radius(_feedback, 8);
        _feedback.style.whiteSpace = WhiteSpace.Normal; _feedback.style.maxWidth = 480;
        center.Add(_feedback);
        _root.Add(center);

        _board.Render(ChessGame.StartFen, canMove: false, lastMove: null, whiteBottom: true);

        _overlayHost = new VisualElement();
        _overlayHost.style.position = Position.Absolute;
        _overlayHost.style.left = 0; _overlayHost.style.top = 0; _overlayHost.style.right = 0; _overlayHost.style.bottom = 0;
        _root.Add(_overlayHost);

        if (Environment.GetCommandLineArgs().Contains("-kaissa-guesstest"))
            StartCoroutine(AutoDemo());
        else
            ShowIntro();
    }

    private void ShowIntro()
    {
        var dim = Overlay();
        var panel = UiKit.OverlayCard(); panel.style.width = 470;
        panel.Add(UiKit.Text_("Guess the Move", 28, UiKit.Text, bold: true));
        var sub = UiKit.Text_("Play through a famous game as one of the players and predict each move. You score when you find the move the master played; the real move is shown either way, so you follow the whole game.", 14, UiKit.Dim);
        sub.style.whiteSpace = WhiteSpace.Normal; sub.style.marginTop = 10; sub.style.marginBottom = 18;
        sub.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(sub);
        var start = UiKit.Primary("Start", () => { _overlayHost.Clear(); LoadGame(0); }, 16); start.style.width = 320;
        panel.Add(start);
        dim.Add(panel); _overlayHost.Add(dim);
    }

    private void NextGame() { _overlayHost.Clear(); LoadGame(_gameIndex + 1); }

    private void LoadGame(int index)
    {
        KaissaStreak.RecordToday();
        var all = MasterGames.All;
        _gameIndex = ((index % all.Count) + all.Count) % all.Count;
        var g = all[_gameIndex];
        _session = new GuessMoveSession(g);
        _from = null;
        _title.text = $"{g.White} - {g.Black}, {g.Year}";
        _sideLabel.text = $"You are {(g.PlayerSide == Side.White ? "White" : "Black")} - find the move";
        SetFeedback("", UiKit.Dim);
        Render();
    }

    private void Render()
    {
        bool whiteBottom = _session.Fen.Split(' ').Length < 2 || _session.Fen.Split(' ')[1] == "w";
        _board.SquareClickHandler = _session.Done ? (Action<string>)null : OnPick;
        _board.Render(_session.Fen, canMove: false, lastMove: null, whiteBottom: whiteBottom);
        _score.text = $"{_session.Score} / {_session.Answered}";
    }

    private void OnPick(string sq)
    {
        if (_session == null || _session.Done || string.IsNullOrEmpty(sq)) return;
        if (_from == null) { _from = sq; _board.HighlightSquare(sq, Sel); return; }
        if (sq == _from) { _from = null; Render(); return; }

        var res = _session.Guess(_from + sq);
        _from = null;
        SetFeedback(res.Correct ? $"Correct - {res.ActualSan}" : $"Master played {res.ActualSan}",
            res.Correct ? UiKit.GreenHi : UiKit.Danger);
        Render();
        if (_session.Done)
        {
            _board.SquareClickHandler = null;
            StartCoroutine(EndAfter());
        }
    }

    private IEnumerator EndAfter()
    {
        yield return new WaitForSeconds(0.6f);
        var dim = Overlay();
        var panel = UiKit.OverlayCard(); panel.style.width = 420;
        panel.Add(UiKit.Text_("Game complete", 24, UiKit.Text, bold: true));
        var sub = UiKit.Text_($"You matched {_session.Score} of {_session.Answered} of the master's moves.", 14, UiKit.Dim);
        sub.style.whiteSpace = WhiteSpace.Normal; sub.style.marginTop = 8; sub.style.marginBottom = 16;
        sub.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(sub);
        var next = UiKit.Primary("Next game", NextGame, 15); next.style.width = 300; next.style.marginBottom = 8;
        panel.Add(next);
        panel.Add(UiKit.Ghost("Back to menu", () => SceneTransition.Go("Menu"), 14));
        dim.Add(panel); _overlayHost.Add(dim);
    }

    private void SetFeedback(string text, Color color)
    {
        _feedback.text = text;
        _feedback.style.color = color;
        _feedback.style.backgroundColor = string.IsNullOrEmpty(text) ? new Color(0, 0, 0, 0) : new Color(color.r, color.g, color.b, 0.14f);
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

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneTransition.Go("Menu");
    }

    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);
        ShowIntro();
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "guess_intro.png"));
        _overlayHost.Clear(); LoadGame(0);
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "guess_board.png"));
        // Guess the opening move correctly (e2e4 for the Opera Game).
        OnPick("e2"); yield return new WaitForSeconds(0.2f); OnPick("e4");
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "guess_correct.png"));
        // Now a wrong guess to show the reveal.
        OnPick("a2"); yield return new WaitForSeconds(0.2f); OnPick("a3");
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "guess_reveal.png"));
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
