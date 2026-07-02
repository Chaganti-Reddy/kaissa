using System;
using System.IO;
using Kaissa.Chess.Rules;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Play a full game against the adaptive bot (desktop). Drives KaissaGame, which runs Stockfish
// from StreamingAssets. Click a piece then a target to move; the bot replies; the rating updates
// and the game is reviewed at the end. Self-contained (builds its own scene/HUD) so it can run in
// its own scene. Placeholder visuals, like the training screen.
public sealed class KaissaGameController : MonoBehaviour
{
    private KaissaGame _game;
    private Transform _boardRoot;
    private bool _busy;

    private string _originSquare;
    private Transform _selectedPiece;

    private Text _titleText;
    private Text _statusText;
    private Font _font;
    private AudioSource _audio;
    private AudioClip _moveClip;

    private static readonly Color LightSquare = new(0.87f, 0.80f, 0.64f);
    private static readonly Color DarkSquare = new(0.36f, 0.26f, 0.19f);

    private async void Start()
    {
        _font = Hud.Font;
        SetUpCameraAndLight();
        BuildPostProcessing();
        BuildAudio();
        BuildHud();

        var enginePath = Path.Combine(Application.streamingAssetsPath, "stockfish", "stockfish.exe");
        if (!File.Exists(enginePath))
        {
            _statusText.text = "Stockfish not found. Run scripts/build-unity-plugins.ps1.";
            return;
        }

        _titleText.text = "Play vs Bot";
        _statusText.text = "Starting engine...";
        var startFen = EndgameRoute.Fen; // set by the endgame picker, if any
        EndgameRoute.Fen = null;
        try
        {
            _game = await KaissaGame.StartAsync(enginePath, Side.White, 1200,
                fen: startFen, botThinkTime: TimeSpan.FromMilliseconds(150));
        }
        catch (Exception e)
        {
            _statusText.text = "Engine failed to start (see Console).";
            Debug.LogError(e);
            return;
        }

        _statusText.text = $"You are White. Bot ~{_game.OpponentElo} Elo. Your move.";
        RenderBoard(_game.Board);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            return;
        }

        var mouse = Mouse.current;
        if (_busy || _game == null || mouse == null || !mouse.leftButton.wasPressedThisFrame)
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
            if (!char.IsUpper(hitName[3]))
                return; // you play White; can't move Black's pieces
            _originSquare = square;
            _selectedPiece = hit.transform;
            _selectedPiece.position += Vector3.up * 0.35f;
            SetGlow(_selectedPiece, true);
            if (KaissaSettings.Sound && _moveClip != null) _audio.PlayOneShot(_moveClip);
        }
        else if (square == _originSquare)
        {
            if (_selectedPiece != null) { _selectedPiece.position -= Vector3.up * 0.35f; SetGlow(_selectedPiece, false); }
            _originSquare = null;
            _selectedPiece = null;
        }
        else
        {
            OnMove(_originSquare, square);
        }
    }

    private async void OnMove(string origin, string target)
    {
        _busy = true;
        _originSquare = null;
        _selectedPiece = null;

        var move = origin + target;
        char moving = PieceCharAt(origin);
        if ((moving == 'P' && target[1] == '8') || (moving == 'p' && target[1] == '1'))
            move += "q";

        try
        {
            var outcome = await _game.PlayAsync(move);
            if (!outcome.Accepted)
            {
                _statusText.text = "Illegal move — try again.";
                _busy = false;
                RenderBoard(_game.Board);
                return;
            }

            if (KaissaSettings.Sound && _moveClip != null) _audio.PlayOneShot(_moveClip);
            RenderBoard(outcome.Board);
            HighlightMove(move);
            if (!string.IsNullOrEmpty(outcome.BotMove)) HighlightMove(outcome.BotMove!);

            if (outcome.IsGameOver)
            {
                _statusText.text = $"Game over: {outcome.Result}. Rating {_game.PlayerRating:0}. Reviewing...";
                var review = await _game.ReviewAsync();
                _statusText.text = $"Game over: {outcome.Result}. Rating {_game.PlayerRating:0}. " +
                                   $"{review.Mistakes.Count} mistake(s); {review.Practice.Count} added to practice.";
            }
            else
            {
                _statusText.text = $"Bot played {outcome.BotMove}. Your move.";
            }
        }
        catch (Exception e)
        {
            _statusText.text = "Engine error (see Console).";
            Debug.LogError(e);
        }

        _busy = false;
    }

    private char PieceCharAt(string square)
    {
        foreach (var p in _game.Board.Pieces)
            if (p.Square == square)
                return p.Piece;
        return '\0';
    }

    private void HighlightMove(string uci)
    {
        if (uci.Length < 4 || _boardRoot == null)
            return;
        var color = new Color(0.95f, 0.85f, 0.35f); // soft amber for the last move
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

    private static void SetGlow(Transform piece, bool on)
    {
        foreach (var renderer in piece.GetComponentsInChildren<Renderer>())
        {
            var m = renderer.material;
            if (on)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.9f, 0.8f, 0.3f) * 0.8f);
            }
            else
            {
                m.SetColor("_EmissionColor", Color.black);
            }
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
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(1100f, 50f));
        _statusText = MakeText(canvas.transform, 22, TextAnchor.LowerCenter,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1200f, 60f));
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

    private void BuildAudio()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.08f);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            data[i] = Mathf.Sin(2f * Mathf.PI * 330f * i / sampleRate) * Mathf.Exp(-4f * t) * 0.5f;
        }
        _moveClip = AudioClip.Create("move", samples, 1, sampleRate, false);
        _moveClip.SetData(data, 0);
    }

    private void OnDestroy()
    {
        if (_game != null)
            _ = _game.DisposeAsync();
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

        if (UnityEngine.Object.FindAnyObjectByType<Light>() == null)
        {
            var keyObj = new GameObject("KeyLight");
            var key = keyObj.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.15f;
            key.color = new Color(1f, 0.96f, 0.9f);
            key.shadows = LightShadows.Soft;
            keyObj.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        }

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
        bloom.intensity.value = 0.6f;

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.3f;
    }
}
