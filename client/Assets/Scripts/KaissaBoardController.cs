using System;
using System.Collections;
using System.Collections.Generic;
using Kaissa.Chess.Rules;
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

    private BoardInteractor _interactor;
    private PieceAudio _audio;

    private Text _titleText;
    private Text _ratingText;
    private Text _feedbackText;
    private Font _font;

    private static readonly Color LightSquare = new(0.87f, 0.80f, 0.64f);
    private static readonly Color DarkSquare = new(0.36f, 0.26f, 0.19f);
    private static readonly Color CorrectColor = new(0.30f, 0.85f, 0.45f);
    private static readonly Color WrongColor = new(0.92f, 0.33f, 0.33f);

    private void Start()
    {
        _font = Hud.Font;
        _trainer = KaissaTrainer.CreateDefault(KaissaProgress.Load());
        SetUpCameraAndLight();
        BuildPostProcessing();
        BuildHud();
        _audio = PieceAudio.Attach(gameObject);
        _interactor = gameObject.AddComponent<BoardInteractor>();
        _interactor.Init(uci => OnPlayerMove(uci), _audio);
        DealNext();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
    }

    // Called by the BoardInteractor only for legal moves (illegal ones are rejected before this),
    // so the trainer never grades an impossible move. Shows the played move, then grades and advances.
    private void OnPlayerMove(string uci)
    {
        if (_busy || _board == null)
            return;
        _busy = true;
        _interactor.SetInputEnabled(false);

        var thinkTime = TimeSpan.FromSeconds(Time.time - _cardShownTime);
        var result = _trainer.Answer(uci, thinkTime);
        KaissaProgress.Save(_trainer.ExportProgress());

        // Render the position after the move so click and drag both show the move being made.
        var afterFen = ApplyMove(_board.Fen, uci);
        if (afterFen != null)
            RenderBoard(BoardView.FromFen(afterFen));

        var color = result.Correct ? CorrectColor : WrongColor;
        HighlightSolution(result.Solutions.Count > 0 ? result.Solutions[0] : null, color);

        _feedbackText.color = color;
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

        for (int file = 0; file < 8; file++)
        for (int rank = 0; rank < 8; rank++)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"sq_{(char)('a' + file)}{rank + 1}";
            tile.transform.SetParent(_boardRoot);
            tile.transform.localScale = new Vector3(1f, 0.15f, 1f);
            tile.transform.position = new Vector3(file, 0f, rank);
            Paint(tile, (file + rank) % 2 == 0 ? DarkSquare : LightSquare);
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

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.7f;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1.05f;

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.32f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.5f;

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.overrideState = true;
        color.postExposure.value = 0.15f;
        color.contrast.overrideState = true;
        color.contrast.value = 12f;
        color.saturation.overrideState = true;
        color.saturation.value = 8f;
    }
}
