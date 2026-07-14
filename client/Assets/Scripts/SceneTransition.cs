using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Smooth page transitions. Every scene load fades in from a solid cover; navigating away fades out to
// the cover first, then loads. The cover is a child VisualElement of the scene's own UIDocument root
// (child opacity animates reliably - unlike a panel ROOT's opacity, a cloned panel, or a uGUI overlay,
// the three approaches that failed earlier). Fade-in is global via sceneLoaded, so it applies no matter
// how a scene was entered; fade-out happens when navigation goes through SceneTransition.Go.
public sealed class SceneTransition : MonoBehaviour
{
    private static SceneTransition _inst;
    private const int RevealMs = 200; // cover fades out to reveal the page
    private const int CoverMs = 160;  // cover fades in before leaving

    // Set by StartupAnimation so the boot animation owns the intro to the first scene (no double cover).
    public static bool SkipInitialReveal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (_inst != null) return;
        var go = new GameObject("SceneTransition");
        DontDestroyOnLoad(go);
        _inst = go.AddComponent<SceneTransition>();
        SceneManager.sceneLoaded += (_, __) => _inst.StartCoroutine(_inst.RevealWhenReady());
        if (!SkipInitialReveal)
            _inst.StartCoroutine(_inst.RevealWhenReady()); // the first (already-loaded) scene
    }

    // Navigate to a scene with a fade-out; falls back to a direct load if the transitioner is absent.
    // A page can install a guard (e.g. a live game) that intercepts navigation away. If it returns
    // false the navigation is cancelled; the guard is then responsible for confirming and retrying.
    public static System.Func<string, bool> LeaveGuard;

    public static void Go(string scene)
    {
        if (LeaveGuard != null && !LeaveGuard(scene)) return;
        if (_inst == null) { SceneManager.LoadScene(scene); return; }
        _inst.StartCoroutine(_inst.FadeOutAndLoad(scene));
    }

    private IEnumerator RevealWhenReady()
    {
        VisualElement root = null;
        for (int i = 0; i < 180; i++) // wait up to ~3s for the controller to build its UI tree
        {
            root = FindRoot();
            if (root != null && root.childCount > 0) break;
            root = null;
            yield return null;
        }
        if (root == null) yield break;

        var cover = AddCover(root, PickingMode.Ignore); // never block input; it is only a visual reveal
        cover.style.opacity = 1f;
        cover.experimental.animation
            .Start(1f, 0f, RevealMs, (e, v) => e.style.opacity = v)
            .OnCompleted(() => { if (cover.parent != null) cover.parent.Remove(cover); });
    }

    private IEnumerator FadeOutAndLoad(string scene)
    {
        var root = FindRoot();
        if (root == null) { SceneManager.LoadScene(scene); yield break; }

        var cover = AddCover(root, PickingMode.Position); // block input while leaving
        cover.style.opacity = 0f;
        bool done = false;
        cover.experimental.animation
            .Start(0f, 1f, CoverMs, (e, v) => e.style.opacity = v)
            .OnCompleted(() => done = true);

        float t = 0f;
        while (!done && t < 0.5f) { t += Time.deltaTime; yield return null; }
        SceneManager.LoadScene(scene);
    }

    // The active scene's UI root - the UIDocument with the most children (the controller's, not a stray).
    internal static VisualElement FindRoot()
    {
        var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        UIDocument best = null;
        foreach (var d in docs)
        {
            if (d.rootVisualElement == null) continue;
            if (best == null || d.rootVisualElement.childCount > best.rootVisualElement.childCount)
                best = d;
        }
        return best?.rootVisualElement;
    }

    private static VisualElement AddCover(VisualElement root, PickingMode pick)
    {
        var c = new VisualElement { pickingMode = pick };
        c.style.position = Position.Absolute;
        c.style.left = 0; c.style.top = 0; c.style.right = 0; c.style.bottom = 0;
        c.style.backgroundColor = UiKit.Bg;
        root.Add(c); // last child renders on top
        return c;
    }
}
