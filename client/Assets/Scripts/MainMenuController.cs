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
        MakeText(canvas, "Kaissa", 72, new Vector2(0f, 240f), new Vector2(800f, 100f));
        MakeText(canvas, "Train. Play. Improve.", 26, new Vector2(0f, 180f), new Vector2(800f, 50f));

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

        // First-run calibration + settings
        MakeButton(canvas, "Calibrate my level", new Vector2(-100f, -200f), () => SceneManager.LoadScene("Calibrate"), 200f);
        MakeButton(canvas, "Settings", new Vector2(115f, -200f), () => SceneManager.LoadScene("Settings"), 200f);

        MakeButton(canvas, "Quit", new Vector2(0f, -270f), Quit, 200f);
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
