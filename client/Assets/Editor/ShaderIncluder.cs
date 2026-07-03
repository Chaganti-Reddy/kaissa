using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Some shaders are referenced only from code (e.g. Skybox/Panoramic in SceneEnvironment). The build
// strips those because no scene or material asset uses them, so Shader.Find returns null at runtime.
// This adds them to Graphics > Always Included Shaders so they ship. Run once (it writes the
// reference into ProjectSettings/GraphicsSettings.asset, which is committed):
//   Unity -batchmode -quit -projectPath client -executeMethod ShaderIncluder.EnsureAlwaysIncluded
public static class ShaderIncluder
{
    private static readonly string[] Required =
    {
        "Skybox/Panoramic",                  // HDRI skybox (SceneEnvironment)
        "Universal Render Pipeline/Unlit",   // translucent board highlight overlays (BoardFx)
    };

    [MenuItem("Kaissa/Ensure Always-Included Shaders")]
    public static void EnsureAlwaysIncluded()
    {
        var graphicsSettings = GraphicsSettings.GetGraphicsSettings();
        var so = new SerializedObject(graphicsSettings);
        var list = so.FindProperty("m_AlwaysIncludedShaders");

        foreach (var name in Required)
        {
            var shader = Shader.Find(name);
            if (shader == null)
            {
                Debug.LogWarning($"ShaderIncluder: shader not found in editor: {name}");
                continue;
            }

            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    present = true;
                    break;
                }
            }

            if (!present)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                Debug.Log($"ShaderIncluder: added {name}");
            }
        }

        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }
}
