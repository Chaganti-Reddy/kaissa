using System.Collections;
using Kaissa.Training;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Board-vision drill, redesigned: shown a square name, say whether it is a light or dark square.
public sealed class VisionController : MonoBehaviour
{
    private readonly VisionSession _session = new();
    private Label _prompt;
    private Label _score;

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
        var root = doc.rootVisualElement;
        root.Clear();
        root.style.flexDirection = FlexDirection.Row; root.style.flexGrow = 1; root.style.backgroundColor = UiKit.Bg;
        root.Add(UiKit.NavRail("Vision"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; center.style.justifyContent = Justify.Center;
        center.Add(UiKit.Text_("Light or dark square?", 22, UiKit.Dim, bold: true));
        _prompt = UiKit.Text_("", 72, UiKit.Text, bold: true);
        _prompt.style.marginTop = 10; _prompt.style.marginBottom = 24;
        center.Add(_prompt);

        var row = UiKit.Row();
        var light = UiKit.Primary("Light", () => Answer(true), 18); light.style.width = 160; light.style.marginRight = 8;
        var dark = UiKit.Ghost("Dark", () => Answer(false), 18); dark.style.width = 160;
        row.Add(light); row.Add(dark);
        center.Add(row);

        _score = UiKit.Text_("", 15, UiKit.Mute, bold: true);
        _score.style.marginTop = 22;
        center.Add(_score);
        root.Add(center);

        Next();
    }

    private void Answer(bool light) { _session.Answer(light); Next(); }

    private void Next()
    {
        _prompt.text = _session.NextSquare();
        _score.text = $"Score {_session.Score}/{_session.Asked}";
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneManager.LoadScene("Menu");
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
