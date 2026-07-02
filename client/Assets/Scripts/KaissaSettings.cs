using System;
using System.IO;
using UnityEngine;

// Player preferences (sound, board flip, board theme, piece style), persisted locally. Uses
// Unity's JsonUtility so it needs no extra dependency. Read/written through static properties.
public static class KaissaSettings
{
    [Serializable]
    private sealed class Data
    {
        public bool sound = true;
        public bool flip = true;
        public int boardTheme;      // index into Board3D.Themes
        public bool useModels = true; // modelled pieces vs procedural
    }

    private static Data _data;
    private static string Path => System.IO.Path.Combine(Application.persistentDataPath, "kaissa-settings.json");

    private static Data D
    {
        get
        {
            if (_data == null)
                _data = File.Exists(Path) ? JsonUtility.FromJson<Data>(File.ReadAllText(Path)) ?? new Data() : new Data();
            return _data;
        }
    }

    private static void Save() => File.WriteAllText(Path, JsonUtility.ToJson(D));

    public static bool Sound { get => D.sound; set { D.sound = value; Save(); } }
    public static bool Flip { get => D.flip; set { D.flip = value; Save(); } }
    public static int BoardTheme { get => D.boardTheme; set { D.boardTheme = value; Save(); } }
    public static bool UseModels { get => D.useModels; set { D.useModels = value; Save(); } }
}
