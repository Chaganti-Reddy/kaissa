namespace Kaissa.Training;

/// <summary>Board-vision helpers and a light/dark-square drill (the "know the board" trainer).</summary>
public static class BoardVision
{
    /// <summary>True if the given square (e.g. "e4") is a light square. a1 is dark.</summary>
    public static bool IsLightSquare(string square)
    {
        int file = square[0] - 'a';
        int rank = square[1] - '1';
        return (file + rank) % 2 == 1;
    }
}

/// <summary>
/// A quick board-vision drill: shown a square, the player answers whether it is light or dark.
/// Deterministic sequence (no RNG) so it is easy to test and reproduce; a client supplies the timer.
/// </summary>
public sealed class VisionSession
{
    private int _index;
    private string _current = "";

    public int Score { get; private set; }
    public int Asked { get; private set; }

    public string NextSquare()
    {
        int file = (_index * 5 + 2) % 8;
        int rank = (_index * 3 + 1) % 8;
        _index++;
        _current = $"{(char)('a' + file)}{rank + 1}";
        return _current;
    }

    /// <summary>Answers the current square; returns whether the guess was correct.</summary>
    public bool Answer(bool guessLight)
    {
        Asked++;
        bool correct = BoardVision.IsLightSquare(_current) == guessLight;
        if (correct)
            Score++;
        return correct;
    }
}
