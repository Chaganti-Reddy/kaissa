using System;
using UnityEngine;
using UnityEngine.UIElements;

// Chooses the board renderer per the player's Board setting and mounts it into a screen. In 2D mode
// the flat board goes into the center host; in 3D mode the UI root is made transparent so the 3D
// camera shows behind the (opaque) chrome panels, and the 3D board + input are set up instead.
public static class BoardMount
{
    public static IBoardView Create(GameObject go, VisualElement host, VisualElement root,
        Action<string> onMove, PieceAudio audio, float size = 480f)
    {
        if (KaissaSettings.BoardView == 1)
        {
            root.style.backgroundColor = new Color(0, 0, 0, 0); // reveal the 3D board behind the chrome
            // Let clicks over the board region fall through to the 3D scene instead of the transparent UI.
            host.pickingMode = PickingMode.Ignore;
            if (host.parent != null) host.parent.pickingMode = PickingMode.Ignore;
            return new Board3DView(go, onMove, audio);
        }

        var board = new Board2D(onMove);
        board.Root.style.width = size;
        board.Root.style.height = size;
        host.Add(board.Root);
        return board;
    }
}
