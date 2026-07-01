using System;
using System.Collections;
using Kaissa.Training;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Puzzle Rush screen: solve as many as possible before three misses. Reuses the board render and
// click-to-move input; drives RushSession. Esc returns to the menu.
public sealed class RushController : MonoBehaviour
{
    private RushSession _rush;
    private Transform _boardRoot;
    private BoardView _board;
    private Scenario _current;
    private float _shownTime;

    private string _originSquare;
    private Transform _selectedPiece;
    private bool _busy;

    private Text _hudText;
    private Text _feedbackText;
    private Font _font;

    private static readonly Color LightSquare = new(0.87f, 0.80f, 0.64f);
    private static readonly Color DarkSquare = new(0.36f, 0.26f, 0.19f);
    private static readonly Color CorrectColor = new(0.30f, 0.85f, 0.45f);
    private static readonly Color WrongColor = new(0.92f, 0.33f, 0.33f);

    private void Start()
    {
        _font = Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf") as Font;
        SetUpCameraAndLight();
        BuildHud();
        _rush = RushSession.CreateDefault(startRating: 800, lives: 3);
        DealNext();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            return;
        }

        var mouse = Mouse.current;
        if (_busy || _rush.IsOver || _board == null || mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        var ray = Camera.main!.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out var hit))
            return;

        var hitName = hit.transform.name;
        if (hitName.Length < 2)
            return;
        var square = hitName.Substring(hitName.Length - 2);

        if (_originSquare == null)
        {
            if (!hitName.StartsWith("pc_", StringComparison.Ordinal))
                return;
            _originSquare = square;
            _selectedPiece = hit.transform;
            _selectedPiece.position += Vector3.up * 0.35f;
        }
        else if (square == _originSquare)
        {
            if (_selectedPiece != null) _selectedPiece.position -= Vector3.up * 0.35f;
            _originSquare = null;
            _selectedPiece = null;
        }
        else
        {
            Submit(_originSquare, square);
        }
    }

    private void Submit(string origin, string target)
    {
        var move = origin + target;
        char moving = PieceAt(origin);
        if ((moving == 'P' && target[1] == '8') || (moving == 'p' && target[1] == '1'))
            move += "q";

        _originSquare = null;
        _selectedPiece = null;

        var result = _rush.Submit(move, TimeSpan.FromSeconds(Time.time - _shownTime));
        StartCoroutine(FeedbackThenNext(result));
    }

    private IEnumerator FeedbackThenNext(RushResult result)
    {
        _busy = true;
        var color = result.Correct ? CorrectColor : WrongColor;
        if (result.Solutions.Count > 0)
            HighlightSquares(result.Solutions[0], color);

        _feedbackText.color = color;
        _feedbackText.text = result.Correct ? "Correct!" : $"Missed — {string.Join(", ", result.Solutions)}";
        UpdateHud();

        yield return new WaitForSeconds(result.Correct ? 0.6f : 1.3f);
        _feedbackText.text = string.Empty;
        _busy = false;

        if (result.IsOver)
        {
            _hudText.text = $"Game over!  Score {result.Score}.   Esc — menu";
            if (_boardRoot != null) Destroy(_boardRoot.gameObject);
        }
        else
        {
            DealNext();
        }
    }

    private void DealNext()
    {
        var scenario = _rush.Next();
        if (scenario == null)
            return;
        _current = scenario;
        _board = BoardView.FromFen(scenario.Fen);
        _shownTime = Time.time;
        UpdateHud();
        RenderBoard(_board);
    }

    private void UpdateHud() =>
        _hudText.text = $"Score {_rush.Score}    Lives {_rush.Lives}    Streak {_rush.Streak}";

    private char PieceAt(string square)
    {
        foreach (var p in _board.Pieces)
            if (p.Square == square)
                return p.Piece;
        return '\0';
    }

    private void HighlightSquares(string uci, Color color)
    {
        if (uci.Length < 4 || _boardRoot == null)
            return;
        foreach (var sq in new[] { uci.Substring(0, 2), uci.Substring(2, 2) })
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

        var basePlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        basePlate.name = "base";
        basePlate.transform.SetParent(_boardRoot);
        basePlate.transform.localScale = new Vector3(9.4f, 0.3f, 9.4f);
        basePlate.transform.position = new Vector3(3.5f, -0.2f, 3.5f);
        Destroy(basePlate.GetComponent<Collider>());
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

            var piece = PieceModelLibrary.TryCreate(square.Piece, white) ?? PieceFactory.Create(square.Piece, white);
            if (piece.GetComponent<Collider>() == null)
            {
                var capsule = piece.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 0.6f, 0f);
                capsule.height = 1.4f;
                capsule.radius = 0.35f;
            }
            piece.name = $"pc_{square.Piece}_{square.Square}";
            piece.transform.SetParent(_boardRoot);
            piece.transform.position = new Vector3(file, 0.12f, rank);
        }
    }

    private void BuildHud()
    {
        var canvasObj = new GameObject("HUD");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        _hudText = MakeText(canvas.transform, 26, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(1100f, 50f));
        _feedbackText = MakeText(canvas.transform, 36, TextAnchor.LowerCenter,
            new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1100f, 60f));
    }

    private Text MakeText(Transform parent, int size, TextAnchor anchor, Vector2 anchorMinMax, Vector2 pos, Vector2 sizeDelta)
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
        rt.anchorMin = rt.anchorMax = anchorMinMax;
        rt.pivot = anchorMinMax;
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
        }

        if (UnityEngine.Object.FindAnyObjectByType<Light>() == null)
        {
            var lightObj = new GameObject("Sun");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        }
    }
}
