using System.IO;
using System.Threading.Tasks;
using Kaissa.Chess.Engine;
using UnityEngine;

// One persistent Stockfish process per role - a play engine (strength-capped per move by the bot) and a
// full-strength analysis engine - launched once and reused across every screen. No page spawns its own
// engine and none is respawned when switching pages; both are warmed at launch so the first use is
// instant too. Two fixed-role processes (rather than one) avoid reconfiguring strength on every move and
// avoid races between the live eval bar and the bot, which run concurrently on the Play screen.
public static class EngineHub
{
    private static Task<IChessEngine> _play;
    private static Task<IChessEngine> _analysis;

    public static string EnginePath => Path.Combine(Application.streamingAssetsPath, "stockfish", "stockfish.exe");
    public static bool Available => File.Exists(EnginePath);

    public static Task<IChessEngine> PlayEngineAsync() => _play ??= LaunchAsync();
    public static Task<IChessEngine> AnalysisEngineAsync() => _analysis ??= LaunchAsync();

    private static async Task<IChessEngine> LaunchAsync()
    {
        var engine = UciChessEngine.LaunchProcess(EnginePath);
        await engine.HandshakeAsync();
        return new SerializedEngine(engine); // guard against overlapping searches on the shared process
    }

    // Spawn both processes in the background at launch, so no page ever shows "starting engine".
    public static void Warm()
    {
        if (!Available) return;
        _ = PlayEngineAsync();
        _ = AnalysisEngineAsync();
        EngineHubLifetime.Ensure();
    }

    public static async void Shutdown()
    {
        var play = _play; var analysis = _analysis;
        _play = null; _analysis = null;
        try { if (play != null) await (await play).DisposeAsync(); } catch { }
        try { if (analysis != null) await (await analysis).DisposeAsync(); } catch { }
    }
}

// Kills the shared engine processes when the app quits (they are not owned by any scene).
public sealed class EngineHubLifetime : MonoBehaviour
{
    private static EngineHubLifetime _instance;

    public static void Ensure()
    {
        if (_instance != null) return;
        var go = new GameObject("EngineHubLifetime");
        Object.DontDestroyOnLoad(go);
        _instance = go.AddComponent<EngineHubLifetime>();
    }

    private void OnApplicationQuit() => EngineHub.Shutdown();
}
