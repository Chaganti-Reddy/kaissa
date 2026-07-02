using UnityEngine;

// Builds our own chess pieces as lathed geometry (a spun silhouette) plus a few detail bits for
// the parts that are not rotationally symmetric (rook battlements, king cross, queen crown, knight
// head). Fully procedural — our IP, no assets. A single capsule collider on the root handles clicks.
public static class PieceFactory
{
    // Marble ivory vs polished obsidian.
    private static readonly Color White = new(0.94f, 0.91f, 0.84f);
    private static readonly Color Black = new(0.07f, 0.07f, 0.10f);

    // Shared base silhouette (radius, height), bottom to top.
    private static readonly Vector2[] BaseProfile =
    {
        new(0f, 0f), new(0.34f, 0f), new(0.34f, 0.07f), new(0.25f, 0.12f), new(0.19f, 0.18f),
    };

    public static GameObject Create(char piece, bool white)
    {
        var root = new GameObject();
        var color = white ? White : Black;

        switch (char.ToUpperInvariant(piece))
        {
            case 'P':
                Lathe(root, Profile.Pawn, color);
                break;
            case 'B':
                Lathe(root, Profile.Bishop, color);
                break;
            case 'Q':
                Lathe(root, Profile.Queen, color);
                Crown(root, color, 0.90f, 0.26f);
                break;
            case 'K':
                Lathe(root, Profile.King, color);
                AddBox(root, color, new Vector3(0f, 1.16f, 0f), new Vector3(0.07f, 0.26f, 0.07f)); // cross
                AddBox(root, color, new Vector3(0f, 1.14f, 0f), new Vector3(0.20f, 0.07f, 0.07f));
                break;
            case 'R':
                Lathe(root, Profile.Rook, color);
                Battlements(root, color, 0.72f, 0.30f);
                break;
            case 'N':
                Lathe(root, Profile.KnightBase, color);
                KnightHead(root, color);
                break;
            default:
                Lathe(root, Profile.Pawn, color);
                break;
        }

        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 0.55f, 0f);
        capsule.height = 1.3f;
        capsule.radius = 0.34f;
        return root;
    }

    private static void Lathe(GameObject root, Vector2[] profile, Color color)
    {
        var body = new GameObject("body");
        body.transform.SetParent(root.transform, false);
        body.AddComponent<MeshFilter>().mesh = LatheMesh.Build(profile, segments: 44);
        body.AddComponent<MeshRenderer>().material = Material(color);
    }

    private static void Crown(GameObject root, Color color, float y, float radius)
    {
        for (int i = 0; i < 6; i++)
        {
            float a = 2f * Mathf.PI * i / 6f;
            AddSphere(root, color, new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius), 0.11f);
        }
    }

    private static void Battlements(GameObject root, Color color, float y, float radius)
    {
        for (int i = 0; i < 6; i++)
        {
            float a = 2f * Mathf.PI * i / 6f;
            AddBox(root, color, new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius),
                new Vector3(0.12f, 0.16f, 0.12f));
        }
    }

    private static void KnightHead(GameObject root, Color color)
    {
        AddBox(root, color, new Vector3(0f, 0.5f, 0.02f), new Vector3(0.26f, 0.34f, 0.44f),
            Quaternion.Euler(20f, 0f, 0f));                                   // head/neck
        AddBox(root, color, new Vector3(0f, 0.62f, 0.22f), new Vector3(0.2f, 0.16f, 0.26f),
            Quaternion.Euler(45f, 0f, 0f));                                   // muzzle
        AddBox(root, color, new Vector3(0.07f, 0.78f, -0.08f), new Vector3(0.05f, 0.14f, 0.05f)); // ears
        AddBox(root, color, new Vector3(-0.07f, 0.78f, -0.08f), new Vector3(0.05f, 0.14f, 0.05f));
    }

    private static void AddBox(GameObject root, Color color, Vector3 pos, Vector3 scale, Quaternion? rot = null)
        => AddPrimitive(root, PrimitiveType.Cube, color, pos, scale, rot);

    private static void AddSphere(GameObject root, Color color, Vector3 pos, float diameter)
        => AddPrimitive(root, PrimitiveType.Sphere, color, pos, new Vector3(diameter, diameter, diameter));

    private static void AddPrimitive(GameObject root, PrimitiveType type, Color color, Vector3 pos, Vector3 scale, Quaternion? rot = null)
    {
        var part = GameObject.CreatePrimitive(type);
        part.transform.SetParent(root.transform, false);
        part.transform.localPosition = pos;
        part.transform.localScale = scale;
        if (rot.HasValue)
            part.transform.localRotation = rot.Value;

        var collider = part.GetComponent<Collider>();
        if (collider != null)
            Object.Destroy(collider);
        part.GetComponent<Renderer>().material = Material(color);
    }

    private static Material Material(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader);
        m.color = color;
        m.SetColor("_BaseColor", color);

        // Premium stone look: polished, faintly metallic; obsidian glossier than marble.
        bool dark = color.grayscale < 0.5f;
        m.SetFloat("_Smoothness", dark ? 0.72f : 0.55f);
        m.SetFloat("_Metallic", 0.15f);
        m.SetFloat("_Cull", 0f); // double-sided, so lathe winding never hides a face
        return m;
    }

    private static class Profile
    {
        public static readonly Vector2[] Pawn =
        {
            new(0f, 0f), new(0.30f, 0f), new(0.30f, 0.06f), new(0.21f, 0.11f), new(0.15f, 0.20f),
            new(0.12f, 0.29f), new(0.18f, 0.34f), new(0.13f, 0.38f), new(0.19f, 0.45f),
            new(0.20f, 0.53f), new(0.13f, 0.62f), new(0f, 0.67f),
        };

        public static readonly Vector2[] Bishop =
        {
            new(0f, 0f), new(0.33f, 0f), new(0.33f, 0.07f), new(0.24f, 0.12f), new(0.16f, 0.22f),
            new(0.13f, 0.45f), new(0.19f, 0.50f), new(0.13f, 0.54f), new(0.20f, 0.66f),
            new(0.16f, 0.74f), new(0.10f, 0.82f), new(0.12f, 0.86f), new(0f, 0.93f),
        };

        public static readonly Vector2[] Queen =
        {
            new(0f, 0f), new(0.37f, 0f), new(0.37f, 0.07f), new(0.27f, 0.13f), new(0.18f, 0.25f),
            new(0.15f, 0.55f), new(0.22f, 0.62f), new(0.30f, 0.70f), new(0.31f, 0.80f),
            new(0.22f, 0.86f), new(0f, 0.90f),
        };

        public static readonly Vector2[] King =
        {
            new(0f, 0f), new(0.37f, 0f), new(0.37f, 0.07f), new(0.27f, 0.13f), new(0.18f, 0.25f),
            new(0.15f, 0.60f), new(0.22f, 0.67f), new(0.30f, 0.74f), new(0.28f, 0.83f),
            new(0.20f, 0.88f), new(0.20f, 0.98f), new(0f, 1.02f),
        };

        public static readonly Vector2[] Rook =
        {
            new(0f, 0f), new(0.36f, 0f), new(0.36f, 0.07f), new(0.26f, 0.12f), new(0.22f, 0.20f),
            new(0.22f, 0.58f), new(0.30f, 0.63f), new(0.30f, 0.72f), new(0f, 0.72f),
        };

        public static readonly Vector2[] KnightBase =
        {
            new(0f, 0f), new(0.36f, 0f), new(0.36f, 0.07f), new(0.26f, 0.12f), new(0.21f, 0.22f),
            new(0.19f, 0.34f), new(0.20f, 0.40f), new(0f, 0.42f),
        };
    }
}
