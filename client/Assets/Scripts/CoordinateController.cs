using System.Collections;
using Kaissa.Training;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Coordinate drill, redesigned: shown a square name, click that square on the (label-free) board.
public sealed class CoordinateController : MonoBehaviour
{
    private readonly CoordinateSession _session = new();
    private Board2D _board;
    private Label _prompt;
    private Label _score;

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }

        _board = new Board2D(null) { ShowCoordinates = false };
        _board.SquareClickHandler = OnSquareClicked;

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
        root.Add(UiKit.NavRail("Coordinate"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 22, 24, 22, 24);
        _prompt = UiKit.Text_("", 26, UiKit.Text, bold: true);
        _prompt.style.marginBottom = 12; center.Add(_prompt);

        var host = new VisualElement();
        host.style.width = 480; host.style.height = 480; host.style.flexShrink = 0;
        host.Add(_board.Root);
        _board.Root.style.width = 480; _board.Root.style.height = 480;
        center.Add(host);

        _score = UiKit.Text_("", 15, UiKit.Mute, bold: true);
        _score.style.marginTop = 12; center.Add(_score);
        root.Add(center);

        _board.Render("8/8/8/8/8/8/8/8 w - - 0 1", canMove: false, lastMove: null, whiteBottom: true);
        Next();
    }

    private void OnSquareClicked(string square)
    {
        var (file, rank) = Coordinates.Parse(square);
        _session.Answer(file, rank);
        Next();
    }

    private void Next()
    {
        _prompt.text = $"Find:  {_session.NextTarget()}";
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
