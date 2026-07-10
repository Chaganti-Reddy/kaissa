using Kaissa.Training.Api;
using UnityEngine;

// Temporary smoke test: proves the Kaissa core DLLs load and run inside Unity - it loads the
// bundled puzzles, deals the first card, and logs it. Attach to a GameObject and press Play;
// check the Console. Once this works we replace it with the real board UI.
public sealed class KaissaSmokeTest : MonoBehaviour
{
    private void Start()
    {
        var trainer = KaissaTrainer.CreateDefault();
        var card = trainer.NextCard();

        if (card is null)
        {
            Debug.LogError("Kaissa: no card returned - content failed to load.");
            return;
        }

        Debug.Log($"Kaissa OK - pattern '{card.PatternName}' (puzzle {card.PuzzleRating}, " +
                  $"you {card.PlayerRating:0}). Prompt: {card.Prompt}");
        Debug.Log($"Board: {card.Board.Pieces.Count} pieces, whiteToMove={card.Board.WhiteToMove}, fen={card.Board.Fen}");
    }
}
