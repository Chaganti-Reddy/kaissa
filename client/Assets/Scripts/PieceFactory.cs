using UnityEngine;

// Builds simple, recognisable chess pieces from primitives (no art assets needed yet). Each piece
// is a single GameObject with a capsule collider on the root, so board clicks resolve to the whole
// piece. Real modelled art can replace this later without touching the controller.
public static class PieceFactory
{
    private static readonly Color White = new(0.95f, 0.95f, 0.92f);
    private static readonly Color Black = new(0.13f, 0.13f, 0.16f);

    public static GameObject Create(char piece, bool white)
    {
        var root = new GameObject();
        var color = white ? White : Black;

        void Add(PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion? rot = null)
        {
            var part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = pos;
            part.transform.localScale = scale;
            if (rot.HasValue)
                part.transform.localRotation = rot.Value;

            var collider = part.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider); // only the root carries a collider
            Paint(part, color);
        }

        Add(PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.5f, 0.12f, 0.5f)); // base

        switch (char.ToUpperInvariant(piece))
        {
            case 'P':
                Add(PrimitiveType.Sphere, new Vector3(0f, 0.38f, 0f), new Vector3(0.34f, 0.34f, 0.34f));
                break;
            case 'R':
                Add(PrimitiveType.Cylinder, new Vector3(0f, 0.38f, 0f), new Vector3(0.34f, 0.4f, 0.34f));
                Add(PrimitiveType.Cube, new Vector3(0f, 0.66f, 0f), new Vector3(0.5f, 0.16f, 0.5f));
                break;
            case 'N':
                Add(PrimitiveType.Cylinder, new Vector3(0f, 0.34f, 0f), new Vector3(0.3f, 0.3f, 0.3f));
                Add(PrimitiveType.Cube, new Vector3(0f, 0.62f, 0.05f), new Vector3(0.28f, 0.5f, 0.42f),
                    Quaternion.Euler(18f, 0f, 0f));
                break;
            case 'B':
                Add(PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(0.3f, 0.42f, 0.3f));
                Add(PrimitiveType.Sphere, new Vector3(0f, 0.72f, 0f), new Vector3(0.3f, 0.36f, 0.3f));
                Add(PrimitiveType.Sphere, new Vector3(0f, 0.95f, 0f), new Vector3(0.1f, 0.1f, 0.1f));
                break;
            case 'Q':
                Add(PrimitiveType.Cylinder, new Vector3(0f, 0.44f, 0f), new Vector3(0.34f, 0.5f, 0.34f));
                Add(PrimitiveType.Sphere, new Vector3(0f, 0.86f, 0f), new Vector3(0.42f, 0.3f, 0.42f));
                Add(PrimitiveType.Sphere, new Vector3(0f, 1.05f, 0f), new Vector3(0.16f, 0.16f, 0.16f));
                break;
            case 'K':
                Add(PrimitiveType.Cylinder, new Vector3(0f, 0.46f, 0f), new Vector3(0.34f, 0.55f, 0.34f));
                Add(PrimitiveType.Sphere, new Vector3(0f, 0.92f, 0f), new Vector3(0.32f, 0.28f, 0.32f));
                Add(PrimitiveType.Cube, new Vector3(0f, 1.18f, 0f), new Vector3(0.08f, 0.28f, 0.08f)); // cross
                Add(PrimitiveType.Cube, new Vector3(0f, 1.18f, 0f), new Vector3(0.22f, 0.08f, 0.08f));
                break;
        }

        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 0.6f, 0f);
        capsule.height = 1.4f;
        capsule.radius = 0.35f;
        return root;
    }

    private static void Paint(GameObject go, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader);
        material.color = color;
        material.SetColor("_BaseColor", color);
        go.GetComponent<Renderer>().material = material;
    }
}
