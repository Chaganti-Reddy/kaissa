using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The home screen: a title and buttons to enter Training or Play. Built in code (Canvas +
// EventSystem) so no scene wiring is needed beyond adding the scenes to Build Settings.
public sealed class MainMenuController : MonoBehaviour
{
    private Font _font;
    private static bool _startedWindowed;

    private void Start()
    {
        // Launch windowed so a first-time player isn't trapped in fullscreen with no obvious exit.
        // Only forced once per run, so returning to the menu doesn't fight a manual resize.
        if (!_startedWindowed)
        {
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            _startedWindowed = true;
        }

        _font = Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf") as Font;
        EnsureEventSystem();

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
        }

        var canvas = BuildCanvas();
        MakeText(canvas, "Kaissa", 72, new Vector2(0f, 255f), new Vector2(800f, 100f));
        MakeText(canvas, SubtitleText(), 26, new Vector2(0f, 198f), new Vector2(900f, 50f));

        bool dailyDone = KaissaSettings.DailyDone == System.DateTime.Now.ToString("yyyy-MM-dd");
        MakeButton(canvas, dailyDone ? "Daily Puzzle  (done)" : "Daily Puzzle", new Vector2(0f, 150f),
            () => { DailyRoute.Active = true; SceneManager.LoadScene("SampleScene"); }, 260f);

        // Left column
        MakeButton(canvas, "Train", new Vector2(-170f, 90f), () => SceneManager.LoadScene("SampleScene"));
        MakeButton(canvas, "Puzzle Rush", new Vector2(-170f, 20f), () => SceneManager.LoadScene("Rush"));
        MakeButton(canvas, "Play vs Bot", new Vector2(-170f, -50f), () => SceneManager.LoadScene("Play"));
        MakeButton(canvas, "Progress", new Vector2(-170f, -120f), () => SceneManager.LoadScene("Stats"));

        // Right column
        MakeButton(canvas, "Endgames", new Vector2(170f, 90f), () => SceneManager.LoadScene("Endgame"));
        MakeButton(canvas, "Openings", new Vector2(170f, 20f), () => SceneManager.LoadScene("Opening"));
        MakeButton(canvas, "Board Vision", new Vector2(170f, -50f), () => SceneManager.LoadScene("Vision"));
        MakeButton(canvas, "Coordinates", new Vector2(170f, -120f), () => SceneManager.LoadScene("Coordinate"));

        // First-run calibration, custom practice, settings
        MakeButton(canvas, "Calibrate", new Vector2(-185f, -200f), () => SceneManager.LoadScene("Calibrate"), 170f);
        MakeButton(canvas, "Practice", new Vector2(0f, -200f), () => SceneManager.LoadScene("Custom"), 170f);
        MakeButton(canvas, "Settings", new Vector2(185f, -200f), () => SceneManager.LoadScene("Settings"), 170f);

        MakeButton(canvas, "Quit", new Vector2(0f, -270f), Quit, 200f);

        if (!KaissaSettings.Onboarded)
            BuildWelcome(canvas);
    }

    // First-run welcome: steer a new player to calibration so puzzles and the bot match their level.
    private void BuildWelcome(Transform canvas)
    {
        var dim = new GameObject("welcome").AddComponent<Image>();
        dim.transform.SetParent(canvas, false);
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        var drt = dim.rectTransform;
        drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
        drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;

        var panel = new GameObject("panel").AddComponent<Image>();
        panel.transform.SetParent(dim.transform, false);
        panel.color = new Color(0.12f, 0.13f, 0.17f, 1f);
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(660f, 340f);
        prt.anchoredPosition = Vector2.zero;

        MakeText(panel.transform, "Welcome to Kaissa", 40, new Vector2(0f, 120f), new Vector2(620f, 60f));
        MakeText(panel.transform, "Let's find your level so the puzzles and the\nbot match you. It takes about a minute.",
            22, new Vector2(0f, 45f), new Vector2(620f, 80f));
        MakeButton(panel.transform, "Find my level", new Vector2(0f, -45f),
            () => { KaissaSettings.Onboarded = true; SceneManager.LoadScene("Calibrate"); }, 320f);
        MakeButton(panel.transform, "Skip for now", new Vector2(0f, -120f),
            () => { KaissaSettings.Onboarded = true; Destroy(dim.gameObject); }, 320f);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Quit();
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Returning players see their streak and how many patterns are due; new players see the tagline.
    private static string SubtitleText()
    {
        try
        {
            var trainer = KaissaTrainer.CreateDefault(KaissaProgress.Load());
            var stats = trainer.GetStats();
            if (stats.TotalAttempts > 0)
            {
                int days = KaissaStreak.CurrentDays();
                int due = trainer.DueCount();
                return days > 0 ? $"Streak {days}d   ·   {due} due for review" : $"{due} due for review";
            }
        }
        catch { /* fall back to the tagline */ }
        return "Train. Play. Improve.";
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
            return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>(); // project uses the new Input System
    }

    private static Transform BuildCanvas()
    {
        var canvasObj = new GameObject("Menu");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        return canvas.transform;
    }

    private void MakeText(Transform parent, string content, int size, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject("text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = _font;
        text.text = content;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
    }

    private void MakeButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick, float width = 300f)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = new Color(0.16f, 0.17f, 0.22f);

        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 64f);

        var button = go.AddComponent<Button>();
        button.onClick.AddListener(onClick);

        var textObj = new GameObject("text");
        textObj.transform.SetParent(go.transform, false);
        var text = textObj.AddComponent<Text>();
        text.font = _font;
        text.text = label;
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }
}
