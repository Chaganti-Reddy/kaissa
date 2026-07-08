using System.Collections;
using System.Linq;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Pattern library ("Learn"), redesigned: a list of motifs on the left; selecting one shows what it
// trains, an example position on the 2D board, and a button to drill it (themed mode via ThemeRoute).
public sealed class LibraryController : MonoBehaviour
{
    private ScenarioLibrary _lib;
    private Board2D _board;
    private Label _name;
    private Label _desc;
    private VisualElement _drillHost;
    private PatternId _selected;

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }

        _lib = ScenarioLibrary.LoadDefault();
        _board = new Board2D(null); // display only

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
        root.Add(UiKit.NavRail("Library"));

        // pattern list
        var listCol = new VisualElement();
        listCol.style.width = 240; UiKit.Pad(listCol, 24, 8, 24, 16);
        listCol.Add(UiKit.Text_("Patterns", 20, UiKit.Text, bold: true));
        var scroll = new ScrollView(); scroll.style.marginTop = 10;
        foreach (var id in _lib.Patterns)
        {
            var pid = id;
            var name = _lib.Describe(pid).Name;
            var item = UiKit.Row(UiKit.Text_(name, 14, UiKit.Dim, bold: true));
            UiKit.Pad(item, 9, 10, 9, 10); UiKit.Radius(item, 6);
            item.RegisterCallback<MouseEnterEvent>(_ => item.style.backgroundColor = UiKit.Panel2);
            item.RegisterCallback<MouseLeaveEvent>(_ => item.style.backgroundColor = new Color(0, 0, 0, 0));
            item.RegisterCallback<ClickEvent>(_ => Select(pid));
            scroll.Add(item);
        }
        listCol.Add(scroll);
        root.Add(listCol);

        // detail
        var detail = new VisualElement();
        detail.style.flexGrow = 1; detail.style.alignItems = Align.Center; UiKit.Pad(detail, 24, 24, 24, 24);
        _name = UiKit.Text_("Select a pattern", 24, UiKit.Text, bold: true);
        detail.Add(_name);
        _desc = UiKit.Text_("", 14, UiKit.Dim);
        _desc.style.marginTop = 6; _desc.style.marginBottom = 14; _desc.style.maxWidth = 460; _desc.style.unityTextAlign = TextAnchor.MiddleCenter;
        detail.Add(_desc);

        var host = new VisualElement();
        host.style.width = 420; host.style.height = 420; host.style.flexShrink = 0;
        host.Add(_board.Root);
        _board.Root.style.width = 420; _board.Root.style.height = 420;
        detail.Add(host);

        _drillHost = new VisualElement(); _drillHost.style.marginTop = 16;
        detail.Add(_drillHost);
        root.Add(detail);

        if (_lib.Patterns.Count > 0) Select(_lib.Patterns[0]);
    }

    private void Select(PatternId id)
    {
        _selected = id;
        var p = _lib.Describe(id);
        _name.text = p.Name;
        _desc.text = p.Description;

        var example = _lib.ForPattern(id).FirstOrDefault();
        if (example != null)
        {
            var view = BoardView.FromFen(example.Fen);
            _board.Render(example.Fen, canMove: false, lastMove: null, whiteBottom: view.WhiteToMove);
        }

        _drillHost.Clear();
        var drill = UiKit.Primary("Drill this pattern", () =>
        {
            ThemeRoute.PatternId = _selected.Value;
            ThemeRoute.PatternName = p.Name;
            SceneManager.LoadScene("SampleScene");
        }, 15);
        drill.style.width = 260;
        _drillHost.Add(drill);
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
