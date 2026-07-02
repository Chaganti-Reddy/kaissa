using UnityEngine;
using UnityEngine.Rendering;

// Applies a CC0 HDRI (Assets/Resources/env.hdr) as the lighting environment: skybox-based ambient
// and a reflection probe so the polished pieces actually reflect the studio. No-ops (keeps the
// coded lighting) if the HDRI isn't present, so the project still runs without it.
public static class SceneEnvironment
{
    public static void Apply()
    {
        var texture = Resources.Load<Texture>("env");
        if (texture == null)
            return; // HDRI not added yet

        var skybox = new Material(Shader.Find("Skybox/Panoramic"));
        skybox.SetTexture("_MainTex", texture);
        skybox.SetFloat("_Mapping", 1f);   // latitude-longitude (equirectangular)
        skybox.SetFloat("_ImageType", 0f); // 360 degrees

        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
        DynamicGI.UpdateEnvironment();

        if (Object.FindAnyObjectByType<ReflectionProbe>() != null)
            return;

        var probeObj = new GameObject("ReflectionProbe");
        probeObj.transform.position = new Vector3(3.5f, 2f, 3.5f);
        var probe = probeObj.AddComponent<ReflectionProbe>();
        probe.size = new Vector3(40f, 20f, 40f);
        probe.intensity = 1f;
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.RenderProbe();
    }
}
