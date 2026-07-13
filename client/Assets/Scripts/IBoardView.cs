using System;
using UnityEngine;

// A board the screens can drive without caring whether it is the flat 2D board or the 3D board.
// Board2D and Board3DView both implement it; BoardMount picks one per the player's setting.
public interface IBoardView
{
    void Render(string fen, bool canMove, string lastMove, bool whiteBottom);
    void HighlightSquare(string sq, Color color);
    Action<string> SquareClickHandler { get; set; }
    bool AllowPremove { get; set; }

    // Test hook: drive a move through the same input path a real click uses (select from-square, then
    // target), so automated self-tests exercise the board input handlers, not just the move callback.
    void DebugClickMove(string from, string to);

    // Engine-drawn arrows (best move, threat), kept separate from the player's right-click annotations.
    // Passing an empty list clears them. Colors let the analysis board distinguish best move vs threat.
    void SetEngineArrows(System.Collections.Generic.IReadOnlyList<(string from, string to, Color color)> arrows);
}
