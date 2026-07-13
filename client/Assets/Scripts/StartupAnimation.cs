using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

// One-time boot animation over the first scene (Home): the Kaissa mark fades and scales in, a thin
// progress bar fills while the engine warms and content preloads in the background, then the whole
// overlay fades out to reveal the app. It is a child of the scene's UIDocument root (child opacity
// animates reliably), and it takes over the intro from SceneTransition so the first scene is not
// revealed twice. Plays only on a real launch - suppressed under the screenshot harness (except the
// dedicated -kaissa-startup capture) so automated runs are not delayed.
public sealed class StartupAnimation : MonoBehaviour
{
    private static bool _enabled;
    private static bool _played;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Prime()
    {
        var args = Environment.GetCommandLineArgs();
        bool harness = args.Any(a => a.StartsWith("-kaissa-", StringComparison.Ordinal));
        bool startupTest = args.Contains("-kaissa-startup");
        if (harness && !startupTest) return; // don't stall the other harness modes
        _enabled = true;
        SceneTransition.SkipInitialReveal = true; // the boot animation is the intro to scene 0
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!_enabled || _played) return;
        _played = true;
        var go = new GameObject("StartupAnimation");
        DontDestroyOnLoad(go);
        go.AddComponent<StartupAnimation>().StartCoroutine(Run());
    }

    // Timeline (seconds): mark in 0..0.46, bar fills 0.30..1.12, hold to 1.30, overlay fades out
    // 1.30..1.64. Driven manually per-frame (not experimental.animation) so it survives the first
    // controller clearing/rebuilding its root - the overlay is re-attached and kept on top each frame.
    private const float InEnd = 0.46f, FillStart = 0.30f, FillEnd = 1.12f, FadeStart = 1.30f, FadeEnd = 1.64f;

    private static IEnumerator Run()
    {
        VisualElement root = null;
        for (int i = 0; i < 180; i++) // wait for the first controller to build its UI tree
        {
            root = SceneTransition.FindRoot();
            if (root != null && root.childCount > 0) break;
            root = null;
            yield return null;
        }
        if (root == null) yield break;

        var ov = new VisualElement { pickingMode = PickingMode.Position }; // block input while it plays
        ov.style.position = Position.Absolute;
        ov.style.left = 0; ov.style.top = 0; ov.style.right = 0; ov.style.bottom = 0;
        ov.style.backgroundColor = UiKit.Bg;
        ov.style.alignItems = Align.Center;
        ov.style.justifyContent = Justify.Center;

        var col = new VisualElement { pickingMode = PickingMode.Ignore };
        col.style.alignItems = Align.Center;
        col.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));

        var mark = UiKit.Text_("♞", 104, UiKit.GreenHi, bold: true); // the brand knight, as in the nav rail
        mark.style.unityTextAlign = TextAnchor.MiddleCenter;
        var word = UiKit.Text_("Kaissa", 58, UiKit.Text, bold: true);
        word.style.unityTextAlign = TextAnchor.MiddleCenter; word.style.marginTop = 2;
        var sub = UiKit.Text_("Train the patterns behind strong play", 15, UiKit.Dim);
        sub.style.unityTextAlign = TextAnchor.MiddleCenter; sub.style.marginTop = 10;

        var track = new VisualElement();
        track.style.width = 240; track.style.height = 3; track.style.marginTop = 26;
        track.style.backgroundColor = UiKit.Panel2; UiKit.Radius(track, 2);
        var fill = new VisualElement();
        fill.style.height = Length.Percent(100); fill.style.width = Length.Percent(0);
        fill.style.backgroundColor = UiKit.GreenHi; UiKit.Radius(fill, 2);
        track.Add(fill);

        col.Add(mark); col.Add(word); col.Add(sub); col.Add(track);
        ov.Add(col);

        float t = 0f;
        while (t < FadeEnd)
        {
            // Keep the overlay attached and on top even if a controller cleared/rebuilt its root.
            var r = SceneTransition.FindRoot();
            if (r != null)
            {
                if (ov.parent != r) { ov.RemoveFromHierarchy(); r.Add(ov); }
                else ov.BringToFront();
            }

            float inP = Mathf.Clamp01(t / InEnd);
            col.style.opacity = inP;
            float s = 0.86f + 0.14f * OutBack(inP);
            col.style.scale = new Scale(new Vector3(s, s, 1f));

            float fillP = Mathf.Clamp01((t - FillStart) / (FillEnd - FillStart));
            fill.style.width = Length.Percent(100f * fillP);

            if (t >= FadeStart)
                ov.style.opacity = 1f - Mathf.Clamp01((t - FadeStart) / (FadeEnd - FadeStart));

            t += Time.deltaTime;
            yield return null;
        }

        ov.RemoveFromHierarchy();
        if (_instGo != null) Destroy(_instGo);
    }

    private static GameObject _instGo;
    private void Awake() => _instGo = gameObject;

    private static float OutBack(float x)
    {
        const float s = 1.70158f;
        x -= 1f;
        return 1f + (s + 1f) * x * x * x + s * x * x;
    }
}
