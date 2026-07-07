using System.Collections.Generic;
using Kaissa.Chess.Rules;
using Kaissa.Training.Api;
using UnityEngine;
using UnityEngine.Rendering;

// Board highlight overlays: selected square, last move, check, hover, and legal-move markers. All are
// thin translucent quads (or small dots) parented to the board root and named "fx_*" so they can be
// cleared without rebuilding the board. Tile squares are named sq_<file><rank> at (file, 0, rank);
// the tile top is y = 0.075, so overlays sit just above it and below the pieces' silhouettes.
public static class BoardFx
{
    private static readonly Color SelectColor  = new(1.00f, 0.90f, 0.30f, 0.45f);
    private static readonly Color LastMoveColor = new(0.95f, 0.80f, 0.25f, 0.35f);
    private static readonly Color CheckColor    = new(0.95f, 0.20f, 0.15f, 0.55f);
    private static readonly Color HoverColor    = new(1.00f, 1.00f, 1.00f, 0.22f);
    private static readonly Color DotColor      = new(0.20f, 0.80f, 0.35f, 0.85f);
    private static readonly Color CaptureColor  = new(0.90f, 0.30f, 0.25f, 0.40f);
    private static readonly Color PremoveColor  = new(0.35f, 0.65f, 0.95f, 0.50f);

    // y offsets per layer to avoid z-fighting between stacked overlays.
    private const float LastMoveY = 0.078f, SelectY = 0.080f, CheckY = 0.082f, HoverY = 0.084f;

    public static void ClearByPrefix(Transform root, string prefix)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (child.name.StartsWith(prefix, System.StringComparison.Ordinal))
                Object.Destroy(child.gameObject);
        }
    }

    public static void ClearAll(Transform root)   => ClearByPrefix(root, "fx_");
    public static void ClearHover(Transform root)  => ClearByPrefix(root, "fx_hover");
    public static void ClearSelect(Transform root) => ClearByPrefix(root, "fx_sel");
    public static void ClearDots(Transform root)   { ClearByPrefix(root, "fx_dot"); ClearByPrefix(root, "fx_cap"); }

    public static void LastMove(Transform root, string uci)
    {
        ClearByPrefix(root, "fx_last");
        if (root == null || uci == null || uci.Length < 4) return;
        Quad(root, uci.Substring(0, 2), LastMoveColor, LastMoveY, "fx_last");
        Quad(root, uci.Substring(2, 2), LastMoveColor, LastMoveY, "fx_last");
    }

    public static void Selected(Transform root, string square)
    {
        ClearSelect(root);
        Quad(root, square, SelectColor, SelectY, "fx_sel");
    }

    private static readonly Color HintColor = new(1.00f, 0.78f, 0.25f, 0.60f);

    // Highlights the from-square of a suggested move.
    public static void HintSquare(Transform root, string square)
    {
        ClearByPrefix(root, "fx_hint");
        if (square != null)
            Quad(root, square, HintColor, SelectY, "fx_hint");
    }

    public static void ClearPremove(Transform root) => ClearByPrefix(root, "fx_pre");

    // Shows a queued premove: the origin square, and the target too once chosen.
    public static void Premove(Transform root, string from, string to)
    {
        ClearByPrefix(root, "fx_pre");
        Quad(root, from, PremoveColor, SelectY, "fx_pre");
        if (to != null)
            Quad(root, to, PremoveColor, SelectY, "fx_pre");
    }

    public static void Hover(Transform root, string square)
    {
        ClearHover(root);
        if (square != null)
            Quad(root, square, HoverColor, HoverY, "fx_hover");
    }

    // Highlights the checked king's square (red) if the side to move is in check.
    public static void Check(Transform root, BoardView board)
    {
        ClearByPrefix(root, "fx_check");
        if (root == null || board == null) return;
        var game = ChessGameSafe(board.Fen);
        if (game == null || !game.IsCheck) return;

        char king = board.WhiteToMove ? 'K' : 'k';
        foreach (var p in board.Pieces)
            if (p.Piece == king)
            {
                Quad(root, p.Square, CheckColor, CheckY, "fx_check");
                return;
            }
    }

    // Legal targets from a square: a dot on empty squares, a translucent tint on capturable ones.
    public static void LegalTargets(Transform root, string from, IReadOnlyList<string> legalUci, BoardView board)
    {
        ClearDots(root);
        if (root == null || from == null || legalUci == null) return;

        var occupied = new HashSet<string>();
        if (board != null)
            foreach (var p in board.Pieces)
                occupied.Add(p.Square);

        foreach (var uci in legalUci)
        {
            if (uci.Length < 4 || !uci.StartsWith(from, System.StringComparison.Ordinal)) continue;
            var target = uci.Substring(2, 2);
            if (occupied.Contains(target))
                Quad(root, target, CaptureColor, SelectY, "fx_cap");
            else
                Dot(root, target);
        }
    }

    private static ChessGame ChessGameSafe(string fen)
    {
        try { return ChessGame.FromFen(fen); }
        catch { return null; }
    }

    private static void Quad(Transform root, string square, Color color, float y, string name)
    {
        if (root == null || square == null || square.Length < 2) return;
        int file = square[0] - 'a';
        int rank = square[1] - '1';
        if (file < 0 || file > 7 || rank < 0 || rank > 7) return;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        var col = quad.GetComponent<Collider>();
        if (col != null) Object.Destroy(col); // never block board raycasts
        quad.transform.SetParent(root, false);
        quad.transform.position = new Vector3(file, y, rank);
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, facing up
        quad.transform.localScale = new Vector3(0.96f, 0.96f, 1f);
        quad.GetComponent<Renderer>().material = Transparent(color);
    }

    private static void Dot(Transform root, string square)
    {
        int file = square[0] - 'a';
        int rank = square[1] - '1';
        var dot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dot.name = "fx_dot";
        var col = dot.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        dot.transform.SetParent(root, false);
        dot.transform.position = new Vector3(file, 0.09f, rank);
        dot.transform.localScale = new Vector3(0.30f, 0.02f, 0.30f);
        dot.GetComponent<Renderer>().material = Transparent(DotColor);
    }

    private static Material Transparent(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        mat.color = color;
        mat.SetColor("_BaseColor", color);
        // Configure URP/Unlit for alpha blending.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        return mat;
    }
}
