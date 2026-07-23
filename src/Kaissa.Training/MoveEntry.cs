using Kaissa.Chess.Rules;

namespace Kaissa.Training;

/// <summary>
/// Turns typed move text into a UCI move against the current position - the keyboard-entry path for
/// accessibility and power users. Accepts SAN ("Nf3", "exd5", "e8=Q", "O-O"), long algebraic
/// ("e2-e4"), raw UCI ("e2e4", "e7e8q"), and is forgiving about check/mate/annotation marks, spacing,
/// case, castling written with zeros, and a missing promotion "=". It matches by normalising both the
/// input and each legal move's own SAN, so it never disagrees with the rules about what is legal.
/// Returns null when nothing legal matches, so a client can reject the input without throwing.
/// </summary>
public static class MoveEntry
{
    public static string? Parse(ChessGame game, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        string norm = Normalize(Zeroize(text));
        if (norm.Length == 0)
            return null;

        var legal = game.LegalUciMoves();

        // Raw UCI (e2e4, e7e8q), possibly upper-cased or dashed - already dash-stripped by Normalize.
        string uci = norm.ToLowerInvariant();
        if (IsUciShape(uci))
            foreach (var m in legal)
                if (string.Equals(m, uci, StringComparison.OrdinalIgnoreCase))
                    return m;

        // SAN: compare the input to each legal move's SAN, both normalised (marks/case/"=" removed).
        foreach (var m in legal)
        {
            var san = game.SanForUci(m);
            if (san != null && string.Equals(Normalize(san), norm, StringComparison.OrdinalIgnoreCase))
                return m;
        }

        return null;
    }

    private static string Zeroize(string text)
    {
        // Castling written with zeros: 0-0 -> O-O so it survives normalisation as "OO".
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
            sb.Append(c == '0' ? 'O' : c);
        return sb.ToString();
    }

    private static string Normalize(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char c in text.Trim())
        {
            if (c is '+' or '#' or '!' or '?' or '=' or '-' or ' ' or '\t')
                continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static bool IsUciShape(string s)
    {
        if (s.Length is not (4 or 5))
            return false;
        if (s[0] is < 'a' or > 'h' || s[2] is < 'a' or > 'h')
            return false;
        if (s[1] is < '1' or > '8' || s[3] is < '1' or > '8')
            return false;
        return s.Length == 4 || "qrbn".IndexOf(char.ToLowerInvariant(s[4])) >= 0;
    }
}
