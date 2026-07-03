using System;
using UnityEngine;
using UnityEngine.UI;

// A small overlay that asks which piece a promoting pawn becomes (Q/R/B/N). Built in code so no scene
// wiring is needed. Call PromotionPicker.Ask(white, choice => ...); the callback gets the lowercase
// promotion letter ('q','r','b','n'). Honors the auto-queen setting (calls back immediately).
public sealed class PromotionPicker : MonoBehaviour
{
    private Action<char> _onPick;

    public static void Ask(bool white, Action<char> onPick)
    {
        if (KaissaSettings.AutoQueen)
        {
            onPick('q');
            return;
        }

        var go = new GameObject("PromotionPicker");
        var picker = go.AddComponent<PromotionPicker>();
        picker._onPick = onPick;
        picker.Build(white);
    }

    private void Build(bool white)
    {
        var canvasObj = new GameObject("Canvas");
        canvasObj.transform.SetParent(transform, false);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // above the game HUD
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Dim backdrop.
        var dim = NewImage(canvasObj.transform, new Color(0f, 0f, 0f, 0.55f));
        Stretch(dim.rectTransform);

        // Centered panel.
        var panel = NewImage(canvasObj.transform, new Color(0.12f, 0.13f, 0.17f, 0.98f));
        panel.rectTransform.sizeDelta = new Vector2(520f, 190f);
        panel.rectTransform.anchoredPosition = Vector2.zero;

        var font = Hud.Font;
        var label = new GameObject("label").AddComponent<Text>();
        label.transform.SetParent(panel.transform, false);
        label.font = font;
        label.text = "Promote to";
        label.fontSize = 26;
        label.alignment = TextAnchor.UpperCenter;
        label.color = Color.white;
        label.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        label.rectTransform.pivot = new Vector2(0.5f, 1f);
        label.rectTransform.anchoredPosition = new Vector2(0f, -14f);
        label.rectTransform.sizeDelta = new Vector2(480f, 36f);

        var options = new (char letter, string glyph)[] { ('q', "Q"), ('r', "R"), ('b', "B"), ('n', "N") };
        for (int i = 0; i < options.Length; i++)
        {
            var (letter, glyph) = options[i];
            var button = NewImage(panel.transform, new Color(0.20f, 0.22f, 0.28f, 1f));
            button.rectTransform.sizeDelta = new Vector2(100f, 100f);
            button.rectTransform.anchoredPosition = new Vector2(-165f + i * 110f, -20f);

            var btn = button.gameObject.AddComponent<Button>();
            char captured = letter;
            btn.onClick.AddListener(() => Pick(captured));

            var text = new GameObject("t").AddComponent<Text>();
            text.transform.SetParent(button.transform, false);
            text.font = font;
            text.text = glyph;
            text.fontSize = 54;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = white ? new Color(0.95f, 0.94f, 0.9f) : new Color(0.75f, 0.78f, 0.85f);
            Stretch(text.rectTransform);
        }
    }

    private void Pick(char letter)
    {
        var cb = _onPick;
        _onPick = null;
        Destroy(gameObject);
        cb?.Invoke(letter);
    }

    private static Image NewImage(Transform parent, Color color)
    {
        var go = new GameObject("img");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
