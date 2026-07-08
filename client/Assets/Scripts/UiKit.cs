using System;
using UnityEngine;
using UnityEngine.UIElements;

// A small UI Toolkit component kit with the redesign's palette baked in, styled inline (the path the
// render probe proved works). Screens build their trees from these so the chess.com-style look stays
// consistent without a stylesheet asset. Can be lifted into USS later without changing call sites.
public static class UiKit
{
    // Palette (our own hex values, chess.com-faithful).
    public static readonly Color Bg = Hex(0x31, 0x2e, 0x2b);
    public static readonly Color Rail = Hex(0x26, 0x24, 0x21);
    public static readonly Color Panel = Hex(0x27, 0x25, 0x22);
    public static readonly Color Panel2 = Hex(0x3a, 0x37, 0x33);
    public static readonly Color Panel3 = Hex(0x21, 0x20, 0x1d);
    public static readonly Color Text = Color.white;
    public static readonly Color Dim = Hex(0xb4, 0xb0, 0xab);
    public static readonly Color Mute = Hex(0x8b, 0x87, 0x83);
    public static readonly Color Green = Hex(0x81, 0xb6, 0x4c);
    public static readonly Color GreenHi = Hex(0x95, 0xc1, 0x5c);
    public static readonly Color GreenDeep = Hex(0x45, 0x75, 0x3c);
    public static readonly Color Gold = Hex(0xf0, 0xc3, 0x3c);
    public static readonly Color Danger = Hex(0xc9, 0x4b, 0x3b);
    public static readonly Color Line = new(1f, 1f, 1f, 0.08f);

    public static Color Hex(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);

    public static VisualElement Col(params VisualElement[] kids)
    {
        var e = new VisualElement();
        e.style.flexDirection = FlexDirection.Column;
        foreach (var k in kids) e.Add(k);
        return e;
    }

    public static VisualElement Row(params VisualElement[] kids)
    {
        var e = new VisualElement();
        e.style.flexDirection = FlexDirection.Row;
        e.style.alignItems = Align.Center;
        foreach (var k in kids) e.Add(k);
        return e;
    }

    public static Label Text_(string s, int size, Color color, bool bold = false)
    {
        var l = new Label(s);
        l.style.color = color;
        l.style.fontSize = size;
        l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        l.style.whiteSpace = WhiteSpace.Normal;
        return l;
    }

    public static void Pad(VisualElement e, float t, float r, float b, float l)
    {
        e.style.paddingTop = t; e.style.paddingRight = r; e.style.paddingBottom = b; e.style.paddingLeft = l;
    }

    public static void Pad(VisualElement e, float p) => Pad(e, p, p, p, p);

    public static void Margin(VisualElement e, float t, float r, float b, float l)
    {
        e.style.marginTop = t; e.style.marginRight = r; e.style.marginBottom = b; e.style.marginLeft = l;
    }

    public static void Radius(VisualElement e, float r)
    {
        e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
        e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
    }

    public static void NoBorder(VisualElement e)
    {
        e.style.borderTopWidth = 0; e.style.borderBottomWidth = 0;
        e.style.borderLeftWidth = 0; e.style.borderRightWidth = 0;
    }

    // A green primary button with the raised look faked via a darker bottom border (USS has no box-shadow).
    public static Button Primary(string label, Action onClick, int size = 15)
    {
        var b = new Button(onClick) { text = label };
        NoBorder(b);
        b.style.backgroundColor = Green;
        b.style.color = Text;
        b.style.fontSize = size;
        b.style.unityFontStyleAndWeight = FontStyle.Bold;
        Pad(b, 12, 18, 12, 18);
        Radius(b, 8);
        b.style.borderBottomWidth = 3;
        b.style.borderBottomColor = GreenDeep;
        b.style.marginTop = 0; b.style.marginBottom = 0; b.style.marginLeft = 0; b.style.marginRight = 0;
        b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = GreenHi);
        b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = Green);
        return b;
    }

    public static Button Ghost(string label, Action onClick, int size = 14)
    {
        var b = new Button(onClick) { text = label };
        NoBorder(b);
        b.style.backgroundColor = Panel2;
        b.style.color = Text;
        b.style.fontSize = size;
        b.style.unityFontStyleAndWeight = FontStyle.Bold;
        Pad(b, 10, 14, 10, 14);
        Radius(b, 8);
        b.style.marginTop = 0; b.style.marginBottom = 0; b.style.marginLeft = 0; b.style.marginRight = 0;
        b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = Hex(0x45, 0x42, 0x39));
        b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = Panel2);
        return b;
    }

    // A rounded chip (streak / due count) with a colored dot.
    public static VisualElement Chip(string label, Color dot)
    {
        var d = new VisualElement();
        d.style.width = 8; d.style.height = 8; Radius(d, 4); d.style.backgroundColor = dot; d.style.marginRight = 8;
        var t = Text_(label, 13, Text, true);
        var c = Row(d, t);
        c.style.backgroundColor = Panel;
        c.style.borderTopWidth = 1; c.style.borderBottomWidth = 1; c.style.borderLeftWidth = 1; c.style.borderRightWidth = 1;
        c.style.borderTopColor = c.style.borderBottomColor = c.style.borderLeftColor = c.style.borderRightColor = Line;
        Pad(c, 8, 12, 8, 12); Radius(c, 20); c.style.marginLeft = 8;
        return c;
    }
}
