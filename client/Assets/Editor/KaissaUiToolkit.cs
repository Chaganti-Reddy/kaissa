#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// Generates the runtime UI Toolkit assets the client loads from Resources: a PanelSettings (scaled to
// a 1280x720 reference like the rest of the UI) and a theme stylesheet. Run from the menu, or in
// batchmode via -executeMethod KaissaUiToolkit.Generate, so the assets exist before a player build.
public static class KaissaUiToolkit
{
    [MenuItem("Kaissa/Generate UI Toolkit Panel")]
    public static void Generate()
    {
        const string dir = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Resources");

        // Reuse an existing theme if the project/packages already provide one; otherwise make a blank
        // theme (our own USS supplies all styling, so default control skins aren't needed).
        const string themePath = dir + "/KaissaTheme.tss";
        var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
        if (theme == null)
        {
            var found = AssetDatabase.FindAssets("t:ThemeStyleSheet");
            if (found.Length > 0)
                theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(found[0]));
        }
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            AssetDatabase.CreateAsset(theme, themePath);
        }

        const string psPath = dir + "/KaissaPanel.asset";
        var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(psPath);
        if (ps == null)
        {
            ps = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(ps, psPath);
        }
        ps.themeStyleSheet = theme;
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = new Vector2Int(1280, 720);
        ps.match = 0.5f;

        EditorUtility.SetDirty(ps);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("KaissaUiToolkit: panel settings + theme generated.");
    }
}
#endif
