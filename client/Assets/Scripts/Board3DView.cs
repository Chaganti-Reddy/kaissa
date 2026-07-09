using System;
using Kaissa.Training.Api;
using UnityEngine;

// Wraps the existing 3D board (Board3D render + BoardInteractor input) behind IBoardView, so a screen
// can use it in place of the 2D board. The 3D board renders through the transparent region of the UI
// Toolkit chrome (see BoardMount, which makes the root transparent in 3D mode).
public sealed class Board3DView : IBoardView
{
    private readonly BoardInteractor _interactor;
    private Transform _root3d;

    // The coordinate drill uses the 2D board; raw square clicks are not wired for 3D.
    public Action<string> SquareClickHandler { get; set; }

    public Board3DView(GameObject go, Action<string> onMove, PieceAudio audio)
    {
        Board3D.SetupScene();
        _interactor = go.AddComponent<BoardInteractor>();
        _interactor.Init(onMove, audio);
    }

    public void Render(string fen, bool canMove, string lastMove, bool whiteBottom)
    {
        var view = BoardView.FromFen(fen);
        if (_root3d != null)
            UnityEngine.Object.Destroy(_root3d.gameObject);
        _root3d = Board3D.Render(view);
        Board3D.OrientCamera(whiteBottom);
        _interactor.OnBoardRendered(_root3d, view, lastMove, canMove);
    }

    public void HighlightSquare(string sq, Color color)
    {
        if (_root3d != null && !string.IsNullOrEmpty(sq) && sq.Length >= 2)
            Board3D.Highlight(_root3d, sq.Substring(0, 2), color);
    }
}
