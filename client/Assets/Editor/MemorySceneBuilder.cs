#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Creates the Memory trainer scene (a Main Camera + a GameObject carrying MemoryController) and
// registers it in the build settings, matching how the other single-controller scenes are set up.
// Run from the menu, or in batchmode via -executeMethod MemorySceneBuilder.Create.
public static class MemorySceneBuilder
{
    [MenuItem("Kaissa/Create Memory Scene")]
    public static void Create() => CreateScene<MemoryController>("Memory");

    [MenuItem("Kaissa/Create Captures Scene")]
    public static void CreateCaptures() => CreateScene<CapturesController>("Captures");

    [MenuItem("Kaissa/Create Visualization Scene")]
    public static void CreateVisualization() => CreateScene<VisualizationController>("Visualization");

    [MenuItem("Kaissa/Create Solo Chess Scene")]
    public static void CreateSolo() => CreateScene<SoloChessController>("SoloChess");

    // Build a single-controller scene (Main Camera + Directional Light from DefaultGameObjects, plus a
    // GameObject carrying the controller) and register it in the build settings, like the other scenes.
    private static void CreateScene<T>(string sceneName) where T : Component
    {
        string path = $"Assets/Scenes/{sceneName}.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var go = new GameObject(sceneName);
        go.AddComponent<T>();

        EditorSceneManager.SaveScene(scene, path);

        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == path))
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        Debug.Log($"MemorySceneBuilder: {sceneName}.unity created and registered in build settings.");
    }
}
#endif
