using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Press F1 anywhere to toggle a controls cheat-sheet. Bootstrapped once and kept across scenes, so
// the many keyboard shortcuts are discoverable without cluttering each screen.
public sealed class HelpOverlay : MonoBehaviour
{
    private GameObject _panel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("HelpOverlay");
        DontDestroyOnLoad(go);
        go.AddComponent<HelpOverlay>();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            Toggle();
    }

    private void Toggle()
    {
        if (_panel != null)
        {
            Destroy(_panel);
            _panel = null;
            return;
        }

        var canvas = Hud.Canvas();
        _panel = canvas.gameObject;

        var dim = new GameObject("dim").AddComponent<Image>();
        dim.transform.SetParent(canvas, false);
        dim.color = new Color(0f, 0f, 0f, 0.82f);
        var r = dim.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;

        string text =
            "Controls\n\n" +
            "Move:   click a piece then its target, or drag it\n" +
            "F:      flip the board view\n" +
            "F1:     toggle this help\n" +
            "Esc:    back to menu\n\n" +
            "Play vs bot:\n" +
            "   N  new game      R  resign      U  take back\n" +
            "   After the game:  Left / Right  step through it\n\n" +
            "Training / Puzzle Rush:\n" +
            "   H  hint  (counts as assisted)\n\n" +
            "Tip: turn Move hints off in Settings to train recall.";
        Hud.Text(canvas, text, 22, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(920f, 620f));
        Hud.Button(canvas, "Close (F1)", new Vector2(0f, -300f),
            () => { Destroy(_panel); _panel = null; }, 260f);
    }
}
