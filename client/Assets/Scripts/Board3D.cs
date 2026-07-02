using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.Rendering;

// Shared 3D board rendering used by the newer screens (Opening, Endgame, Coordinate). The original
// Training/Rush controllers keep their own copies; this consolidates the rest.
public static class Board3D
{
    private static readonly Color LightSquare = new(0.87f, 0.80f, 0.64f);
    private static readonly Color DarkSquare = new(0.36f, 0.26f, 0.19f);

    /// <summary>Builds a full board with pieces from a BoardView; returns the root transform.</summary>
    public static Transform Render(BoardView board)
    {
        var root = BuildBoardAndTiles();
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
            piece.transform.SetParent(root);
            piece.transform.position = new Vector3(file, 0.12f, rank);
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

        for (int file = 0; file < 8; file++)
        for (int rank = 0; rank < 8; rank++)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"sq_{(char)('a' + file)}{rank + 1}";
            tile.transform.SetParent(root);
            tile.transform.localScale = new Vector3(1f, 0.15f, 1f);
            tile.transform.position = new Vector3(file, 0f, rank);
            Paint(tile, (file + rank) % 2 == 0 ? DarkSquare : LightSquare);
        }
        return root;
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
        go.GetComponent<Renderer>().material = material;
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
            var lightObj = new GameObject("Sun");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        }
    }
}
