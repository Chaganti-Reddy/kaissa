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

    public static void Record(double accuracy)
    {
        D.accuracies.Add(accuracy);
        if (D.accuracies.Count > MaxEntries)
            D.accuracies.RemoveRange(0, D.accuracies.Count - MaxEntries);
        File.WriteAllText(Path, JsonUtility.ToJson(D));
    }
}
