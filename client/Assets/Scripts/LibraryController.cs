using System.Linq;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pattern library: browse each motif, read what it trains, see an example position, then drill it.
// Drilling hands the pattern to the training scene via ThemeRoute (the same path Practice used).
public sealed class LibraryController : MonoBehaviour
{
    private ScenarioLibrary _lib;
    private Transform _listCanvas;
    private Transform _detailCanvas;
    private Transform _boardRoot;
    private bool _inDetail;

    private void Start()
    {
        Board3D.SetupScene();
        _lib = ScenarioLibrary.LoadDefault();
        ShowList();
    }

    private void ShowList()
    {
        ClearDetail();
        _inDetail = false;
        _listCanvas = Hud.Canvas();
        Hud.Text(_listCanvas, "Pattern library", 44, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 60f));

        var ids = _lib.Patterns;
        const float top = 150f, step = 56f;
        int half = (ids.Count + 1) / 2;
        for (int i = 0; i < ids.Count; i++)
        {
            var id = ids[i];
            string name = _lib.Describe(id).Name;
            float x = i < half ? -240f : 240f;
            float y = top - (i % half) * step;
            Hud.Button(_listCanvas, name, new Vector2(x, y), () => ShowDetail(id), 440f);
        }

        Hud.Text(_listCanvas, "Esc — menu", 18, TextAnchor.LowerCenter,
            new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(400f, 30f));
    }

    private void ShowDetail(PatternId id)
    {
        if (_listCanvas != null) { Destroy(_listCanvas.gameObject); _listCanvas = null; }
        _inDetail = true;

        var pattern = _lib.Describe(id);
        var example = _lib.ForPattern(id).FirstOrDefault();
        if (example != null)
        {
            var view = BoardView.FromFen(example.Fen);
            _boardRoot = Board3D.Render(view);
            Board3D.OrientCamera(view.WhiteToMove);
        }

        _detailCanvas = Hud.Canvas();
        Hud.Text(_detailCanvas, pattern.Name, 40, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1000f, 56f));
        var desc = Hud.Text(_detailCanvas, pattern.Description, 24, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(760f, 120f));
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;

        if (example != null && !string.IsNullOrEmpty(example.Prompt))
            Hud.Text(_detailCanvas, example.Prompt, 20, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(760f, 40f));

        Hud.Button(_detailCanvas, "Drill this pattern", new Vector2(-130f, 70f), () =>
        {
            ThemeRoute.PatternId = id.Value;
            ThemeRoute.PatternName = pattern.Name;
            SceneManager.LoadScene("SampleScene");
        }, 260f);
        Hud.Button(_detailCanvas, "Back", new Vector2(130f, 70f), ShowList, 200f);
    }

    private void ClearDetail()
    {
        if (_detailCanvas != null) { Destroy(_detailCanvas.gameObject); _detailCanvas = null; }
        if (_boardRoot != null) { Destroy(_boardRoot.gameObject); _boardRoot = null; }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_inDetail) ShowList();
            else SceneManager.LoadScene("Menu");
        }
    }
}
