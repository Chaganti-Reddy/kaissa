using System;
using UnityEngine;
using UnityEngine.UIElements;

// A short, board-agnostic celebration flourish for a solve/win: two expanding rings and a drawn
// checkmark that pops in, over the board area, then fades and removes itself. Painted entirely with
// painter2D (no glyphs, honoring the ASCII/no-icon source rule) so it works on the 2D and 3D board
// hosts alike. Call BoardCelebrate.Burst(boardHost) at the win moment.
public static class BoardCelebrate
{
    private const float DurationMs = 620f;

    public static void Burst(VisualElement host, bool positive = true)
    {
        if (host == null) return;

        var ov = new VisualElement { pickingMode = PickingMode.Ignore };
        ov.style.position = Position.Absolute;
        ov.style.left = 0; ov.style.top = 0; ov.style.right = 0; ov.style.bottom = 0;
        host.Add(ov);

        Color col = positive ? new Color(0.58f, 0.76f, 0.36f, 1f) : new Color(0.79f, 0.29f, 0.23f, 1f);
        float t = 0f;

        ov.generateVisualContent += ctx =>
        {
            var r = ov.contentRect;
            if (r.width <= 1f) return;
            var p = ctx.painter2D;
            Vector2 c = new(r.width / 2f, r.height / 2f);
            float unit = Mathf.Min(r.width, r.height);

            // Expanding rings (second one lags for a ripple).
            DrawRing(p, c, unit, col, Mathf.Clamp01(t / 0.85f));
            DrawRing(p, c, unit, col, Mathf.Clamp01((t - 0.18f) / 0.85f));

            // Checkmark pops in over the back third of the animation.
            float ck = Mathf.Clamp01((t - 0.30f) / 0.55f);
            if (ck > 0f) DrawCheck(p, c, unit, col, ck);
        };

        var start = Environment.TickCount;
        ov.schedule.Execute(() =>
        {
            t = Mathf.Clamp01((Environment.TickCount - start) / DurationMs);
            ov.MarkDirtyRepaint();
        }).Every(16).Until(() => t >= 1f);

        ov.schedule.Execute(() => { if (ov.parent != null) ov.parent.Remove(ov); }).StartingIn((long)DurationMs + 40);
    }

    private static void DrawRing(Painter2D p, Vector2 c, float unit, Color col, float prog)
    {
        if (prog <= 0f || prog >= 1f) return;
        float radius = Mathf.Lerp(unit * 0.10f, unit * 0.46f, EaseOut(prog));
        var rc = col; rc.a = (1f - prog) * 0.9f;
        p.strokeColor = rc;
        p.lineWidth = Mathf.Lerp(unit * 0.05f, unit * 0.012f, prog);
        p.BeginPath();
        p.Arc(c, radius, 0f, 360f);
        p.Stroke();
    }

    // A checkmark that scales up with a slight overshoot and fades near the end.
    private static void DrawCheck(Painter2D p, Vector2 c, float unit, Color col, float prog)
    {
        float s = unit * 0.16f * OvershootOut(prog);
        var cc = col; cc.a = prog > 0.8f ? (1f - prog) / 0.2f : 1f;
        Vector2 a = c + new Vector2(-1.0f, 0.05f) * s;
        Vector2 b = c + new Vector2(-0.30f, 0.75f) * s;
        Vector2 d = c + new Vector2(1.05f, -0.75f) * s;
        p.strokeColor = cc;
        p.lineWidth = unit * 0.035f;
        p.lineCap = LineCap.Round;
        p.lineJoin = LineJoin.Round;
        p.BeginPath();
        p.MoveTo(a); p.LineTo(b); p.LineTo(d);
        p.Stroke();
    }

    private static float EaseOut(float x) => 1f - Mathf.Pow(1f - x, 3f);

    private static float OvershootOut(float x)
    {
        const float s = 1.70158f;
        x -= 1f;
        return 1f + (s + 1f) * x * x * x + s * x * x;
    }
}
