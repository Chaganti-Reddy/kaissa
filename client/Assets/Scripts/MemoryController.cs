using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Memory drill (chunk recall): a position is shown for a few seconds, then the board clears and you
// rebuild it from memory by stamping pieces from a palette. Each solved position adds a piece and
// shortens the look time; the best level reached is kept. Trains the pattern-chunk storage the whole
// app is built around. 2D board only (recall is board-agnostic; the flat board is clearest here).
public sealed class MemoryController : MonoBehaviour
{
    private Board2D _board;
    private readonly char[,] _target = new char[8, 8];  // [file, rank], '\0' = empty
    private readonly char[,] _placed = new char[8, 8];
    private char _palette = 'P';
    private int _level;
    private float _memLeft;
    private bool _memorizing;
    private readonly System.Random _rng = new();

    private VisualElement _root, _overlayHost, _boardHost, _paletteHost;
    private Label _phase, _timer, _scoreLabel, _bestLabel, _feedback;
    private Button _submit;

    private static readonly Color Good = new(0.30f, 0.85f, 0.45f, 0.55f);
    private static readonly Color Bad = new(0.92f, 0.33f, 0.33f, 0.55f);

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
        _root.Add(UiKit.NavRail("Memory"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 18, 24, 18, 24);

        var head = UiKit.Row();
        head.style.width = 480; head.style.justifyContent = Justify.SpaceBetween; head.style.marginBottom = 8;
        _phase = UiKit.Text_("", 22, UiKit.Text, bold: true);
        _timer = UiKit.Text_("", 22, UiKit.Gold, bold: true);
        head.Add(_phase); head.Add(_timer);
        center.Add(head);

        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        _board = new Board2D(null);
        _board.Root.style.width = 480; _board.Root.style.height = 480;
        _boardHost.Add(_board.Root);
        center.Add(_boardHost);

        _paletteHost = UiKit.Row();
        _paletteHost.style.flexWrap = Wrap.Wrap; _paletteHost.style.justifyContent = Justify.Center;
        _paletteHost.style.width = 480; _paletteHost.style.marginTop = 10;
        center.Add(_paletteHost);

        _submit = UiKit.Primary("Submit", Submit, 16); _submit.style.width = 200; _submit.style.marginTop = 10;
        center.Add(_submit);

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
        _bestLabel = UiKit.Text_($"Best level {KaissaSettings.MemoryBest}", 13, UiKit.Dim, bold: true);
        panel.Add(_bestLabel);
        rail.Add(panel);
        _root.Add(rail);

        BuildPalette();
        _board.Render("8/8/8/8/8/8/8/8 w - - 0 1", canMove: false, lastMove: null, whiteBottom: true);

        _overlayHost = new VisualElement();
        _overlayHost.style.position = Position.Absolute;
        _overlayHost.style.left = 0; _overlayHost.style.top = 0; _overlayHost.style.right = 0; _overlayHost.style.bottom = 0;
        _root.Add(_overlayHost);

        if (Environment.GetCommandLineArgs().Contains("-kaissa-memorytest"))
            StartCoroutine(AutoDemo());
        else
            ShowIntro();
    }

    private void ShowIntro()
    {
        var dim = Overlay();
        var panel = OverlayPanel(); panel.style.width = 460;
        panel.Add(UiKit.Text_("Memory", 28, UiKit.Text, bold: true));
        var sub = UiKit.Text_("A position appears for a few seconds. Memorize it, then rebuild it from memory by stamping pieces. Each solve adds a piece and shortens the look. How far can you go?", 14, UiKit.Dim);
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
        _level = 1;
        NextPosition();
    }

    private void NextPosition()
    {
        GenerateTarget(_level + 3); // start with 4 pieces (2 kings + 2), +1 per level
        _scoreLabel.text = _level.ToString();
        SetFeedback("", UiKit.Dim);
        _paletteHost.style.display = DisplayStyle.None;
        _submit.style.display = DisplayStyle.None;
        _board.SquareClickHandler = null;
        _board.Render(BuildFen(_target), canMove: false, lastMove: null, whiteBottom: true);
        _phase.text = "Memorize";
        _memLeft = Mathf.Max(2f, 6f - _level * 0.35f);
        _memorizing = true;
    }

    private void Update()
    {
        if (!_memorizing) return;
        _memLeft -= Time.deltaTime;
        _timer.text = Mathf.CeilToInt(Mathf.Max(0, _memLeft)).ToString();
        if (_memLeft <= 0) BeginReconstruct();
    }

    private void BeginReconstruct()
    {
        _memorizing = false;
        _timer.text = "";
        for (int f = 0; f < 8; f++) for (int r = 0; r < 8; r++) _placed[f, r] = '\0';
        _board.Render("8/8/8/8/8/8/8/8 w - - 0 1", canMove: false, lastMove: null, whiteBottom: true);
        _board.SquareClickHandler = OnPlace;
        _paletteHost.style.display = DisplayStyle.Flex;
        _submit.style.display = DisplayStyle.Flex;
        _phase.text = "Rebuild it";
    }

    private void OnPlace(string sq)
    {
        var (f, r) = Square(sq);
        if (f < 0) return;
        _placed[f, r] = _palette; // '\0' from the eraser clears the square
        _board.Render(BuildFen(_placed), canMove: false, lastMove: null, whiteBottom: true);
    }

    private void Submit()
    {
        if (_memorizing) return;
        bool correct = true;
        for (int f = 0; f < 8 && correct; f++)
            for (int r = 0; r < 8; r++)
                if (_placed[f, r] != _target[f, r]) { correct = false; break; }

        if (correct)
        {
            _audioPlayed(true);
            SetFeedback("Correct!", UiKit.GreenHi);
            _level++;
            NextPosition();
        }
        else
        {
            _audioPlayed(false);
            _board.Render(BuildFen(_target), canMove: false, lastMove: null, whiteBottom: true); // reveal the answer
            _board.SquareClickHandler = null;
            _paletteHost.style.display = DisplayStyle.None;
            _submit.style.display = DisplayStyle.None;
            GameOver();
        }
    }

    private void GameOver()
    {
        int reached = _level; // pieces on the last (failed) board; best is the last CLEARED level
        int best = Math.Max(0, _level - 1);
        if (best > KaissaSettings.MemoryBest) KaissaSettings.MemoryBest = best;
        _bestLabel.text = $"Best level {KaissaSettings.MemoryBest}";

        var dim = Overlay();
        var panel = OverlayPanel(); panel.style.width = 420;
        panel.Add(UiKit.Text_("Not quite", 24, UiKit.Text, bold: true));
        var msg = best > 0
            ? $"You rebuilt {best} position(s), the last with {best + 3} pieces. The answer is shown on the board."
            : "The answer is shown on the board. Have another go!";
        var sub = UiKit.Text_(msg, 14, UiKit.Dim);
        sub.style.whiteSpace = WhiteSpace.Normal; sub.style.marginTop = 8; sub.style.marginBottom = 16;
        sub.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(sub);
        var again = UiKit.Primary("Play again", StartRun, 15); again.style.width = 300; again.style.marginBottom = 8;
        panel.Add(again);
        panel.Add(UiKit.Ghost("Back to menu", () => SceneTransition.Go("Menu"), 14));
        dim.Add(panel); _overlayHost.Add(dim);
    }

    private void BuildPalette()
    {
        _paletteHost.Clear();
        foreach (char c in new[] { 'K', 'Q', 'R', 'B', 'N', 'P', 'k', 'q', 'r', 'b', 'n', 'p' })
            _paletteHost.Add(PaletteButton(c));
        _paletteHost.Add(PaletteButton('\0')); // eraser
        HighlightPalette();
    }

    private VisualElement PaletteButton(char piece)
    {
        var b = new VisualElement { name = "palette" };
        b.style.width = 44; b.style.height = 44; b.style.marginRight = 4; b.style.marginBottom = 4;
        b.style.backgroundColor = piece == '\0' ? UiKit.Panel2 : UiKit.Panel;
        UiKit.Radius(b, 8);
        b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        if (piece == '\0')
        {
            var x = UiKit.Text_("x", 18, UiKit.Dim, bold: true);
            x.style.unityTextAlign = TextAnchor.MiddleCenter; x.style.flexGrow = 1;
            b.Add(x);
        }
        else
        {
            var tex = PieceArt.Get(piece);
            var img = new VisualElement { pickingMode = PickingMode.Ignore };
            img.style.flexGrow = 1;
            if (tex != null) img.style.backgroundImage = new StyleBackground(tex);
            else { var l = UiKit.Text_(piece.ToString(), 20, UiKit.Text, bold: true); l.style.unityTextAlign = TextAnchor.MiddleCenter; img.Add(l); }
            b.Add(img);
        }
        b.RegisterCallback<ClickEvent>(_ => { _palette = piece; HighlightPalette(); });
        return b;
    }

    private void HighlightPalette()
    {
        int i = 0;
        var order = new[] { 'K', 'Q', 'R', 'B', 'N', 'P', 'k', 'q', 'r', 'b', 'n', 'p', '\0' };
        foreach (var el in _paletteHost.Children())
        {
            bool sel = order[i] == _palette;
            var edge = sel ? UiKit.Gold : UiKit.Line;
            el.style.borderTopColor = el.style.borderBottomColor = el.style.borderLeftColor = el.style.borderRightColor = edge;
            i++;
        }
    }

    // Random position: two kings (non-adjacent) plus (n-2) random pieces, no pawns on the back ranks.
    private void GenerateTarget(int n)
    {
        for (int f = 0; f < 8; f++) for (int r = 0; r < 8; r++) _target[f, r] = '\0';
        n = Mathf.Clamp(n, 2, 24);

        (int f, int r) wk = RandomEmpty();
        _target[wk.f, wk.r] = 'K';
        (int f, int r) bk;
        do { bk = RandomEmpty(); } while (Math.Abs(bk.f - wk.f) <= 1 && Math.Abs(bk.r - wk.r) <= 1);
        _target[bk.f, bk.r] = 'k';

        char[] types = { 'Q', 'R', 'B', 'N', 'P' };
        int placed = 2;
        int guard = 0;
        while (placed < n && guard++ < 500)
        {
            var (f, r) = RandomEmpty();
            char t = types[_rng.Next(types.Length)];
            if (t == 'P' && (r == 0 || r == 7)) continue; // pawns never on rank 1 or 8
            bool white = _rng.Next(2) == 0;
            _target[f, r] = white ? t : char.ToLowerInvariant(t);
            placed++;
        }
    }

    private (int f, int r) RandomEmpty()
    {
        while (true)
        {
            int f = _rng.Next(8), r = _rng.Next(8);
            if (_target[f, r] == '\0') return (f, r);
        }
    }

    // Placement-only FEN (side to move / rights are irrelevant here; Board2D just draws the placement).
    private static string BuildFen(char[,] board)
    {
        var sb = new System.Text.StringBuilder();
        for (int r = 7; r >= 0; r--)
        {
            int empty = 0;
            for (int f = 0; f < 8; f++)
            {
                char c = board[f, r];
                if (c == '\0') { empty++; continue; }
                if (empty > 0) { sb.Append(empty); empty = 0; }
                sb.Append(c);
            }
            if (empty > 0) sb.Append(empty);
            if (r > 0) sb.Append('/');
        }
        sb.Append(" w - - 0 1");
        return sb.ToString();
    }

    private static (int f, int r) Square(string sq)
    {
        if (string.IsNullOrEmpty(sq) || sq.Length < 2) return (-1, -1);
        int f = sq[0] - 'a', r = sq[1] - '1';
        return (f is >= 0 and < 8 && r is >= 0 and < 8) ? (f, r) : (-1, -1);
    }

    private void _audioPlayed(bool ok) { /* sound hook: memory drill is silent for now */ }

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

    private VisualElement OverlayPanel()
    {
        var p = new VisualElement();
        p.style.backgroundColor = UiKit.Panel;
        UiKit.Pad(p, 28); UiKit.Radius(p, 14);
        p.style.alignItems = Align.Center;
        return p;
    }

    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "mem_warmup.png"));
        yield return new WaitForSeconds(0.6f);
        ShowIntro();
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "mem_intro.png"));
        StartRun();
        yield return new WaitForSeconds(0.4f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "mem_memorize.png"));
        // let the memorize timer elapse into the reconstruct phase
        yield return new WaitForSeconds(6.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "mem_reconstruct.png"));
        // stamp a couple of pieces to show placement, then submit (likely wrong -> game over)
        _palette = 'K'; HighlightPalette(); OnPlace("e1");
        yield return new WaitForSeconds(0.3f);
        _palette = 'k'; HighlightPalette(); OnPlace("e8");
        yield return new WaitForSeconds(0.3f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "mem_placed.png"));
        Submit();
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "mem_gameover.png"));
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
