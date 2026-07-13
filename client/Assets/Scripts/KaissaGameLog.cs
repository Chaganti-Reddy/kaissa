using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// A small local log of finished-game accuracy scores, so the Stats screen can show how accurately the
// player has been playing over time. Kept in the same folder as the other player data.
public static class KaissaGameLog
{
    [Serializable]
    private sealed class Data
    {
        public List<double> accuracies = new();
        public List<int> results = new(); // parallel to accuracies: 0 loss, 1 draw, 2 win
    }

    private const int MaxEntries = 100;
    private static Data _data;
    private static string Path => System.IO.Path.Combine(Application.persistentDataPath, "kaissa-games.json");

    private static Data D
    {
        get
        {
            if (_data == null)
                _data = File.Exists(Path) ? JsonUtility.FromJson<Data>(File.ReadAllText(Path)) ?? new Data() : new Data();
            return _data;
        }
    }

    public static int Count => D.accuracies.Count;
    public static double Average => D.accuracies.Count == 0 ? 0 : D.accuracies.Average();

    // The most recent up-to-n game accuracies, oldest-first.
    public static IReadOnlyList<double> Recent(int n) =>
        D.accuracies.Skip(Math.Max(0, D.accuracies.Count - n)).ToList();

    public static int Wins => D.results.Count(r => r == 2);
    public static int Draws => D.results.Count(r => r == 1);
    public static int Losses => D.results.Count(r => r == 0);

    public static void Record(double accuracy, int result = 1)
    {
        D.accuracies.Add(accuracy);
        D.results.Add(result);
        if (D.accuracies.Count > MaxEntries) D.accuracies.RemoveRange(0, D.accuracies.Count - MaxEntries);
        if (D.results.Count > MaxEntries) D.results.RemoveRange(0, D.results.Count - MaxEntries);
        File.WriteAllText(Path, JsonUtility.ToJson(D));
    }
}
