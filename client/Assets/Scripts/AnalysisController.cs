using System;
using System.Collections;
using System.IO;
using System.Threading;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Analysis board: explore any position freely — play/branch moves, step back and forth, and see the
// engine's evaluation, best move, and line for the current position. Navigation is the core
// AnalysisSession; evaluation is KaissaAnalysis (Stockfish).
public sealed class AnalysisController : MonoBehaviour
{
    private readonly AnalysisSession _session = new();
    private IBoardView _board;
    private VisualElement _boardHost;
    private PieceAudio _audio;
    private KaissaAnalysis _engine;
    private CancellationTokenSource _evalCts;
    private bool _whiteBottom = true;
    private string _lastMove;

    private Label _eval;
    private Label _best;
    private VisualElement _movesBody;

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
        var root = doc.rootVisualElement;
        root.Clear();
        root.style.flexDirection = FlexDirection.Row; root.style.flexGrow = 1; root.style.backgroundColor = UiKit.Bg;
        root.Add(UiKit.NavRail("Analysis"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 24, 24, 24, 24);
        center.Add(UiKit.Text_("Analysis", 24, UiKit.Text, bold: true));

        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0; _boardHost.style.marginTop = 12;
        center.Add(_boardHost);

        var nav = UiKit.Row(
            NavBtn("|<", _session.GoToStart), NavBtn("<", () => _session.StepBack()),
            NavBtn(">", () => _session.StepForward()), NavBtn(">|", _session.GoToEnd),
            NavBtn("Flip", () => _whiteBottom = !_whiteBottom), NavBtn("Reset", Reset));
        nav.style.marginTop = 12;
        center.Add(nav);
        root.Add(center);

        root.Add(BuildRightRail());

        _board = BoardMount.Create(gameObject, _boardHost, root, uci => OnMove(uci), _audio);
        StartCoroutine(StartEngine());
        RenderCurrent();
    }

    private VisualElement BuildRightRail()
    {
        var rail = new VisualElement();
        rail.style.width = 340; UiKit.Pad(rail, 24, 24, 24, 0);

        var panel = Panel();
        _eval = UiKit.Text_("Evaluation —", 20, UiKit.Text, bold: true); UiKit.Pad(_eval, 16, 16, 4, 16);
        panel.Add(_eval);
        _best = UiKit.Text_("", 14, UiKit.Dim); UiKit.Pad(_best, 0, 16, 14, 16); _best.style.whiteSpace = WhiteSpace.Normal;
        panel.Add(_best);
        rail.Add(panel);

        var moves = Panel(); moves.style.marginTop = 16;
        var hd = UiKit.Text_("Line", 15, UiKit.Text, bold: true); UiKit.Pad(hd, 14, 16, 14, 16);
        hd.style.borderBottomWidth = 1; hd.style.borderBottomColor = UiKit.Line;
        moves.Add(hd);
        var scroll = new ScrollView(); scroll.style.maxHeight = 260; _movesBody = scroll.contentContainer;
        moves.Add(scroll);
        rail.Add(moves);
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

    private VisualElement NavBtn(string label, Action onClick)
    {
        var b = UiKit.Ghost(label, () => { onClick(); RenderCurrent(); }, 13);
        b.style.marginLeft = 3; b.style.marginRight = 3; b.style.minWidth = 46;
        return b;
    }

    private void OnMove(string uci)
    {
        if (_session.Play(uci)) { _lastMove = uci; RenderCurrent(); }
    }

    private void Reset() { _session.LoadFen(Kaissa.Chess.Rules.ChessGame.StartFen); _lastMove = null; }

    private void RenderCurrent()
    {
        _board.Render(_session.CurrentFen, canMove: true, lastMove: _lastMove, whiteBottom: _whiteBottom);
        RebuildMoves();
        Evaluate();
    }

    private void RebuildMoves()
    {
        if (_movesBody == null) return;
        _movesBody.Clear();
        var san = _session.LineSan();
        for (int i = 0; i < san.Count; i += 2)
        {
            string w = san[i];
            string b = i + 1 < san.Count ? san[i + 1] : "";
            var row = UiKit.Row(Cell($"{i / 2 + 1}.", 40, UiKit.Mute), Cell(w, 120, UiKit.Text), Cell(b, 120, UiKit.Text));
            if ((i / 2) % 2 == 1) row.style.backgroundColor = UiKit.Panel3;
            UiKit.Pad(row, 6, 12, 6, 12);
            _movesBody.Add(row);
        }
    }

    private static Label Cell(string s, float w, Color c) { var l = UiKit.Text_(s, 14, c); l.style.width = w; return l; }

    private IEnumerator StartEngine()
    {
        var enginePath = Path.Combine(Application.streamingAssetsPath, "stockfish", "stockfish.exe");
        if (!File.Exists(enginePath)) { _eval.text = "Engine not found"; yield break; }
        var task = KaissaAnalysis.StartAsync(enginePath);
        while (!task.IsCompleted) yield return null;
        if (task.IsFaulted) { _eval.text = "Engine failed"; Debug.LogError(task.Exception); yield break; }
        _engine = task.Result;
        Evaluate();
    }

    private async void Evaluate()
    {
        if (_engine == null) return;
        _evalCts?.Cancel();
        _evalCts = new CancellationTokenSource();
        var ct = _evalCts.Token;
        string fen = _session.CurrentFen;
        try
        {
            var line = await _engine.EvaluateAsync(fen, depth: 16, ct);
            if (ct.IsCancellationRequested) return;
            _eval.text = string.IsNullOrEmpty(line.Score) ? "Evaluation —" : $"Evaluation  {line.Score}";
            _best.text = string.IsNullOrEmpty(line.BestMove) ? "" : $"Best: {line.BestMove}";
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Debug.LogError(e); }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame) SceneManager.LoadScene("Menu");
        else if (Keyboard.current.leftArrowKey.wasPressedThisFrame) { _session.StepBack(); RenderCurrent(); }
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame) { _session.StepForward(); RenderCurrent(); }
        else if (Keyboard.current.fKey.wasPressedThisFrame) { _whiteBottom = !_whiteBottom; RenderCurrent(); }
    }

    private void OnDestroy()
    {
        _evalCts?.Cancel();
        if (_engine != null) _ = _engine.DisposeAsync();
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
