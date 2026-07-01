using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The home screen: a title and buttons to enter Training or Play. Built in code (Canvas +
// EventSystem) so no scene wiring is needed beyond adding the scenes to Build Settings.
public sealed class MainMenuController : MonoBehaviour
{
    private Font _font;

    private void Start()
    {
        _font = Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf") as Font;
        EnsureEventSystem();

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
        }

        var canvas = BuildCanvas();
        MakeText(canvas, "Kaissa", 72, new Vector2(0f, 210f), new Vector2(800f, 100f));
        MakeText(canvas, "Train. Play. Improve.", 26, new Vector2(0f, 150f), new Vector2(800f, 50f));
        MakeButton(canvas, "Train", new Vector2(0f, 70f), () => SceneManager.LoadScene("SampleScene"));
        MakeButton(canvas, "Puzzle Rush", new Vector2(0f, -10f), () => SceneManager.LoadScene("Rush"));
        MakeButton(canvas, "Play vs Bot", new Vector2(0f, -90f), () => SceneManager.LoadScene("Play"));
        MakeButton(canvas, "Progress", new Vector2(0f, -170f), () => SceneManager.LoadScene("Stats"));
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

    private void MakeButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = new Color(0.16f, 0.17f, 0.22f);

        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(300f, 64f);

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
