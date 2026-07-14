using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Settings, rebuilt: a live 2D board preview + theme swatches on the left, and grouped setting rows on
// the right (board & pieces, gameplay, sound & display, data). Toggles read as on/off pills; the theme
// and coordinate changes update the preview instantly. Reset asks for confirmation before wiping.
public sealed class SettingsController : MonoBehaviour
{
    private const string PreviewFen = "r1bqk2r/pppp1ppp/2n2n2/2b1p3/2B1P3/2N2N2/PPPP1PPP/R1BQ1RK1 w kq - 0 1";

    private VisualElement _root, _groups, _swatches, _pieceSets, _soundThemes, _boardHost;
    private Board2D _preview;
    private PieceAudio _audio;
    private bool _confirmReset;

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }
        _preview = new Board2D(null);
        _audio = PieceAudio.Attach(gameObject);
        if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null && cam != null)
            cam.gameObject.AddComponent<AudioListener>();
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
        _root.Add(UiKit.NavRail("Settings"));

        var main = new VisualElement();
        main.style.flexGrow = 1; UiKit.Pad(main, 26, 34, 20, 34);
        main.Add(UiKit.Text_("Settings", 26, UiKit.Text, bold: true));

        var cols = UiKit.Row(); cols.style.marginTop = 16; cols.style.flexGrow = 1; cols.style.alignItems = Align.FlexStart;

        // Scrollable so every card is reachable even at short window heights (the pickers used to
        // fall below the fold).
        var left = UiKit.Scroll();
        left.style.width = 320; left.style.marginRight = 24; left.style.flexShrink = 0;
        left.style.alignSelf = Align.Stretch;
        var prevCard = Panel(); UiKit.Pad(prevCard, 14);
        prevCard.Add(UiKit.Text_("PREVIEW", 11, UiKit.Mute, bold: true));
        _boardHost = new VisualElement();
        _boardHost.style.width = 280; _boardHost.style.height = 280; _boardHost.style.marginTop = 8;
        _boardHost.Add(_preview.Root);
        _preview.Root.style.width = 280; _preview.Root.style.height = 280;
        prevCard.Add(_boardHost);
        left.Add(prevCard);

        var themeCard = Panel(); themeCard.style.marginTop = 14; UiKit.Pad(themeCard, 12, 14, 12, 14);
        themeCard.Add(UiKit.Text_("Board theme", 12, UiKit.Mute, bold: true));
        _swatches = new VisualElement();
        _swatches.style.flexDirection = FlexDirection.Row; _swatches.style.flexWrap = Wrap.Wrap; _swatches.style.marginTop = 8;
        themeCard.Add(_swatches);
        left.Add(themeCard);

        var pieceCard = Panel(); pieceCard.style.marginTop = 14; UiKit.Pad(pieceCard, 12, 14, 12, 14);
        pieceCard.Add(UiKit.Text_("Piece set", 12, UiKit.Mute, bold: true));
        _pieceSets = new VisualElement();
        _pieceSets.style.flexDirection = FlexDirection.Row; _pieceSets.style.flexWrap = Wrap.Wrap; _pieceSets.style.marginTop = 8;
        pieceCard.Add(_pieceSets);
        left.Add(pieceCard);

        var soundCard = Panel(); soundCard.style.marginTop = 14; UiKit.Pad(soundCard, 12, 14, 12, 14);
        soundCard.Add(UiKit.Text_("Sound theme", 12, UiKit.Mute, bold: true));
        _soundThemes = new VisualElement();
        _soundThemes.style.flexDirection = FlexDirection.Row; _soundThemes.style.flexWrap = Wrap.Wrap; _soundThemes.style.marginTop = 8;
        soundCard.Add(_soundThemes);
        left.Add(soundCard);
        cols.Add(left);

        var scroll = UiKit.Scroll(); scroll.style.flexGrow = 1; scroll.style.maxWidth = 620;
        _groups = scroll.contentContainer;
        cols.Add(scroll);

        main.Add(cols);
        _root.Add(main);

        RefreshPreview();
        RefreshSwatches();
        RefreshPieceSets();
        RefreshSoundThemes();
        RefreshGroups();

        if (Environment.GetCommandLineArgs().Contains("-kaissa-settingstest"))
            StartCoroutine(AutoDemo());
    }

    private void RefreshPreview()
    {
        _preview.ShowCoordinates = KaissaSettings.Coordinates;
        _preview.Render(PreviewFen, canMove: false, lastMove: null, whiteBottom: true);
    }

    private void RefreshSwatches()
    {
        _swatches.Clear();
        for (int i = 0; i < Board3D.Themes.Length; i++)
        {
            int idx = i;
            var th = Board3D.Themes[i];
            bool selected = KaissaSettings.BoardTheme == i;

            var sw = new VisualElement();
            sw.style.width = 46; sw.style.height = 46; sw.style.marginRight = 8; sw.style.marginBottom = 8;
            sw.style.flexDirection = FlexDirection.Row; sw.style.flexWrap = Wrap.Wrap; sw.style.overflow = Overflow.Hidden;
            UiKit.Radius(sw, 8);
            sw.style.borderTopWidth = sw.style.borderBottomWidth = sw.style.borderLeftWidth = sw.style.borderRightWidth = 2;
            var edge = selected ? UiKit.Gold : UiKit.Line;
            sw.style.borderTopColor = sw.style.borderBottomColor = sw.style.borderLeftColor = sw.style.borderRightColor = edge;
            // 2x2 mini checker
            AddCell(sw, th.Light); AddCell(sw, th.Dark); AddCell(sw, th.Dark); AddCell(sw, th.Light);
            sw.tooltip = th.Name;
            sw.RegisterCallback<ClickEvent>(_ =>
            {
                KaissaSettings.BoardTheme = idx;
                RefreshPreview(); RefreshSwatches();
            });
            UiKit.Interactive(sw, 1.06f);
            _swatches.Add(sw);
        }
        var name = UiKit.Text_(Board3D.Themes[Mathf.Clamp(KaissaSettings.BoardTheme, 0, Board3D.Themes.Length - 1)].Name, 13, UiKit.Text, bold: true);
        name.style.width = Length.Percent(100); name.style.marginTop = 2;
        _swatches.Add(name);
    }

    private void RefreshPieceSets()
    {
        _pieceSets.Clear();
        var square = UiKit.Hex(0xb5, 0x88, 0x63); // a neutral dark square behind the sample piece
        foreach (var (name, folder) in PieceArt.Sets)
        {
            var tex = PieceArt.Get(folder, "wK");
            if (tex == null) continue;
            bool selected = KaissaSettings.PieceSet == folder;

            var sw = new VisualElement();
            sw.style.width = 46; sw.style.height = 46; sw.style.marginRight = 8; sw.style.marginBottom = 8;
            sw.style.backgroundColor = square;
            UiKit.Radius(sw, 8);
            sw.style.borderTopWidth = sw.style.borderBottomWidth = sw.style.borderLeftWidth = sw.style.borderRightWidth = 2;
            var edge = selected ? UiKit.Gold : UiKit.Line;
            sw.style.borderTopColor = sw.style.borderBottomColor = sw.style.borderLeftColor = sw.style.borderRightColor = edge;

            var img = new VisualElement { pickingMode = PickingMode.Ignore };
            img.style.position = Position.Absolute;
            img.style.left = 3; img.style.top = 3; img.style.right = 3; img.style.bottom = 3;
            img.style.backgroundImage = new StyleBackground(tex);
            img.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            sw.Add(img);

            sw.tooltip = name;
            sw.RegisterCallback<ClickEvent>(_ =>
            {
                KaissaSettings.PieceSet = folder;
                RefreshPreview(); RefreshPieceSets();
            });
            UiKit.Interactive(sw, 1.06f);
            _pieceSets.Add(sw);
        }
        var active = PieceArt.Sets.FirstOrDefault(s => s.folder == KaissaSettings.PieceSet).name ?? "Cburnett";
        var lbl = UiKit.Text_(active, 13, UiKit.Text, bold: true);
        lbl.style.width = Length.Percent(100); lbl.style.marginTop = 2;
        _pieceSets.Add(lbl);
    }

    private void RefreshSoundThemes()
    {
        _soundThemes.Clear();
        foreach (var (name, folder) in PieceAudio.Themes)
        {
            bool selected = KaissaSettings.SoundTheme == folder;
            var pill = UiKit.Ghost(name, () =>
            {
                KaissaSettings.SoundTheme = folder;
                RefreshSoundThemes();
                _audio.PlayMove(); // hear the pick immediately
            }, 12);
            pill.style.marginRight = 6; pill.style.marginBottom = 6;
            pill.style.backgroundColor = selected ? UiKit.Green : UiKit.Panel2;
            _soundThemes.Add(pill);
        }
    }

    private static void AddCell(VisualElement parent, Color c)
    {
        var cell = new VisualElement();
        cell.style.width = 21; cell.style.height = 21; cell.style.backgroundColor = c;
        parent.Add(cell);
    }

    private void RefreshGroups()
    {
        _groups.Clear();
        var speeds = new[] { "Fast", "Normal", "Slow" };

        var board = Group("Board & pieces");
        Toggle(board, "Board style", KaissaSettings.BoardView == 1 ? "3D" : "2D",
            () => { KaissaSettings.BoardView = KaissaSettings.BoardView == 1 ? 0 : 1; });
        Toggle(board, "3D pieces", KaissaSettings.UseModels ? "Modeled" : "Simple",
            () => KaissaSettings.UseModels = !KaissaSettings.UseModels);
        Toggle(board, "Coordinates", KaissaSettings.Coordinates ? "On" : "Off",
            () => { KaissaSettings.Coordinates = !KaissaSettings.Coordinates; RefreshPreview(); });
        Toggle(board, "Highlight last move", KaissaSettings.HighlightMove ? "On" : "Off",
            () => KaissaSettings.HighlightMove = !KaissaSettings.HighlightMove);
        Toggle(board, "Piece animation", new[] { "Fast", "Normal", "Slow" }[Mathf.Clamp(KaissaSettings.AnimSpeed, 0, 2)],
            () => KaissaSettings.AnimSpeed = (KaissaSettings.AnimSpeed + 1) % 3);

        var play = Group("Gameplay");
        Toggle(play, "Move by", KaissaSettings.DragToMove ? "Drag or click" : "Click only",
            () => KaissaSettings.DragToMove = !KaissaSettings.DragToMove);
        Toggle(play, "Move hints", KaissaSettings.MoveHints ? "On" : "Off (train recall)",
            () => KaissaSettings.MoveHints = !KaissaSettings.MoveHints);
        Toggle(play, "Auto-queen", KaissaSettings.AutoQueen ? "On" : "Off",
            () => KaissaSettings.AutoQueen = !KaissaSettings.AutoQueen);
        Toggle(play, "Eval bar (Play)", KaissaSettings.EvalBar ? "On" : "Off",
            () => KaissaSettings.EvalBar = !KaissaSettings.EvalBar);
        Toggle(play, "Confirm resign", KaissaSettings.ConfirmResign ? "On" : "Off",
            () => KaissaSettings.ConfirmResign = !KaissaSettings.ConfirmResign);
        Toggle(play, "Low-time warning", KaissaSettings.LowTimeWarning ? "On" : "Off",
            () => KaissaSettings.LowTimeWarning = !KaissaSettings.LowTimeWarning);
        Toggle(play, "Bot speed", speeds[Mathf.Clamp(KaissaSettings.BotSpeed, 0, 2)],
            () => KaissaSettings.BotSpeed = (KaissaSettings.BotSpeed + 1) % 3);

        var disp = Group("Sound & display");
        Toggle(disp, "Sound", KaissaSettings.Sound ? "On" : "Off",
            () => KaissaSettings.Sound = !KaissaSettings.Sound);
        Toggle(disp, "Display", KaissaSettings.Fullscreen ? "Fullscreen" : "Maximized",
            () => { KaissaSettings.Fullscreen = !KaissaSettings.Fullscreen; WindowMode.Apply(); });
        Toggle(disp, "Close to tray", KaissaSettings.CloseToTray ? "On" : "Off",
            () => KaissaSettings.CloseToTray = !KaissaSettings.CloseToTray);

        var data = Group("Data");
        var resetRow = ResetRow();
        data.Add(resetRow);
    }

    private VisualElement Group(string title)
    {
        var card = Panel(); card.style.marginBottom = 14; UiKit.Pad(card, 6, 16, 12, 16);
        var h = UiKit.Text_(title, 12, UiKit.Mute, bold: true); h.style.marginTop = 10; h.style.marginBottom = 4;
        card.Add(h);
        _groups.Add(card);
        return card;
    }

    private void Toggle(VisualElement group, string label, string value, Action onToggle)
    {
        var l = UiKit.Text_(label, 15, UiKit.Text, bold: true); l.style.flexGrow = 1;

        // Colour by state: a plain "On" is green, "Off" is grey, and any other value is a specific
        // choice (2D/3D, Normal/Fast/Slow, ...) shown in a blue accent. Hover lightens the same colour
        // so the state colour is never lost on hover (the old Ghost hover reset it).
        Color baseCol = value == "On" ? UiKit.Green : value.StartsWith("Off") ? UiKit.Panel2 : UiKit.Blue;
        var pill = new Button(() => { onToggle(); RefreshGroups(); }) { text = value };
        UiKit.NoBorder(pill);
        pill.style.color = UiKit.Text; pill.style.fontSize = 13; pill.style.unityFontStyleAndWeight = FontStyle.Bold;
        UiKit.Pad(pill, 8, 14, 8, 14); UiKit.Radius(pill, 8);
        pill.style.marginTop = 0; pill.style.marginBottom = 0; pill.style.marginLeft = 0; pill.style.marginRight = 0;
        pill.style.minWidth = 150; pill.style.unityTextAlign = TextAnchor.MiddleCenter;
        pill.style.backgroundColor = baseCol;
        pill.RegisterCallback<MouseEnterEvent>(_ => pill.style.backgroundColor = UiKit.Lighten(baseCol));
        pill.RegisterCallback<MouseLeaveEvent>(_ => pill.style.backgroundColor = baseCol);
        UiKit.Interactive(pill);

        var row = UiKit.Row(l, pill);
        row.style.justifyContent = Justify.SpaceBetween; UiKit.Pad(row, 7, 4, 7, 4);
        row.style.borderBottomWidth = 1; row.style.borderBottomColor = UiKit.Line;
        group.Add(row);
    }

    private VisualElement ResetRow()
    {
        var l = UiKit.Text_("Reset progress", 15, UiKit.Text, bold: true); l.style.flexGrow = 1;
        var btn = UiKit.Ghost(_confirmReset ? "Tap again to confirm" : "Reset", null, 13);
        btn.style.minWidth = 170;
        btn.style.backgroundColor = _confirmReset ? UiKit.Danger : UiKit.Panel2;
        btn.clicked += () =>
        {
            if (!_confirmReset) { _confirmReset = true; RefreshGroups(); return; }
            KaissaProgress.Clear();
            _confirmReset = false;
            RefreshGroups();
            var done = UiKit.Text_("Progress reset.", 13, UiKit.GreenHi, bold: true);
            done.style.marginTop = 6;
            btn.parent.parent.Add(done);
        };
        var row = UiKit.Row(l, btn);
        row.style.justifyContent = Justify.SpaceBetween; UiKit.Pad(row, 7, 4, 7, 4);
        return row;
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

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneTransition.Go("Menu");
    }

    private IEnumerator AutoDemo()
    {
        string dir = ArgValue("-shotdir") ?? System.IO.Path.Combine(Application.persistentDataPath, "shots");
        System.IO.Directory.CreateDirectory(dir);

        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "settings_warmup.png"));
        yield return new WaitForSeconds(1.0f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "settings_top.png"));
        yield return new WaitForSeconds(0.5f);

        var sw = _swatches.Children().ElementAtOrDefault(3);
        UiAutomation.Click(sw);
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "settings_theme.png"));
        yield return new WaitForSeconds(0.4f);

        var ps = _pieceSets.Children().ElementAtOrDefault(3);
        UiAutomation.Click(ps);
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "settings_pieceset.png"));
        yield return new WaitForSeconds(0.4f);

        UiAutomation.Click(UiAutomation.FindButton(_root, "Piano"));
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "settings_sound.png"));
        yield return new WaitForSeconds(0.4f);

        UiAutomation.Click(UiAutomation.FindButton(_root, "On"));
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "settings_toggled.png"));
        yield return new WaitForSeconds(0.4f);

        UiAutomation.Click(UiAutomation.FindButton(_root, "Reset"));
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "settings_reset_confirm.png"));
        yield return new WaitForSeconds(0.4f);
        UiAutomation.Click(UiAutomation.FindButton(_root, "Tap again to confirm"));
        yield return new WaitForSeconds(0.5f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "settings_reset_done.png"));
        yield return new WaitForSeconds(0.4f);
        Application.Quit();
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
