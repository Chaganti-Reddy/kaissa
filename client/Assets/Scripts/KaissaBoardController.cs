using System;
using System.Collections;
using System.Collections.Generic;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// The training screen (MVP + first polish): renders the current card as a 3D board with lighting
// and post-processing, shows a HUD, plays generated sound cues, and lets the player make a move by
// clicking a piece then a destination. Graded by the core; feedback flashes (green/red) and the
// next card is dealt. UI/lighting/audio are built in code so no scene wiring is needed. Modelled
// art replaces the primitives later.
public sealed class KaissaBoardController : MonoBehaviour
{
    private KaissaTrainer _trainer;
    private Transform _boardRoot;
    private BoardView _board;
    private float _cardShownTime;
    private bool _busy;

    private int _answered, _correct;
    private double _ratingStart;
    private bool _summaryShown;
    private Transform _summaryCanvas;

    private bool _dailyMode;
    private Scenario _dailyScenario;
    private bool _hintUsed;

    private BoardInteractor _interactor;
    private PieceAudio _audio;

    private Text _titleText;
    private Text _ratingText;
    private Text _feedbackText;
    private Font _font;

    private static readonly Color CorrectColor = new(0.30f, 0.85f, 0.45f);
    private static readonly Color WrongColor = new(0.92f, 0.33f, 0.33f);

    private void Start()
    {
        _font = Hud.Font;
        _trainer = KaissaTrainer.CreateDefault(KaissaProgress.Load());
        _ratingStart = _trainer.PlayerRating;
        SetUpCameraAndLight();
        BuildPostProcessing();
        BuildHud();
        _audio = PieceAudio.Attach(gameObject);
        _interactor = gameObject.AddComponent<BoardInteractor>();
        _interactor.Init(uci => OnPlayerMove(uci), _audio);

        if (DailyRoute.Active)
        {
            DailyRoute.Active = false;
            StartDaily();
        }
        else
        {
            DealNext();
        }
    }

    private void StartDaily()
    {
        _dailyMode = true;
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        _dailyScenario = DailyPuzzle.ForDate(ScenarioLibrary.LoadDefault(), DateTime.Today);
        _board = BoardView.FromFen(_dailyScenario.Fen);
        RenderBoard(_board);
        Board3D.OrientCamera(!KaissaSettings.Flip || _board.WhiteToMove);

        _titleText.text = $"Daily puzzle — {today}\n{_dailyScenario.Prompt}";
        _ratingText.text = $"Puzzle {_dailyScenario.Rating}";

        bool alreadyDone = KaissaSettings.DailyDone == today;
        if (alreadyDone)
        {
            _feedbackText.color = CorrectColor;
            _feedbackText.text = "Already solved today. Come back tomorrow.   Esc — menu";
            _interactor.OnBoardRendered(_boardRoot, _board, null, humanCanMove: false);
        }
        else
        {
            _cardShownTime = Time.time;
            _interactor.OnBoardRendered(_boardRoot, _board, null, humanCanMove: true);
        }
    }

    private void OnDailyMove(string uci)
    {
        _interactor.SetInputEnabled(false);

        bool correct = false;
        foreach (var s in _dailyScenario.Solutions)
            if (string.Equals(s, uci, StringComparison.OrdinalIgnoreCase)) { correct = true; break; }

        var afterFen = ApplyMove(_dailyScenario.Fen, uci);
        if (afterFen != null)
            RenderBoard(BoardView.FromFen(afterFen));

        var color = correct ? CorrectColor : WrongColor;
        HighlightSolution(_dailyScenario.Solutions.Count > 0 ? _dailyScenario.Solutions[0] : null, color);
        _feedbackText.color = color;
        _feedbackText.text = correct
            ? "Correct! Daily solved. Come back tomorrow.   Esc — menu"
            : $"Missed — best was {string.Join(", ", _dailyScenario.Solutions)}.   Esc — menu";

        if (correct)
            KaissaSettings.DailyDone = DateTime.Now.ToString("yyyy-MM-dd");
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_answered > 0 && !_summaryShown) ShowSummary();
            else UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
        }
        else if (Keyboard.current.hKey.wasPressedThisFrame && _trainer != null && !_busy
                 && !_summaryShown && _boardRoot != null)
        {
            var sq = _trainer.Hint();
            if (sq != null) { BoardFx.HintSquare(_boardRoot, sq); _hintUsed = true; }
        }
    }

    private void ShowSummary()
    {
        _summaryShown = true;
        _interactor.SetInputEnabled(false);
        _summaryCanvas = Hud.Canvas();

        var dim = new GameObject("dim").AddComponent<Image>();
        dim.transform.SetParent(_summaryCanvas, false);
        dim.color = new Color(0f, 0f, 0f, 0.78f);
        var r = dim.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;

        int pct = _answered > 0 ? Mathf.RoundToInt(100f * _correct / _answered) : 0;
        double now = _trainer.PlayerRating;
        int delta = Mathf.RoundToInt((float)(now - _ratingStart));
        var center = new Vector2(0.5f, 0.5f);
        Hud.Text(_summaryCanvas, "Session summary", 40, TextAnchor.MiddleCenter, center, new Vector2(0f, 150f), new Vector2(700f, 60f));
        Hud.Text(_summaryCanvas, $"Solved {_correct}/{_answered}   ({pct}%)", 28, TextAnchor.MiddleCenter, center, new Vector2(0f, 85f), new Vector2(700f, 40f));
        Hud.Text(_summaryCanvas, $"Rating {_ratingStart:0} → {now:0}   ({delta:+0;-0})", 28, TextAnchor.MiddleCenter, center, new Vector2(0f, 40f), new Vector2(700f, 40f));
        Hud.Button(_summaryCanvas, "Keep training", new Vector2(0f, -40f),
            () => { Destroy(_summaryCanvas.gameObject); _summaryCanvas = null; _summaryShown = false; _interactor.SetInputEnabled(true); }, 320f);
        Hud.Button(_summaryCanvas, "Back to menu", new Vector2(0f, -110f),
            () => UnityEngine.SceneManagement.SceneManager.LoadScene("Menu"), 320f);
    }

    // Called by the BoardInteractor only for legal moves (illegal ones are rejected before this),
    // so the trainer never grades an impossible move. Shows the played move, then grades and advances.
    private void OnPlayerMove(string uci)
    {
        if (_dailyMode)
        {
            OnDailyMove(uci);
            return;
        }
        if (_busy || _board == null)
            return;
        _busy = true;
        _interactor.SetInputEnabled(false);

        var thinkTime = TimeSpan.FromSeconds(Time.time - _cardShownTime);
        bool hinted = _hintUsed;
        var result = _trainer.Answer(uci, thinkTime, hinted); // a hinted answer counts as a lapse
        KaissaProgress.Save(_trainer.ExportProgress());
        _answered++;
        if (result.Correct) _correct++;

        // Render the position after the move so click and drag both show the move being made.
        var afterFen = ApplyMove(_board.Fen, uci);
        if (afterFen != null)
            RenderBoard(BoardView.FromFen(afterFen));

        var color = result.Correct ? CorrectColor : WrongColor;
        HighlightSolution(result.Solutions.Count > 0 ? result.Solutions[0] : null, color);

        _feedbackText.color = color;
        if (hinted)
            _feedbackText.text = $"With a hint — best was {string.Join(", ", result.Solutions)}. It'll come back soon.";
        else
            _feedbackText.text = result.Correct
                ? $"Correct!  ({result.Grade})"
                : $"Missed — best was {string.Join(", ", result.Solutions)}";
        _ratingText.text = $"Rating {result.PlayerRating:0}  ({result.PlayerRatingChange:+0;-0})";

        if (result.Correct) _audio.PlayCorrect();
        else _audio.PlayWrong();

        StartCoroutine(NextAfterDelay(result.Correct ? 0.9f : 1.6f));
    }

    private static string ApplyMove(string fen, string uci)
    {
        try
        {
            var game = ChessGame.FromFen(fen);
            if (game.TryMakeMove(uci))
                return game.Fen;
        }
        catch { /* fall through */ }
        return null;
    }

    private IEnumerator NextAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _feedbackText.text = string.Empty;
        _busy = false;
        DealNext();
    }

    private void DealNext()
    {
        var card = _trainer.NextCard();
        if (card == null)
        {
            _titleText.text = "No more cards.";
            _interactor.SetInputEnabled(false);
            return;
        }

        _board = card.Board;
        _hintUsed = false;
        _cardShownTime = Time.time;
        _titleText.text = $"{card.PatternName}\n{card.Prompt}";
        _ratingText.text = $"Rating {card.PlayerRating:0}";
        RenderBoard(_board);
        Board3D.OrientCamera(!KaissaSettings.Flip || _board.WhiteToMove);
        _interactor.OnBoardRendered(_boardRoot, _board, lastMoveUci: null, humanCanMove: true);
    }

    private void HighlightSolution(string uciSolution, Color color)
    {
        if (string.IsNullOrEmpty(uciSolution) || uciSolution.Length < 4 || _boardRoot == null)
            return;

        foreach (var sq in new[] { uciSolution.Substring(0, 2), uciSolution.Substring(2, 2) })
        {
            var tile = _boardRoot.Find($"sq_{sq}");
            if (tile != null)
                Paint(tile.gameObject, color);
        }
    }

    private void RenderBoard(BoardView board)
    {
        if (_boardRoot != null)
            Destroy(_boardRoot.gameObject);
        _boardRoot = new GameObject("Board").transform;

        // Framed base slab under the board.
        var basePlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        basePlate.name = "base";
        basePlate.transform.SetParent(_boardRoot);
        basePlate.transform.localScale = new Vector3(9.4f, 0.3f, 9.4f);
        basePlate.transform.position = new Vector3(3.5f, -0.2f, 3.5f);
        Destroy(basePlate.GetComponent<Collider>()); // don't intercept board clicks
        Paint(basePlate, new Color(0.08f, 0.08f, 0.10f));

        var theme = Board3D.Themes[Mathf.Clamp(KaissaSettings.BoardTheme, 0, Board3D.Themes.Length - 1)];
        for (int file = 0; file < 8; file++)
        for (int rank = 0; rank < 8; rank++)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"sq_{(char)('a' + file)}{rank + 1}";
            tile.transform.SetParent(_boardRoot);
            tile.transform.localScale = new Vector3(1f, 0.15f, 1f);
            tile.transform.position = new Vector3(file, 0f, rank);
            Paint(tile, (file + rank) % 2 == 0 ? theme.Dark : theme.Light);
        }

        foreach (var square in board.Pieces)
        {
            int file = square.Square[0] - 'a';
            int rank = square.Square[1] - '1';
            bool white = char.IsUpper(square.Piece);

            var piece = Pieces.Create(square.Piece, white);
            piece.name = $"pc_{square.Piece}_{square.Square}";
            piece.transform.SetParent(_boardRoot);
            piece.transform.position = new Vector3(file, 0.075f, rank); // seat base on the tile surface
        }
    }

    private void BuildHud()
    {
        var canvasObj = new GameObject("HUD");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        _titleText = MakeText(canvas.transform, 26, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(1100f, 90f));
        _ratingText = MakeText(canvas.transform, 22, TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(500f, 40f));
        _feedbackText = MakeText(canvas.transform, 40, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1100f, 70f));
    }

    private Text MakeText(Transform parent, int size, TextAnchor anchor, Vector2 anchorMinMax, Vector2 pivot, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject("text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = _font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var rt = text.rectTransform;
        rt.anchorMin = anchorMinMax;
        rt.anchorMax = anchorMinMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        return text;
    }

    private static void Paint(GameObject go, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader);
        material.color = color;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.12f); // matte tiles: no mirror hotspot to blow out
        material.SetFloat("_Metallic", 0f);
        go.GetComponent<Renderer>().material = material;
    }

    private static void SetUpCameraAndLight()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(3.5f, 7.5f, -4.5f);
            cam.transform.LookAt(new Vector3(3.5f, 0f, 3.2f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);

            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
                data.renderPostProcessing = true;
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.28f, 0.30f, 0.36f);

        var keyObj = new GameObject("KeyLight");
        var key = keyObj.AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.15f;
        key.color = new Color(1f, 0.96f, 0.9f);
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.6f;
        keyObj.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

        var fillObj = new GameObject("FillLight");
        var fill = fillObj.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.35f;
        fill.color = new Color(0.7f, 0.8f, 1f);
        fill.shadows = LightShadows.None;
        fillObj.transform.rotation = Quaternion.Euler(30f, 150f, 0f);

        SceneEnvironment.Apply();
    }

    private static void BuildPostProcessing()
    {
        var volumeObj = new GameObject("PostFX");
        var volume = volumeObj.AddComponent<Volume>();
        volume.isGlobal = true;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.profile = profile;

        // Tonemapping compresses bright highlights so the light squares can't blow out to pure white.
        var tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = TonemappingMode.Neutral;

        // Gentle bloom with a high threshold so only genuine highlights glow, not lit tiles.
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.30f;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1.40f;

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.30f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.5f;

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.overrideState = true;
        color.postExposure.value = -0.05f; // was +0.15; slight pull-down to protect highlights
        color.contrast.overrideState = true;
        color.contrast.value = 8f;
        color.saturation.overrideState = true;
        color.saturation.value = 6f;
    }
}
