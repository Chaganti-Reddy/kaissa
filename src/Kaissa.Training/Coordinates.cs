namespace Kaissa.Training;

/// <summary>Square-name helpers.</summary>
public static class Coordinates
{
    public static string Name(int file, int rank) => $"{(char)('a' + file)}{rank + 1}";

    public static (int File, int Rank) Parse(string square) => (square[0] - 'a', square[1] - '1');
}

/// <summary>
/// Coordinate trainer: shown a square name, the player clicks the square. Deterministic sequence
/// so it is reproducible and testable; a client supplies the timer and the board to click.
/// </summary>
public sealed class CoordinateSession
{
    private int _index;
    private (int File, int Rank) _target;

    public int Score { get; private set; }
    public int Asked { get; private set; }

    /// <summary>The square name to find next (e.g. "e4").</summary>
    public string NextTarget()
    {
        _index++;
        _target = ((_index * 3 + 1) % 8, (_index * 5 + 2) % 8);
        return Coordinates.Name(_target.File, _target.Rank);
    }

    /// <summary>Answers with the clicked square; true if it matches the asked square.</summary>
    public bool Answer(int file, int rank)
    {
        Asked++;
        bool correct = file == _target.File && rank == _target.Rank;
        if (correct)
            Score++;
        return correct;
    }
}
