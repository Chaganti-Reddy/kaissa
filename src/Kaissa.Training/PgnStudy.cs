using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Kaissa.Chess.Rules;

namespace Kaissa.Training;

/// <summary>One move of a study chapter: the move in SAN and UCI, plus any comment that followed it.</summary>
public sealed record StudyMove(string San, string Uci, string Comment);

/// <summary>A parsed study chapter: the PGN headers and the mainline moves (with comments). Variations,
/// NAGs and results are dropped - this is a lightweight lesson format, not a full PGN database.</summary>
public sealed record StudyChapter(IReadOnlyDictionary<string, string> Headers, IReadOnlyList<StudyMove> Moves)
{
    public string Title => Headers.TryGetValue("Event", out var e) && !string.IsNullOrWhiteSpace(e)
        ? e : $"{Get("White")} - {Get("Black")}";
    private string Get(string k) => Headers.TryGetValue(k, out var v) ? v : "?";
}

/// <summary>
/// A small PGN reader for authoring lessons and studies as data. It reads the headers and the mainline,
/// attaching each comment to the move it follows, and validates every move by replaying it with the rules
/// engine. Variations, NAGs and the result token are skipped. Robust to comments/variations glued to moves.
/// </summary>
public static class PgnStudy
{
    private static readonly Regex Header = new(@"^\[(\w+)\s+""(.*)""\]", RegexOptions.Compiled);
    private static readonly Regex MoveNumber = new(@"^\d+\.(\.\.)?$", RegexOptions.Compiled);
    private static readonly string[] Results = { "1-0", "0-1", "1/2-1/2", "*" };

    public static StudyChapter Parse(string pgn)
    {
        var headers = new Dictionary<string, string>();
        var body = new System.Text.StringBuilder();
        foreach (var raw in pgn.Split('\n'))
        {
            var line = raw.Trim();
            var m = Header.Match(line);
            if (m.Success) headers[m.Groups[1].Value] = m.Groups[2].Value;
            else if (line.Length > 0) body.Append(line).Append(' ');
        }

        // Space out braces/parens so a simple token pass can handle comments and variations.
        var text = body.ToString()
            .Replace("{", " { ").Replace("}", " } ")
            .Replace("(", " ( ").Replace(")", " ) ");
        var tokens = text.Split(new[] { ' ', '\t', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        var moves = new List<StudyMove>();
        var game = ChessGame.Start();
        int variationDepth = 0;
        bool inComment = false;
        var comment = new System.Text.StringBuilder();

        for (int i = 0; i < tokens.Length; i++)
        {
            var t = tokens[i];
            if (inComment)
            {
                if (t == "}") { inComment = false; if (moves.Count > 0) moves[^1] = moves[^1] with { Comment = comment.ToString().Trim() }; comment.Clear(); }
                else { comment.Append(t).Append(' '); }
                continue;
            }
            if (t == "{") { inComment = true; comment.Clear(); continue; }
            if (t == "(") { variationDepth++; continue; }
            if (t == ")") { if (variationDepth > 0) variationDepth--; continue; }
            if (variationDepth > 0) continue;                 // skip variation moves
            if (Results.Contains(t)) break;                    // game result ends the mainline
            if (t.StartsWith("$")) continue;                   // NAG
            if (MoveNumber.IsMatch(t)) continue;               // "12." / "12..."

            // A move number may be glued to the move, e.g. "1.e4" - strip a leading number+dots.
            var san = Regex.Replace(t, @"^\d+\.(\.\.)?", "");
            if (san.Length == 0) continue;

            var uci = game.ResolveToUci(san);
            if (uci == null || !game.TryMakeMove(uci)) break;  // malformed or illegal - stop the mainline
            moves.Add(new StudyMove(san, uci, ""));
        }

        return new StudyChapter(headers, moves);
    }
}
