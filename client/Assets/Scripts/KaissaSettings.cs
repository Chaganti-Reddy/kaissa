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
        public bool autoQueen;        // auto-promote to queen instead of showing a picker
        public bool dragToMove = true; // allow dragging a piece (click-to-move always works too)
        public bool moveHints = true;  // show legal-move dots + hover preview (off = train recall)
        public bool coordinates = true; // show a-h / 1-8 board edge labels
        public int botSpeed = 1;        // bot move pacing: 0 Fast, 1 Normal, 2 Slow
        public bool fullscreen;       // borderless fullscreen vs a maximized window
        public bool onboarded;        // has the player seen the first-run welcome
        public string dailyDone = ""; // yyyy-MM-dd of the last solved daily puzzle
        public int dayStreak;         // consecutive days with training activity
        public string lastActive = ""; // yyyy-MM-dd of the last active day
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
    public static bool AutoQueen { get => D.autoQueen; set { D.autoQueen = value; Save(); } }
    public static bool DragToMove { get => D.dragToMove; set { D.dragToMove = value; Save(); } }
    public static bool MoveHints { get => D.moveHints; set { D.moveHints = value; Save(); } }
    public static bool Coordinates { get => D.coordinates; set { D.coordinates = value; Save(); } }
    public static int BotSpeed { get => D.botSpeed; set { D.botSpeed = value; Save(); } }
    public static bool Fullscreen { get => D.fullscreen; set { D.fullscreen = value; Save(); } }
    public static bool Onboarded { get => D.onboarded; set { D.onboarded = value; Save(); } }
    public static string DailyDone { get => D.dailyDone; set { D.dailyDone = value; Save(); } }
    public static int DayStreak { get => D.dayStreak; set { D.dayStreak = value; Save(); } }
    public static string LastActive { get => D.lastActive; set { D.lastActive = value; Save(); } }
}
