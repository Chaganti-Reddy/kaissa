using System;
using System.Collections;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// First-run calibration, redesigned in UI Toolkit + the 2D board: a short adaptive run that estimates
// a starting rating and seeds it into saved progress.
public sealed class CalibrateController : MonoBehaviour
{
    private CalibrationSession _session;
    private IBoardView _board;
    private VisualElement _boardHost;
    private PieceAudio _audio;
    private bool _done;
    private BoardView _current;

    private Label _prompt;

    private void Start()
    {
        EnsureEventSystem();
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UiKit.Bg; }

        _session = new CalibrationSession(ScenarioLibrary.LoadDefault(), puzzles: 12);
        _audio = PieceAudio.Attach(gameObject);

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
        root.Add(UiKit.NavRail("Calibrate"));

        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.alignItems = Align.Center; UiKit.Pad(center, 22, 24, 22, 24);
        center.Add(UiKit.Text_("Find your level", 24, UiKit.Text, bold: true));
        _prompt = UiKit.Text_("", 15, UiKit.Dim); _prompt.style.marginBottom = 12; _prompt.style.marginTop = 4;
        center.Add(_prompt);

        _boardHost = new VisualElement();
        _boardHost.style.width = 480; _boardHost.style.height = 480; _boardHost.style.flexShrink = 0;
        center.Add(_boardHost);
        root.Add(center);

        _board = BoardMount.Create(gameObject, _boardHost, root, uci => OnPlayerMove(uci), _audio);
        DealNext();
    }

    private void OnPlayerMove(string uci)
    {
        if (_done || _current == null) return;
        _session.Submit(uci, TimeSpan.FromSeconds(4));
        _audio.PlayMove();
        DealNext();
    }

    private void DealNext()
    {
        if (_session.IsComplete) { Finish(); return; }
        var scenario = _session.Next();
        if (scenario == null) { Finish(); return; }

        _current = BoardView.FromFen(scenario.Fen);
        bool whiteBottom = !KaissaSettings.Flip || _current.WhiteToMove;
        _board.Render(scenario.Fen, canMove: true, lastMove: null, whiteBottom: whiteBottom);
        string side = _current.WhiteToMove ? "White" : "Black";
        _prompt.text = $"Puzzle {_session.Answered + 1}/{_session.Total}   -   {side} to move   -   est {_session.EstimatedRating:0}";
    }

    private void Finish()
    {
        _done = true;
        int rating = (int)Math.Round(_session.EstimatedRating);
        var saved = KaissaProgress.Load();
        var model = saved != null ? SkillModel.FromJson(saved) : new SkillModel();
        model.RatingEstimate = rating;
        KaissaProgress.Save(model.ToJson());
        _prompt.text = $"Your starting rating: {rating}.   Esc - menu";
        _board.Render("8/8/8/8/8/8/8/8 w - - 0 1", canMove: false, lastMove: null, whiteBottom: true);
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
