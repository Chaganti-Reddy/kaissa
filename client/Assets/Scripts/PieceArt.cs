using System.Collections.Generic;
using UnityEngine;

// Loads 2D piece art (PNG, rasterized from the bundled SVG sets under Resources/Pieces2D/<set>/).
// Piece codes are Lichess-style: colour letter (w/b) + upper piece letter, e.g. "wK", "bQ". Textures
// are cached per set so a board only pays the Resources.Load cost once. Missing art returns null and
// the board falls back to Unicode glyphs, so a partial or absent set never breaks rendering.
public static class PieceArt
{
    // Bundled sets, license-clean (GPL/AGPL/MIT/Apache/CC0/CC-BY/CC-BY-SA - see THIRD-PARTY-NOTICES.md).
    // Display name first, folder second. Order is the swatch order in Settings.
    public static readonly (string name, string folder)[] Sets =
    {
        ("Cburnett", "cburnett"),
        ("Merida", "merida"),
        ("Chessnut", "chessnut"),
        ("Fantasy", "fantasy"),
        ("Spatial", "spatial"),
        ("Celtic", "celtic"),
        ("Pixel", "pixel"),
        ("Rhosgfx", "rhosgfx"),
        ("Pirouetti", "pirouetti"),
        ("Shapes", "shapes"),
        ("Letter", "letter"),
        ("Kiwen-Suwi", "kiwen-suwi"),
        ("MPChess", "mpchess"),
        ("Mono", "mono"),
    };

    private static readonly Dictionary<string, Texture2D> _cache = new();

    // Texture for a FEN piece char (e.g. 'K' white king, 'q' black queen) in the active set, or null.
    public static Texture2D Get(char fenPiece) => Get(KaissaSettings.PieceSet, Code(fenPiece));

    public static Texture2D Get(string set, string code)
    {
        if (string.IsNullOrEmpty(set) || string.IsNullOrEmpty(code)) return null;
        string key = set + "/" + code;
        if (_cache.TryGetValue(key, out var tex)) return tex;
        tex = Resources.Load<Texture2D>("Pieces2D/" + key);
        _cache[key] = tex;
        return tex;
    }

    // FEN piece char -> art code, e.g. 'K' -> "wK", 'n' -> "bN".
    public static string Code(char fenPiece)
    {
        if (fenPiece == '\0') return null;
        char c = char.ToUpperInvariant(fenPiece);
        return (char.IsUpper(fenPiece) ? "w" : "b") + c;
    }

    // True if the given set has at least its white king rendered (used to gate the swatch preview).
    public static bool Available(string set) => Get(set, "wK") != null;
}
