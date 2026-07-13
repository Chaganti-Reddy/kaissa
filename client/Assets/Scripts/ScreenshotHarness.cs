using System;
using System.Collections;
using System.IO;
using System.Linq;
using Kaissa.Chess.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Automated screenshot pass. Launch the player with `-kaissa-shots` (optionally `-shotdir <path>`) and
// it walks every scene, captures a PNG of each, and quits. Used to verify the UI visually without a
// human eyeballing each screen. Does nothing in a normal launch.
public sealed class ScreenshotHarness : MonoBehaviour
{
    private static readonly string[] Scenes =
    {
        "Menu", "Play", "SampleScene", "Rush", "Opening", "Library",
        "Endgame", "Vision", "Coordinate", "Stats", "Settings", "Calibrate", "Analysis",
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        if (!args.Contains("-kaissa-shots") && !args.Contains("-kaissa-interact")
            && !args.Contains("-kaissa-record") && !args.Contains("-kaissa-playtest")
            && !args.Contains("-annotate3d") && !args.Contains("-kaissa-puzzletest")
            && !args.Contains("-kaissa-rushtest") && !args.Contains("-kaissa-openingtest")
            && !args.Contains("-kaissa-endgametest") && !args.Contains("-kaissa-learntest")
            && !args.Contains("-kaissa-analysistest") && !args.Contains("-kaissa-statstest")
            && !args.Contains("-kaissa-visiontest") && !args.Contains("-kaissa-coordtest")
            && !args.Contains("-kaissa-settingstest") && !args.Contains("-kaissa-calibratetest")
            && !args.Contains("-kaissa-transition") && !args.Contains("-kaissa-startup")
            && !args.Contains("-kaissa-hometest"))
            return;
        Application.runInBackground = true; // harness must keep ticking even when the window is unfocused
        var go = new GameObject("ScreenshotHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<ScreenshotHarness>();
    }

    private IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Contains("-board2d")) KaissaSettings.BoardView = 0;
        else if (args.Contains("-board3d")) KaissaSettings.BoardView = 1;
        if (args.Contains("-greenboard")) KaissaSettings.BoardTheme = 1;

        string dir = ArgValue("-shotdir") ?? Path.Combine(Application.persistentDataPath, "shots");
        Directory.CreateDirectory(dir);
        Debug.Log($"ScreenshotHarness: writing to {dir}");

        if (args.Contains("-kaissa-interact"))
        {
            yield return InteractPass(dir);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-record"))
        {
            yield return RecordPass(dir);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-puzzletest"))
        {
            // The Puzzles controller sees the arg, auto-runs its solve demo, captures labeled frames
            // to -shotdir, and quits. We just route to the scene and wait it out.
            SceneManager.LoadScene("SampleScene");
            yield return new WaitForSeconds(30f);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-rushtest"))
        {
            // The Rush controller auto-runs a scripted run and captures labeled frames, then quits.
            SceneManager.LoadScene("Rush");
            yield return new WaitForSeconds(30f);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-openingtest"))
        {
            // The Openings controller loads the book, exercises every mode, self-captures, and quits.
            SceneManager.LoadScene("Opening");
            yield return new WaitForSeconds(45f);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-endgametest"))
        {
            // The Endgames controller plays a drill vs the engine, self-captures, and quits.
            SceneManager.LoadScene("Endgame");
            yield return new WaitForSeconds(45f);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-learntest"))
        {
            // The Learn controller runs a guided lesson to completion, self-captures, and quits.
            SceneManager.LoadScene("Library");
            yield return new WaitForSeconds(35f);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-analysistest"))
        {
            // The Analysis controller exercises eval/lines/arrows/nav/FEN, self-captures, and quits.
            SceneManager.LoadScene("Analysis");
            yield return new WaitForSeconds(45f);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-statstest"))
        {
            // The Stats dashboard self-captures (top + mastery), clicks practice, and quits.
            SceneManager.LoadScene("Stats");
            yield return new WaitForSeconds(20f);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-visiontest"))
        {
            SceneManager.LoadScene("Vision");
            yield return new WaitForSeconds(20f);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-coordtest"))
        {
            SceneManager.LoadScene("Coordinate");
            yield return new WaitForSeconds(20f);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-settingstest"))
        {
            SceneManager.LoadScene("Settings");
            yield return new WaitForSeconds(15f);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-calibratetest"))
        {
            SceneManager.LoadScene("Calibrate");
            yield return new WaitForSeconds(25f);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-startup"))
        {
            // The boot animation plays over the initial Menu scene; burst frames across it.
            for (int k = 0; k < 40; k++)
            {
                yield return new WaitForSeconds(0.05f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"startup_{k:00}.png"));
            }
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-transition"))
        {
            // Load a scene, then burst-capture across the fade-in reveal so the transition is visible.
            SceneManager.LoadScene("Play");
            for (int k = 0; k < 16; k++)
            {
                yield return new WaitForSeconds(0.04f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"trans_{k:00}.png"));
            }
            yield return new WaitForSeconds(0.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "trans_done.png"));
            yield return new WaitForSeconds(0.3f);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-hometest"))
        {
            SceneManager.LoadScene("Menu");
            yield return new WaitForSeconds(12f);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-annotate3d"))
        {
            KaissaSettings.BoardView = 1; // force 3D
            SceneManager.LoadScene("Play");
            yield return new WaitForSeconds(2.0f); // let the 3D board build + settle
            var bi = UnityEngine.Object.FindAnyObjectByType<BoardInteractor>();
            Debug.Log($"ScreenshotHarness: annotate3d interactor={(bi != null)}");
            if (bi != null) bi.DebugAnnotate();
            yield return new WaitForSeconds(0.6f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "annotate3d.png"));
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
            yield break;
        }

        if (args.Contains("-kaissa-playtest"))
        {
            // The Play controller auto-starts a timed game, exercises the controls, self-captures
            // labeled frames to -shotdir, and quits. We just route to the scene and wait it out.
            SceneManager.LoadScene("Play");
            yield return new WaitForSeconds(40f);
            Application.Quit();
            yield break;
        }

        foreach (var scene in Scenes)
        {
            SceneManager.LoadScene(scene);
            yield return new WaitForSeconds(0.8f); // let the controller build its UI Toolkit tree
            string path = Path.Combine(dir, scene + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"ScreenshotHarness: captured {scene}");
            yield return new WaitForSeconds(0.5f); // let the capture flush to disk
        }

        Debug.Log("ScreenshotHarness: done");
        Application.Quit();
    }

    // Drives Board2D through each interaction state and captures a PNG of each, logging any move it
    // reports - so the interactions can be verified without a human.
    private IEnumerator InteractPass(string dir)
    {
        var camGo = new GameObject("cam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = UiKit.Bg;

        var doc = gameObject.AddComponent<UIDocument>();
        doc.panelSettings = Resources.Load<PanelSettings>("KaissaPanel");
        doc.sortingOrder = 100; // draw over whatever scene loaded first
        yield return null;

        var root = doc.rootVisualElement;
        root.style.flexGrow = 1; root.style.alignItems = Align.Center; root.style.justifyContent = Justify.Center;
        root.style.backgroundColor = UiKit.Bg; // opaque, hides the menu behind

        var board = new Board2D(uci => Debug.Log($"HARNESS-MOVE {uci}"));
        board.Root.style.width = 600; board.Root.style.height = 600;
        root.Add(board.Root);

        const string start = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        board.Render(start, true, null, true);
        yield return new WaitForSeconds(0.6f);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, "i-start.png")); yield return new WaitForSeconds(0.4f);

        // Sweep several 2D piece-art sets so each renders on the board can be verified.
        foreach (var folder in new[] { "cburnett", "merida", "chessnut", "fantasy", "pixel", "shapes", "letter", "mono" })
        {
            KaissaSettings.PieceSet = folder;
            board.Render(start, true, null, true);
            yield return new WaitForSeconds(0.35f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"i-set-{folder}.png"));
            yield return new WaitForSeconds(0.35f);
        }
        KaissaSettings.PieceSet = "cburnett";
        board.Render(start, true, null, true);
        yield return new WaitForSeconds(0.4f);

        board.DebugSelect("e2"); // selection + legal-move dots
        yield return new WaitForSeconds(0.3f);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, "i-select.png")); yield return new WaitForSeconds(0.4f);

        board.Render(start, true, null, true);
        board.DebugAnnotate(); // red/green/blue squares + arrows
        yield return new WaitForSeconds(0.3f);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, "i-annotations.png")); yield return new WaitForSeconds(0.4f);

        board.Render("4k3/P7/8/8/8/8/8/4K3 w - - 0 1", true, null, true);
        board.DebugPromotion(true); // promotion picker
        yield return new WaitForSeconds(0.3f);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, "i-promotion.png")); yield return new WaitForSeconds(0.4f);

        board.Render(start, false, null, true); // opponent's turn
        board.DebugPremove("e2", "e4"); // queued premove (orange highlight)
        yield return new WaitForSeconds(0.3f);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, "i-premove.png")); yield return new WaitForSeconds(0.4f);
        Debug.Log("ScreenshotHarness: interact done");
    }

    // Scripts a short game on the 2D board and captures burst frames through each move's glide, so the
    // animation/feel can be reviewed frame-by-frame (a lightweight "recording").
    private IEnumerator RecordPass(string dir)
    {
        var camGo = new GameObject("cam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg;

        var doc = gameObject.AddComponent<UIDocument>();
        doc.panelSettings = Resources.Load<PanelSettings>("KaissaPanel");
        doc.sortingOrder = 100;
        yield return null;

        var root = doc.rootVisualElement;
        root.style.flexGrow = 1; root.style.alignItems = Align.Center; root.style.justifyContent = Justify.Center;
        root.style.backgroundColor = UiKit.Bg;
        var board = new Board2D(_ => { });
        board.Root.style.width = 600; board.Root.style.height = 600;
        root.Add(board.Root);

        var game = ChessGame.Start();
        board.Render(game.Fen, true, null, true);
        yield return new WaitForSeconds(0.6f);

        int frame = 0;
        // Include a capture (f3e5 = Nxe5) so the capture-pop effect is recorded too.
        foreach (var move in new[] { "e2e4", "e7e5", "g1f3", "b8c6", "f1c4", "f8c5", "f3e5" })
        {
            game.TryMakeMove(move);
            board.Render(game.Fen, true, move, true); // triggers the glide (and capture pop) of `move`
            for (int k = 0; k < 6; k++) // frames across the glide + pop
            {
                yield return new WaitForSeconds(0.035f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"rec_{frame:000}.png"));
                frame++;
            }
            yield return new WaitForSeconds(0.25f);
        }

        // Solve/win celebration flourish over the board.
        BoardCelebrate.Burst(board.Root);
        for (int k = 0; k < 12; k++)
        {
            yield return new WaitForSeconds(0.05f);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"celebrate_{k:00}.png"));
        }
        yield return new WaitForSeconds(0.3f);
        Debug.Log("ScreenshotHarness: record done");
    }

    private static string ArgValue(string key)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == key)
                return args[i + 1];
        return null;
    }
}
