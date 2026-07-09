using System;
using System.Collections;
using System.IO;
using System.Linq;
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
        if (!args.Contains("-kaissa-shots") && !args.Contains("-kaissa-interact"))
            return;
        var go = new GameObject("ScreenshotHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<ScreenshotHarness>();
    }

    private IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Contains("-board2d")) KaissaSettings.BoardView = 0;
        else if (args.Contains("-board3d")) KaissaSettings.BoardView = 1;

        string dir = ArgValue("-shotdir") ?? Path.Combine(Application.persistentDataPath, "shots");
        Directory.CreateDirectory(dir);
        Debug.Log($"ScreenshotHarness: writing to {dir}");

        if (args.Contains("-kaissa-interact"))
        {
            yield return InteractPass(dir);
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
    // reports — so the interactions can be verified without a human.
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
        Debug.Log("ScreenshotHarness: interact done");
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
