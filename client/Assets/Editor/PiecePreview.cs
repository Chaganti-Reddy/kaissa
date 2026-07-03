using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Editor-only: renders a row of white and black pieces on tiles to a PNG so piece proportions,
// seating, and orientation can be checked without running the game. Run headless:
//   Unity -batchmode -quit -projectPath client -executeMethod PiecePreview.Render -outfile <path>
public static class PiecePreview
{
    public static void Render()
    {
        string outfile = "piece-preview.png";
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-outfile") outfile = args[i + 1];

        var spawned = new List<GameObject>();
        const string order = "PNBRQK";

        // Tiles + pieces: white on rank 0, black on rank 1.
        for (int f = 0; f < order.Length; f++)
        {
            for (int rank = 0; rank < 2; rank++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.transform.localScale = new Vector3(1f, 0.15f, 1f);
                tile.transform.position = new Vector3(f, 0f, rank);
                Paint(tile, (f + rank) % 2 == 0 ? new Color(0.30f, 0.20f, 0.12f) : new Color(0.90f, 0.86f, 0.78f));
                spawned.Add(tile);

                bool white = rank == 0;
                char pc = white ? order[f] : char.ToLowerInvariant(order[f]);
                var piece = Pieces.Create(pc, white);
                piece.transform.position = new Vector3(f, 0.075f, rank);
                spawned.Add(piece);
            }
        }

        // Lights.
        spawned.Add(MakeLight("Key", 2.4f, new Color(1f, 0.97f, 0.92f), new Vector3(50f, -30f, 0f)));
        spawned.Add(MakeLight("Fill", 1.0f, new Color(0.8f, 0.85f, 1f), new Vector3(30f, 150f, 0f)));
        spawned.Add(MakeLight("Rim", 0.8f, new Color(1f, 0.9f, 0.8f), new Vector3(-20f, 40f, 0f)));
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.62f);

        // Camera, three-quarter view.
        var camObj = new GameObject("PreviewCam");
        var cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.45f, 0.47f, 0.52f);
        cam.fieldOfView = 32f;
        cam.transform.position = new Vector3(2.5f, 4.2f, -4.6f);
        cam.transform.LookAt(new Vector3(2.5f, 0.5f, 0.6f));
        spawned.Add(camObj);

        int w = 1600, h = 800;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        cam.targetTexture = rt;

        // URP does not light a plain camera.Render() in batch/edit mode; go through the pipeline.
        var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
        if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request))
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
        else
            cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;

        File.WriteAllBytes(outfile, tex.EncodeToPNG());
        Debug.Log($"PiecePreview: wrote {outfile}");

        Object.DestroyImmediate(rt);
        foreach (var go in spawned)
            if (go != null) Object.DestroyImmediate(go);
    }

    private static GameObject MakeLight(string name, float intensity, Color color, Vector3 euler)
    {
        var obj = new GameObject(name);
        var light = obj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        obj.transform.rotation = Quaternion.Euler(euler);
        return obj;
    }

    private static void Paint(GameObject go, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader);
        m.color = color;
        m.SetColor("_BaseColor", color);
        go.GetComponent<Renderer>().material = m;
    }
}
