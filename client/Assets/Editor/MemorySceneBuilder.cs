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
    public static void Create()
    {
        const string path = "Assets/Scenes/Memory.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var go = new GameObject("Memory");
        go.AddComponent<MemoryController>();

        EditorSceneManager.SaveScene(scene, path);

        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == path))
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        Debug.Log("MemorySceneBuilder: Memory.unity created and registered in build settings.");
    }
}
#endif
