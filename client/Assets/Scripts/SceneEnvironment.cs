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

        // Shader.Find only sees shaders the build kept. Skybox/Panoramic is referenced solely from
        // code, so it gets stripped from player builds and Find returns null - guard it, or the
        // Material ctor throws and takes the whole scene down (blank screen). Falls back to the
        // coded key/fill lighting set up by the caller. Add it to Graphics > Always Included Shaders
        // to get the HDRI skybox in builds.
        var skyShader = Shader.Find("Skybox/Panoramic");
        if (skyShader == null)
            return;

        var skybox = new Material(skyShader);
        skybox.SetTexture("_MainTex", texture);
        skybox.SetFloat("_Mapping", 1f);   // latitude-longitude (equirectangular)
        skybox.SetFloat("_ImageType", 0f); // 360 degrees

        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 0.75f;
        DynamicGI.UpdateEnvironment();

        if (Object.FindAnyObjectByType<ReflectionProbe>() != null)
            return;

        var probeObj = new GameObject("ReflectionProbe");
        probeObj.transform.position = new Vector3(3.5f, 2f, 3.5f);
        var probe = probeObj.AddComponent<ReflectionProbe>();
        probe.size = new Vector3(40f, 20f, 40f);
        probe.intensity = 0.55f;
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.RenderProbe();
    }
}
