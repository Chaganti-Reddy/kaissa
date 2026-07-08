using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Small helpers for building code-driven UI (canvas, text, buttons) so each screen stays short.
public static class Hud
{
    public static Font Font =>
        Resources.Load<Font>("Inter-Regular")
        ?? Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf") as Font
        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

    public static Transform Canvas()
    {
        EnsureEventSystem();
        var obj = new GameObject("HUD");
        var canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        ConfigureScaler(obj.AddComponent<CanvasScaler>());
        obj.AddComponent<GraphicRaycaster>();
        return canvas.transform;
    }

    // Every screen's layout is authored in 1280x720 coordinates. Scaling with screen size (rather
    // than the default constant-pixel mode) keeps those layouts proportional at any window size —
    // maximized, fullscreen, or resized — instead of clustering in a corner. match 0.5 balances
    // width and height so nothing clips on off-16:9 aspect ratios.
    public static void ConfigureScaler(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    public static Text Text(Transform parent, string content, int size, TextAnchor anchor,
        Vector2 anchorMinMax, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject("text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = Font;
        text.text = content;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchorMinMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        return text;
    }

    public static void Button(Transform parent, string label, Vector2 pos, UnityAction onClick, float width = 300f)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = new Color(0.16f, 0.17f, 0.22f);

        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 56f);

        go.AddComponent<Button>().onClick.AddListener(onClick);

        var textObj = new GameObject("text");
        textObj.transform.SetParent(go.transform, false);
        var text = textObj.AddComponent<Text>();
        text.font = Font;
        text.text = label;
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
            return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
