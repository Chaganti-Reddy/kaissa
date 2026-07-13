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
        public int boardTheme = 1;  // index into Board3D.Themes / Board2D themes (1 = Green, chess.com-style)
        public bool useModels = true; // modelled pieces vs procedural
        public bool autoQueen;        // auto-promote to queen instead of showing a picker
        public bool dragToMove = true; // allow dragging a piece (click-to-move always works too)
        public bool moveHints = true;  // show legal-move dots + hover preview (off = train recall)
        public bool coordinates = true; // show a-h / 1-8 board edge labels
        public int botSpeed = 1;        // bot move pacing: 0 Fast, 1 Normal, 2 Slow
        public bool fullscreen;       // borderless fullscreen vs a maximized window
        public int boardView;         // 0 = flat 2D board, 1 = 3D board
        public bool evalBar;          // show a live engine eval bar during play
        public bool onboarded;        // has the player seen the first-run welcome
        public string dailyDone = ""; // yyyy-MM-dd of the last solved daily puzzle
        public int dayStreak;         // consecutive days with training activity
        public string lastActive = ""; // yyyy-MM-dd of the last active day
        public long puzzleXp;         // total puzzle XP earned (hybrid progression / tiers)
        public int puzzleBestStreak;  // best consecutive-solve run in a single puzzle session
        public int rushBest3;         // Puzzle Rush best score: 3-minute mode
        public int rushBest5;         // Puzzle Rush best score: 5-minute mode
        public int rushBestSurvival;  // Puzzle Rush best score: survival mode
        public string lessonsDone = ""; // comma-joined ids of completed lessons
        public string lessonCrowns = ""; // per-lesson best crowns, "id:n,id:n" (1-3)
        public int visionBest;        // best light/dark board-vision score in a 30s run
        public int coordBest;         // best coordinate-finding score in a 30s run
        public string pieceSet = "cburnett"; // 2D piece art set (folder under Resources/Pieces2D)
        public string soundTheme = ""; // sound set (folder under Resources/Sounds); empty = Classic procedural
        public bool minimizeToTray; // Windows: hide to a system-tray icon when minimized (convenience)
        public bool highlightMove = true; // highlight the last move's from/to squares
        public bool confirmResign;        // require a second click to resign (misclick guard)
        public bool lowTimeWarning = true; // play a warning sound when your clock runs low
        public int animSpeed = 1;         // piece glide speed: 0 fast, 1 normal, 2 slow
        public string lastOpponent = "";  // last Play opponent label (for the Home rematch card)
        public int lastOpponentElo = -1;  // its fixed Elo, or -1 for Adaptive
        public int lastTc;                // its time-control index
        public int endgameChallengeBestMs; // best time (ms) for the 5-drill Endgame Challenge; 0 = none
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
    public static int BoardView { get => D.boardView; set { D.boardView = value; Save(); } }
    public static bool EvalBar { get => D.evalBar; set { D.evalBar = value; Save(); } }
    public static bool Onboarded { get => D.onboarded; set { D.onboarded = value; Save(); } }
    public static string DailyDone { get => D.dailyDone; set { D.dailyDone = value; Save(); } }
    public static int DayStreak { get => D.dayStreak; set { D.dayStreak = value; Save(); } }
    public static string LastActive { get => D.lastActive; set { D.lastActive = value; Save(); } }
    public static long PuzzleXp { get => D.puzzleXp; set { D.puzzleXp = value; Save(); } }
    public static int PuzzleBestStreak { get => D.puzzleBestStreak; set { D.puzzleBestStreak = value; Save(); } }
    public static int RushBest3 { get => D.rushBest3; set { D.rushBest3 = value; Save(); } }
    public static int RushBest5 { get => D.rushBest5; set { D.rushBest5 = value; Save(); } }
    public static int RushBestSurvival { get => D.rushBestSurvival; set { D.rushBestSurvival = value; Save(); } }

    public static int VisionBest { get => D.visionBest; set { D.visionBest = value; Save(); } }
    public static int CoordBest { get => D.coordBest; set { D.coordBest = value; Save(); } }
    public static string PieceSet { get => string.IsNullOrEmpty(D.pieceSet) ? "cburnett" : D.pieceSet; set { D.pieceSet = value; Save(); } }
    public static string SoundTheme { get => D.soundTheme ?? ""; set { D.soundTheme = value; Save(); } }
    public static bool MinimizeToTray { get => D.minimizeToTray; set { D.minimizeToTray = value; Save(); } }
    public static bool HighlightMove { get => D.highlightMove; set { D.highlightMove = value; Save(); } }
    public static bool ConfirmResign { get => D.confirmResign; set { D.confirmResign = value; Save(); } }
    public static bool LowTimeWarning { get => D.lowTimeWarning; set { D.lowTimeWarning = value; Save(); } }
    public static int AnimSpeed { get => D.animSpeed; set { D.animSpeed = value; Save(); } }
    public static string LastOpponent { get => D.lastOpponent ?? ""; set { D.lastOpponent = value; Save(); } }
    public static int LastOpponentElo { get => D.lastOpponentElo; set { D.lastOpponentElo = value; Save(); } }
    public static int LastTc { get => D.lastTc; set { D.lastTc = value; Save(); } }
    public static int EndgameChallengeBestMs { get => D.endgameChallengeBestMs; set { D.endgameChallengeBestMs = value; Save(); } }
    // Glide duration (ms) from the AnimSpeed preset, shared by the 2D and 3D boards.
    public static int GlideMs => AnimSpeed switch { 0 => 70, 2 => 200, _ => 120 };

    public static bool IsLessonDone(string id) =>
        !string.IsNullOrEmpty(id) && ("," + D.lessonsDone + ",").Contains("," + id + ",");

    public static void MarkLessonDone(string id)
    {
        if (string.IsNullOrEmpty(id) || IsLessonDone(id)) return;
        D.lessonsDone = string.IsNullOrEmpty(D.lessonsDone) ? id : D.lessonsDone + "," + id;
        Save();
    }

    // Per-lesson best crown count (0 = not earned, 1-3 like chess.com's lesson score).
    public static int LessonCrowns(string id)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(D.lessonCrowns)) return 0;
        foreach (var e in D.lessonCrowns.Split(','))
        {
            var kv = e.Split(':');
            if (kv.Length == 2 && kv[0] == id && int.TryParse(kv[1], out int n)) return n;
        }
        return 0;
    }

    public static void SetLessonCrowns(string id, int crowns)
    {
        if (string.IsNullOrEmpty(id) || crowns <= LessonCrowns(id)) return; // keep the best
        var parts = string.IsNullOrEmpty(D.lessonCrowns)
            ? new System.Collections.Generic.List<string>()
            : new System.Collections.Generic.List<string>(D.lessonCrowns.Split(','));
        parts.RemoveAll(e => e.StartsWith(id + ":"));
        parts.Add($"{id}:{crowns}");
        D.lessonCrowns = string.Join(",", parts);
        Save();
    }
}
