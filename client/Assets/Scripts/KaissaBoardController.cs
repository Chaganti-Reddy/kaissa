using System;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.InputSystem;

// The training screen (MVP): renders the current card's position with placeholder primitives and
// lets the player make a move by clicking a piece and then a destination square. The move is graded
// by the Kaissa core and the next card is dealt. Real art and UI polish come later.
public sealed class KaissaBoardController : MonoBehaviour
{
    private KaissaTrainer _trainer;
    private Transform _boardRoot;
    private BoardView _board;
    private float _cardShownTime;

    private string _originSquare;
    private Transform _selectedPiece;

    private static readonly Color LightSquare = new(0.88f, 0.82f, 0.68f);
    private static readonly Color DarkSquare = new(0.40f, 0.30f, 0.22f);
    private static readonly Color WhitePiece = new(0.95f, 0.95f, 0.92f);
    private static readonly Color BlackPiece = new(0.12f, 0.12f, 0.14f);

    private void Start()
    {
        _trainer = KaissaTrainer.CreateDefault();
        SetUpCameraAndLight();
        DealNext();
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || _board == null)
            return;

        var ray = Camera.main!.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out var hit))
            return;

        var name = hit.transform.name;
        if (name.Length < 2)
            return;
        var square = name.Substring(name.Length - 2); // objects are named "sq_e4" / "pc_R_e4"

        if (_originSquare == null)
        {
            if (!name.StartsWith("pc_", StringComparison.Ordinal))
                return; // must start on a piece
            _originSquare = square;
            _selectedPiece = hit.transform;
            _selectedPiece.position += Vector3.up * 0.3f; // lift to show selection
        }
        else if (square == _originSquare)
        {
            Deselect();
        }
        else
        {
            SubmitMove(_originSquare, square);
        }
    }

    private void SubmitMove(string origin, string target)
    {
        var move = origin + target;

        // Naive promotion: a pawn reaching the far rank promotes to a queen (refined later).
        char moving = PieceAt(origin);
        if ((moving == 'P' && target[1] == '8') || (moving == 'p' && target[1] == '1'))
            move += "q";

        var thinkTime = TimeSpan.FromSeconds(Time.time - _cardShownTime);
        var result = _trainer.Answer(move, thinkTime);

        Debug.Log(result.Correct
            ? $"Correct ({result.Grade}). Next review in {result.NextReviewInDays} day(s). Rating {result.PlayerRating:0} ({result.PlayerRatingChange:+0;-0})."
            : $"Not it. A solution was {string.Join(", ", result.Solutions)}. Rating {result.PlayerRating:0} ({result.PlayerRatingChange:+0;-0}).");

        _originSquare = null;
        _selectedPiece = null;
        DealNext();
    }

    private void Deselect()
    {
        if (_selectedPiece != null)
            _selectedPiece.position -= Vector3.up * 0.3f;
        _originSquare = null;
        _selectedPiece = null;
    }

    private void DealNext()
    {
        var card = _trainer.NextCard();
        if (card == null)
        {
            Debug.Log("No more cards.");
            return;
        }

        _board = card.Board;
        _cardShownTime = Time.time;
        Debug.Log($"{card.PatternName} — {card.Prompt} (puzzle {card.PuzzleRating}, you {card.PlayerRating:0})");
        RenderBoard(_board);
    }

    private char PieceAt(string square)
    {
        foreach (var p in _board.Pieces)
            if (p.Square == square)
                return p.Piece;
        return '\0';
    }

    private void RenderBoard(BoardView board)
    {
        if (_boardRoot != null)
            Destroy(_boardRoot.gameObject);
        _boardRoot = new GameObject("Board").transform;

        for (int file = 0; file < 8; file++)
        for (int rank = 0; rank < 8; rank++)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"sq_{(char)('a' + file)}{rank + 1}";
            tile.transform.SetParent(_boardRoot);
            tile.transform.localScale = new Vector3(1f, 0.1f, 1f);
            tile.transform.position = new Vector3(file, 0f, rank);
            Paint(tile, (file + rank) % 2 == 0 ? DarkSquare : LightSquare);
        }

        foreach (var square in board.Pieces)
        {
            int file = square.Square[0] - 'a';
            int rank = square.Square[1] - '1';
            bool white = char.IsUpper(square.Piece);

            var piece = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            piece.name = $"pc_{square.Piece}_{square.Square}";
            piece.transform.SetParent(_boardRoot);
            piece.transform.localScale = new Vector3(0.55f, 0.35f, 0.55f);
            piece.transform.position = new Vector3(file, 0.45f, rank);
            Paint(piece, white ? WhitePiece : BlackPiece);
            AddLabel(piece.transform, char.ToUpperInvariant(square.Piece), white);
        }
    }

    private static void AddLabel(Transform parent, char letter, bool white)
    {
        var labelObj = new GameObject("label");
        labelObj.transform.SetParent(parent);
        labelObj.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        labelObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        var text = labelObj.AddComponent<TextMesh>();
        text.text = letter.ToString();
        text.anchor = TextAnchor.MiddleCenter;
        text.fontSize = 64;
        text.characterSize = 0.15f;
        text.color = white ? Color.black : Color.white;

        var font = Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf") as Font;
        if (font != null)
        {
            text.font = font;
            labelObj.GetComponent<MeshRenderer>().material = font.material;
        }
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
            cam.transform.position = new Vector3(3.5f, 8f, -3.5f);
            cam.transform.LookAt(new Vector3(3.5f, 0f, 3.5f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.10f);
        }

        if (UnityEngine.Object.FindFirstObjectByType<Light>() == null)
        {
            var lightObj = new GameObject("Sun");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }
}
