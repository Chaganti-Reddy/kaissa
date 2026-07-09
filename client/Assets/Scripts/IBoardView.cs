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
}
