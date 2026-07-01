// A tiny, deterministic UCI engine used to exercise the process transport without depending on
// a real engine binary. It understands just enough of the protocol for the CLI and manual tests.
//
// It is intentionally not a chess engine: it always "recommends" 1.e4 and reports fixed scores.

using System.Globalization;

int multiPv = 1;

string? line;
while ((line = Console.ReadLine()) is not null)
{
    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0)
        continue;

    switch (parts[0])
    {
        case "uci":
            Console.WriteLine("id name Kaissa.FakeUci 1.0");
            Console.WriteLine("id author Kaissa");
            Console.WriteLine("option name UCI_LimitStrength type check default false");
            Console.WriteLine("option name UCI_Elo type spin default 1320 min 1320 max 3190");
            Console.WriteLine("option name MultiPV type spin default 1 min 1 max 5");
            Console.WriteLine("uciok");
            break;

        case "isready":
            Console.WriteLine("readyok");
            break;

        case "setoption":
            var nameIdx = Array.IndexOf(parts, "MultiPV");
            var valueIdx = Array.IndexOf(parts, "value");
            if (nameIdx >= 0 && valueIdx >= 0 && valueIdx + 1 < parts.Length &&
                int.TryParse(parts[valueIdx + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mpv))
            {
                multiPv = Math.Clamp(mpv, 1, 5);
            }
            break;

        case "ucinewgame":
        case "position":
            break;

        case "go":
            string[][] candidates =
            {
                new[] { "e2e4", "e7e5" },
                new[] { "d2d4", "d7d5" },
                new[] { "g1f3", "g8f6" },
            };
            for (int i = 0; i < multiPv && i < candidates.Length; i++)
            {
                var score = 34 - (i * 12);
                Console.WriteLine(
                    $"info depth 12 multipv {i + 1} score cp {score} pv {string.Join(' ', candidates[i])}");
            }
            Console.WriteLine("bestmove e2e4 ponder e7e5");
            break;

        case "quit":
            return 0;
    }
}

return 0;
