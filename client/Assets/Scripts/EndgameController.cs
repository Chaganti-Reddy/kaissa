using System.Collections;
using Kaissa.Training;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Endgame picker, redesigned in UI Toolkit: choose an instructive endgame to play out. The chosen
// FEN is handed to the Play scene via EndgameRoute.
public static class EndgameRoute
{
    public static string Fen;
}

public sealed class EndgameController : MonoBehaviour
{
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
        root.style.flexDirection = FlexDirection.Row;
        root.style.flexGrow = 1;
        root.style.backgroundColor = UiKit.Bg;
        root.Add(UiKit.NavRail("Endgame"));

        var main = new VisualElement();
        main.style.flexGrow = 1; UiKit.Pad(main, 26, 34, 40, 34);
        main.Add(UiKit.Text_("Endgames", 26, UiKit.Text, bold: true));
        var sub = UiKit.Text_("Play out an instructive endgame against the engine.", 14, UiKit.Dim);
        sub.style.marginBottom = 18; sub.style.marginTop = 4;
        main.Add(sub);

        foreach (var eg in EndgameLibrary.All)
        {
            var fen = eg.Fen;
            var card = UiKit.Col(
                UiKit.Text_(eg.Name, 17, UiKit.Text, bold: true),
                Goal(eg.Goal));
            card.style.backgroundColor = UiKit.Panel;
            card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 1;
            card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = UiKit.Line;
            UiKit.Pad(card, 16); UiKit.Radius(card, 12);
            card.style.marginBottom = 12; card.style.maxWidth = 640;
            card.RegisterCallback<ClickEvent>(_ => { EndgameRoute.Fen = fen; SceneManager.LoadScene("Play"); });
            main.Add(card);
        }
        root.Add(main);
    }

    private static Label Goal(string g)
    {
        var l = UiKit.Text_(g, 13, UiKit.Dim);
        l.style.marginTop = 6;
        return l;
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
