using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.Rendering;

// Shared 3D board rendering used by the newer screens (Opening, Endgame, Coordinate). The original
// Training/Rush controllers keep their own copies; this consolidates the rest.
public static class Board3D
{
    // Board colour themes (light, dark), chosen via settings.
    public static readonly (string Name, Color Light, Color Dark)[] Themes =
    {
        ("Walnut", new Color(0.90f, 0.86f, 0.78f), new Color(0.30f, 0.20f, 0.12f)),
        ("Green", new Color(0.93f, 0.93f, 0.82f), new Color(0.46f, 0.58f, 0.34f)),
        ("Blue", new Color(0.86f, 0.89f, 0.92f), new Color(0.40f, 0.55f, 0.70f)),
        ("Slate", new Color(0.82f, 0.82f, 0.85f), new Color(0.28f, 0.30f, 0.36f)),
    };

    /// <summary>Builds a full board with pieces from a BoardView; returns the root transform.</summary>
    public static Transform Render(BoardView board)
    {
        var root = BuildBoardAndTiles();
        foreach (var square in board.Pieces)
        {
            int file = square.Square[0] - 'a';
            int rank = square.Square[1] - '1';
            bool white = char.IsUpper(square.Piece);

            var piece = Pieces.Create(square.Piece, white);
            piece.name = $"pc_{square.Piece}_{square.Square}";
            piece.transform.SetParent(root);
            piece.transform.position = new Vector3(file, 0.075f, rank); // seat base on the tile surface
        }
        return root;
    }

    /// <summary>An empty board (tiles only), for coordinate drills.</summary>
    public static Transform RenderEmpty() => BuildBoardAndTiles();

    private static Transform BuildBoardAndTiles()
    {
        var root = new GameObject("Board").transform;

        var basePlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        basePlate.name = "base";
        basePlate.transform.SetParent(root);
        basePlate.transform.localScale = new Vector3(9.4f, 0.3f, 9.4f);
        basePlate.transform.position = new Vector3(3.5f, -0.2f, 3.5f);
        Object.Destroy(basePlate.GetComponent<Collider>());
        Paint(basePlate, new Color(0.08f, 0.08f, 0.10f));

        var theme = Themes[Mathf.Clamp(KaissaSettings.BoardTheme, 0, Themes.Length - 1)];
        for (int file = 0; file < 8; file++)
        for (int rank = 0; rank < 8; rank++)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"sq_{(char)('a' + file)}{rank + 1}";
            tile.transform.SetParent(root);
            tile.transform.localScale = new Vector3(1f, 0.15f, 1f);
            tile.transform.position = new Vector3(file, 0f, rank);
            Paint(tile, (file + rank) % 2 == 0 ? theme.Dark : theme.Light);
        }

        AddCoordinates(root);
        return root;
    }

    /// <summary>Adds a–h / 1–8 edge labels to a board root (public so all screens can match). No-op
    /// when the coordinates setting is off.</summary>
    public static void AddCoordinates(Transform root)
    {
        if (!KaissaSettings.Coordinates)
            return;
        var font = Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf") as Font;
        for (int f = 0; f < 8; f++)
            Label(root, font, $"{(char)('a' + f)}", new Vector3(f, 0.12f, -0.75f));
        for (int r = 0; r < 8; r++)
            Label(root, font, $"{r + 1}", new Vector3(-0.75f, 0.12f, r));
    }

    private static void Label(Transform root, Font font, string text, Vector3 pos)
    {
        var go = new GameObject($"lbl_{text}");
        go.transform.SetParent(root);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.fontSize = 48;
        tm.characterSize = 0.06f;
        tm.color = new Color(0.7f, 0.7f, 0.75f);
        if (font != null) { tm.font = font; go.GetComponent<MeshRenderer>().material = font.material; }
    }

    public static void Highlight(Transform root, string square, Color color)
    {
        var tile = root.Find($"sq_{square}");
        if (tile != null)
            Paint(tile.gameObject, color);
    }

    public static void Paint(GameObject go, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader);
        material.color = color;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.12f); // matte tiles: no mirror hotspot to blow out
        material.SetFloat("_Metallic", 0f);
        go.GetComponent<Renderer>().material = material;
    }

    /// <summary>Positions the camera so the chosen side is at the bottom (a true 180° board flip).</summary>
    public static void OrientCamera(bool whiteBottom)
    {
        var cam = Camera.main;
        if (cam == null)
            return;
        cam.transform.position = whiteBottom ? new Vector3(3.5f, 7.5f, -4.5f) : new Vector3(3.5f, 7.5f, 11.5f);
        cam.transform.LookAt(new Vector3(3.5f, 0f, 3.5f));
    }

    public static void SetupScene()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(3.5f, 7.5f, -4.5f);
            cam.transform.LookAt(new Vector3(3.5f, 0f, 3.2f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.28f, 0.30f, 0.36f);

        if (Object.FindAnyObjectByType<Light>() == null)
        {
            AddLight("KeyLight", 1.15f, new Color(1f, 0.96f, 0.9f), new Vector3(52f, -35f, 0f), LightShadows.Soft);
            AddLight("FillLight", 0.35f, new Color(0.7f, 0.8f, 1f), new Vector3(30f, 150f, 0f), LightShadows.None);
            AddLight("RimLight", 0.5f, new Color(1f, 0.9f, 0.8f), new Vector3(-20f, 40f, 0f), LightShadows.None);
        }

        SceneEnvironment.Apply(); // HDRI skybox + reflections when present
    }

    private static void AddLight(string name, float intensity, Color color, Vector3 euler, LightShadows shadows)
    {
        var obj = new GameObject(name);
        var light = obj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.shadows = shadows;
        obj.transform.rotation = Quaternion.Euler(euler);
    }
}
