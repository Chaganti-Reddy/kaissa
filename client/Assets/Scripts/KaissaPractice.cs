using System.Collections.Generic;
using System.Linq;
using Kaissa.Training;
using Kaissa.Training.Api;
using Kaissa.Training.Play;
using UnityEngine;

// The play-to-train fusion, wired for the client: practice positions generated from the player's own
// game mistakes are saved here and folded into the adaptive trainer so they get scheduled and drilled
// alongside the built-in patterns. Persisted to the same local folder as progress and settings.
public static class KaissaPractice
{
    private static string Path => System.IO.Path.Combine(Application.persistentDataPath, "kaissa-practice.json");

    // Persist newly generated game-practice positions (deduped by position inside the store).
    public static void Add(IEnumerable<Scenario> scenarios)
    {
        var store = PlayerPracticeStore.Load(Path);
        store.AddRange(scenarios);
        store.Save(Path);
    }

    public static int Count => PlayerPracticeStore.Load(Path).Count;

    // Every stored game-mistake position, newest first, for the "From your games" drill.
    public static IReadOnlyList<Scenario> All() =>
        PlayerPracticeStore.Load(Path).Scenarios.Reverse().ToList();

    // Fold the stored positions into a trainer, grouped by the pattern each mistake was tagged with.
    public static void FoldInto(KaissaTrainer trainer)
    {
        var store = PlayerPracticeStore.Load(Path);
        if (store.Count == 0)
            return;

        var byPattern = new Dictionary<PatternId, List<Scenario>>();
        foreach (var s in store.Scenarios)
        {
            if (!byPattern.TryGetValue(s.Pattern, out var list))
                byPattern[s.Pattern] = list = new List<Scenario>();
            list.Add(s);
        }

        foreach (var group in byPattern)
            trainer.AddScenarios(GamePractice.PatternFor(group.Key), group.Value);
    }
}
