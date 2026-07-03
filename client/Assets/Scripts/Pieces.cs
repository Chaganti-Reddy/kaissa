using UnityEngine;

// Central piece factory + normaliser. Whatever the source (real modelled OBJ via PieceModelLibrary
// or the procedural PieceFactory), the mesh is scaled to a consistent Staunton-like height, seated
// so its base sits on the board (y = 0 at the piece root), centred on its square, and knights are
// turned to face up the board. Both render paths (KaissaBoardController and Board3D) go through here
// so pieces read the same everywhere.
public static class Pieces
{
    // Heights in board units (a tile is 1 unit square). Relative proportions follow a Staunton set.
    private static float TargetHeight(char piece) => char.ToUpperInvariant(piece) switch
    {
        'P' => 0.95f,
        'N' => 1.12f,
        'B' => 1.24f,
        'R' => 1.02f,
        'Q' => 1.42f,
        'K' => 1.60f,
        _ => 1.0f,
    };

    private const float MaxFootprint = 0.84f; // keep the base within a tile

    /// <summary>Creates a fully placed-ready piece: base at the root origin, centred, correct size.</summary>
    public static GameObject Create(char piece, bool white)
    {
        var model = PieceModelLibrary.TryCreate(piece, white) ?? PieceFactory.Create(piece, white);

        var root = new GameObject($"pc_{piece}");
        model.transform.SetParent(root.transform, false);

        Normalize(root, model.transform, piece);
        Orient(model.transform, piece, white);
        ApplyMaterial(root, white);

        // One clean click target on the root; drop any colliders the source mesh brought.
        foreach (var existing in root.GetComponentsInChildren<Collider>())
            Object.Destroy(existing);
        AddCollider(root, piece);
        return root;
    }

    // Assign one consistent material to every renderer/slot so a piece reads the same regardless of
    // whatever material the OBJ or procedural mesh shipped with. Ivory vs polished obsidian.
    private static void ApplyMaterial(GameObject root, bool white)
    {
        var color = white ? new Color(0.93f, 0.90f, 0.83f) : new Color(0.09f, 0.09f, 0.12f);
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader);
        mat.color = color;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", white ? 0.30f : 0.55f);
        mat.SetFloat("_Metallic", 0f);

        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
        {
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = mat;
            renderer.sharedMaterials = slots;
        }
    }

    private static void Normalize(GameObject root, Transform model, char piece)
    {
        var bounds = MeshBounds(root);
        if (bounds.size.y <= 0.0001f)
            return;

        float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
        float scale = TargetHeight(piece) / bounds.size.y;
        if (footprint * scale > MaxFootprint)
            scale = MaxFootprint / footprint;

        model.localScale *= scale;

        // Re-measure after scaling, then seat the base at y = 0 and centre on x/z.
        bounds = MeshBounds(root);
        model.localPosition += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
    }

    // Knights are the only asymmetric piece that must face a direction. White faces up the board
    // (+z), black faces down (-z). The base rotation aligns the model's own forward to +z.
    private static void Orient(Transform model, char piece, bool white)
    {
        if (char.ToUpperInvariant(piece) != 'N')
            return;
        model.localRotation = Quaternion.Euler(0f, white ? 0f : 180f, 0f) * model.localRotation;
    }

    private static Bounds MeshBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        var bounds = new Bounds(root.transform.position, Vector3.zero);
        bool has = false;
        foreach (var r in renderers)
        {
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return bounds;
    }

    private static void AddCollider(GameObject root, char piece)
    {
        float h = TargetHeight(piece);
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, h * 0.5f, 0f);
        capsule.height = h;
        capsule.radius = 0.36f;
    }
}
