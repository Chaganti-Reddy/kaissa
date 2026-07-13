using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kaissa.Training;
using UnityEngine;

// A small persisted store of puzzles the player got wrong or gave up on, so they can be re-drilled
// later ("Review misses" mode). Keeps just enough to rebuild a puzzle (start FEN, solver line, setup,
// rating, themes, pattern), deduped by FEN and capped so the file stays small. Local JSON, like
// KaissaProgress.
public static class KaissaMisses
{
    [Serializable]
    private sealed class Miss
    {
        public string fen;
        public string setup;
        public string line;    // comma-joined UCI
        public string themes;  // comma-joined
        public string pattern;
        public int rating;
    }

    [Serializable] private sealed class Store { public List<Miss> items = new(); }

    private const int Cap = 60;
    private static string Path => System.IO.Path.Combine(Application.persistentDataPath, "kaissa-misses.json");
    private static Store _store;

    private static Store S => _store ??= (File.Exists(Path)
        ? JsonUtility.FromJson<Store>(File.ReadAllText(Path)) ?? new Store()
        : new Store());

    public static void Record(string fen, IReadOnlyList<string> line, string setup,
        int rating, IReadOnlyList<string> themes, string pattern)
    {
        if (string.IsNullOrEmpty(fen) || line == null || line.Count == 0) return;
        var s = S;
        s.items.RemoveAll(m => m.fen == fen); // dedupe: keep the most recent occurrence
        s.items.Add(new Miss
        {
            fen = fen,
            setup = setup ?? "",
            line = string.Join(",", line),
            themes = themes != null ? string.Join(",", themes) : "",
            pattern = string.IsNullOrEmpty(pattern) ? "review" : pattern,
            rating = rating,
        });
        while (s.items.Count > Cap) s.items.RemoveAt(0); // drop oldest
        File.WriteAllText(Path, JsonUtility.ToJson(s));
    }

    public static int Count => S.items.Count;

    // Rebuild playable scenarios from the stored misses, newest first.
    public static List<Scenario> AsScenarios()
    {
        var list = new List<Scenario>();
        foreach (var m in Enumerable.Reverse(S.items))
        {
            var line = (m.line ?? "").Split(',').Where(x => x.Length > 0).ToArray();
            if (line.Length == 0) continue;
            var themes = (m.themes ?? "").Split(',').Where(x => x.Length > 0).ToArray();
            list.Add(new Scenario(
                Id: "miss",
                Pattern: new PatternId(m.pattern),
                Fen: m.fen,
                Solutions: new[] { line[0] },
                Prompt: "Find the best move.",
                Rating: m.rating,
                Line: line,
                Themes: themes,
                Setup: string.IsNullOrEmpty(m.setup) ? null : m.setup));
        }
        return list;
    }
}
