using System.Threading.Tasks;
using Kaissa.Training;
using UnityEngine;

// Warms the heavy bundled content (opening book + 51k-puzzle set) on background threads at app launch,
// so by the time the player opens Openings/Puzzles/Rush the parse is already done and the page opens
// instantly - the user never waits. Safe to run off the main thread: both loads are now pure JSON
// deserialization (no rules-engine calls), and both cache their result for the rest of the run.
public static class KaissaPreloader
{
    private static bool _started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Warm()
    {
        if (_started) return;
        _started = true;

        Task.Run(() =>
        {
            try { OpeningBook.LoadDefault(); }
            catch (System.Exception e) { Debug.LogWarning($"Preload openings failed: {e.Message}"); }
        });
        Task.Run(() =>
        {
            try { ScenarioLibrary.LoadDefault(); }
            catch (System.Exception e) { Debug.LogWarning($"Preload scenarios failed: {e.Message}"); }
        });

        // Launch the shared Stockfish processes now (on the main thread) so Play/Endgames/Analysis
        // never pay a per-page engine spawn - the ~1-2s launch happens once, in the background, at start.
        try { EngineHub.Warm(); }
        catch (System.Exception e) { Debug.LogWarning($"Preload engine failed: {e.Message}"); }
    }
}
