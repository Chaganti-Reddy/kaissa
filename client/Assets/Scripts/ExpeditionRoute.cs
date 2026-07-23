// Set by the Expeditions screen before launching Play, read by KaissaGameController at game end so the
// result counts toward the active expedition. A plain static hand-off, like the other *Route classes.
public static class ExpeditionRoute
{
    public static bool Active;
    public static string ExpeditionId;
}
