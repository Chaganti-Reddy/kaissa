// One-shot hand-off telling the Play screen to start immediately against a chosen opponent (from the
// Home "rematch" card), skipping the picker. Mirrors DailyRoute / ThemeRoute.
public static class RematchRoute
{
    public static bool Active;
    public static string Label;
    public static int Elo = -1; // -1 = Adaptive
    public static int Tc;
}
