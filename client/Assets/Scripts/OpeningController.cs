using System;
using System.Collections;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Opening repertoire trainer, redesigned in UI Toolkit + the 2D board. Recall your own moves from
// your repertoire, spaced-repetition scheduled; a wrong recall shows the book move and resurfaces it.
public sealed class OpeningController : MonoBehaviour
{
    private OpeningProgress _progress;
    private RepertoireSession _session;
    private RepertoireCard _card;
    private IBoardView _board;
    private VisualElement _boardHost;
    private PieceAudio _audio;
    private bool _busy;
    private float _shownTime;
    private bool _whiteBottom = true;

    private Label _prompt;
    private Label _feedback;

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }

        _audio = PieceAudio.Attach(gameObject);
        _progress = KaissaOpenings.Load();
        _session = new RepertoireSession(OpeningRepertoire.Default, _progress, new SystemClock());

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
        root.Add(UiKit.NavRail("Opening"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 22, 24, 22, 24);
        center.Add(UiKit.Text_("Openings", 24, UiKit.Text, bold: true));
        _prompt = UiKit.Text_("", 15, UiKit.Dim); _prompt.style.marginBottom = 12; _prompt.style.marginTop = 4;
        center.Add(_prompt);

        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        center.Add(_boardHost);

        _feedback = UiKit.Text_("", 16, UiKit.Dim, bold: true);
        _feedback.style.marginTop = 12; _feedback.style.unityTextAlign = TextAnchor.MiddleCenter;
        center.Add(_feedback);
        root.Add(center);

        _board = BoardMount.Create(gameObject, _boardHost, root, uci => OnPlayerMove(uci), _audio);
        NextCard();
    }

    private void NextCard()
    {
        _card = _session.Next();
        if (_card == null) { _prompt.text = "No repertoire lines."; return; }
        _shownTime = Time.time;
        _whiteBottom = _card.WhiteToMove;
        _board.Render(_card.Fen, canMove: true, lastMove: null, whiteBottom: _whiteBottom);
        _prompt.text = $"{_card.LineName} — your move   ·   {_session.DueCount} due";
    }

    private void OnPlayerMove(string uci)
    {
        if (_busy || _card == null) return;
        _busy = true;
        var result = _session.Submit(uci, TimeSpan.FromSeconds(Time.time - _shownTime));
        KaissaOpenings.Save(_progress);

        var afterFen = ApplyMove(_card.Fen, uci);
        if (afterFen != null) _board.Render(afterFen, false, uci, _whiteBottom);

        _feedback.style.color = result.Correct ? UiKit.GreenHi : UiKit.Danger;
        _feedback.text = result.Correct ? $"{_card.LineName} — correct" : $"Book move was {result.ExpectedMove}";
        if (result.Correct) _audio.PlayCorrect(); else _audio.PlayWrong();
        StartCoroutine(NextAfter(result.Correct ? 0.8f : 1.7f));
    }

    private IEnumerator NextAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _feedback.text = "";
        _busy = false;
        NextCard();
    }

    private static string ApplyMove(string fen, string uci)
    {
        try { var g = ChessGame.FromFen(fen); if (g.TryMakeMove(uci)) return g.Fen; }
        catch { }
        return null;
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
