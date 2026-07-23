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

// Expeditions: short campaigns to master one opening by playing it repeatedly against a bot until you
// win enough games (Lucas Chess's expeditions). Selecting one seeds a Play game from the opening's
// position and flags the result to count toward the expedition; KaissaGameController records the win or
// loss at game end. Progress persists per expedition. The catalogue and completion rule are pure core.
public sealed class ExpeditionsController : MonoBehaviour
{
    private VisualElement _root, _listHost;

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
        _root.Add(UiKit.NavRail("Expeditions"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 20, 24, 20, 24);
        center.Add(UiKit.Text_("Expeditions", 24, UiKit.Text, bold: true));
        var sub = UiKit.Text_("Master an opening by playing it against a bot until you win enough games. Each expedition starts you from its opening and tracks your record.", 13, UiKit.Dim);
        sub.style.whiteSpace = WhiteSpace.Normal; sub.style.maxWidth = 560; sub.style.marginTop = 6; sub.style.marginBottom = 14;
        center.Add(sub);

        var scroll = UiKit.Scroll(); scroll.style.width = 560; scroll.style.flexGrow = 1;
        _listHost = scroll;
        center.Add(scroll);
        _root.Add(center);

        Refresh();

        if (Environment.GetCommandLineArgs().Contains("-kaissa-expeditiontest"))
            StartCoroutine(AutoDemo());
    }

    private void Refresh()
    {
        _listHost.Clear();
        foreach (var e in Expeditions.Catalog)
        {
            var (wins, losses) = KaissaSettings.ExpeditionProgress(e.Id);
            var run = new ExpeditionRun(e, wins, losses);

            var card = new VisualElement();
            card.style.backgroundColor = UiKit.Panel2; UiKit.Radius(card, 12); UiKit.Pad(card, 12, 16, 12, 16);
            card.style.marginBottom = 10;

            var top = UiKit.Row(); top.style.justifyContent = Justify.SpaceBetween; top.style.alignItems = Align.Center;
            var left = UiKit.Col();
            left.Add(UiKit.Text_(e.Title, 16, UiKit.Text, bold: true));
            var d = UiKit.Text_(e.Description, 12, UiKit.Dim); d.style.whiteSpace = WhiteSpace.Normal; d.style.marginTop = 2;
            left.Add(d);
            top.Add(left);

            if (run.IsComplete)
                top.Add(UiKit.Text_("Complete", 13, UiKit.GreenHi, bold: true));
            else
            {
                var play = UiKit.Primary("Play a game", () => StartExpedition(e), 13);
                play.style.width = 130; top.Add(play);
            }
            card.Add(top);

            // Progress line: wins toward target, plus losses for context.
            var prog = UiKit.Text_($"Wins {run.Wins} / {e.TargetWins}" + (run.Losses > 0 ? $"   (losses {run.Losses})" : ""),
                12, run.IsComplete ? UiKit.GreenHi : UiKit.Mute);
            prog.style.marginTop = 8;
            card.Add(prog);

            var bar = new VisualElement();
            bar.style.height = 6; bar.style.marginTop = 4; bar.style.backgroundColor = UiKit.Panel3; UiKit.Radius(bar, 3);
            var fill = new VisualElement();
            fill.style.height = 6; fill.style.width = Length.Percent((float)(run.Progress * 100)); UiKit.Radius(fill, 3);
            fill.style.backgroundColor = run.IsComplete ? UiKit.GreenHi : UiKit.Gold;
            bar.Add(fill);
            card.Add(bar);

            _listHost.Add(card);
        }
    }

    private void StartExpedition(Expedition e)
    {
        var line = OpeningLibrary.ById(e.OpeningId);
        string fen = FenAfter(line?.Moves);
        if (fen == null) return;

        KaissaStreak.RecordToday();
        EndgameRoute.Fen = fen;          // seed the Play game from the opening position
        ExpeditionRoute.Active = true;   // flag the result to count toward this expedition
        ExpeditionRoute.ExpeditionId = e.Id;
        SceneTransition.Go("Play");
    }

    // Replay the opening's book moves and return the resulting FEN, or null if anything is off.
    private static string FenAfter(System.Collections.Generic.IReadOnlyList<string> moves)
    {
        if (moves == null || moves.Count == 0) return ChessGame.StartFen;
        var game = ChessGame.Start();
        foreach (var uci in moves)
            if (!game.TryMakeMove(uci)) return null;
        return game.Fen;
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
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "expeditions.png"));
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
