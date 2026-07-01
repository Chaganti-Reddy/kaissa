using UnityEngine;

// Loads real piece models if the project provides them, otherwise returns null so the caller falls
// back to the procedural PieceFactory. This makes modelled art a drop-in: put prefabs under
// Assets/Resources/Pieces named either "WhitePawn"/"BlackPawn" ... "WhiteKing"/"BlackKing", or a
// single "Pawn".."King" set that we tint by colour. No code change needed once they exist.
public static class PieceModelLibrary
{
    public static GameObject TryCreate(char piece, bool white)
    {
        var name = NameOf(piece);

        var byColor = Resources.Load<GameObject>($"Pieces/{(white ? "White" : "Black")}{name}");
        if (byColor != null)
            return Object.Instantiate(byColor);

        var plain = Resources.Load<GameObject>($"Pieces/{name}");
        if (plain != null)
        {
            var instance = Object.Instantiate(plain);
            Tint(instance, white);
            return instance;
        }

        return null; // no model provided; caller uses the procedural fallback
    }

    private static string NameOf(char piece) => char.ToUpperInvariant(piece) switch
    {
        'P' => "Pawn",
        'N' => "Knight",
        'B' => "Bishop",
        'R' => "Rook",
        'Q' => "Queen",
        'K' => "King",
        _ => "Pawn",
    };

    private static void Tint(GameObject go, bool white)
    {
        var color = white ? new Color(0.95f, 0.95f, 0.92f) : new Color(0.13f, 0.13f, 0.16f);
        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
        {
            var m = renderer.material;
            m.color = color;
            m.SetColor("_BaseColor", color);
        }
    }
}
