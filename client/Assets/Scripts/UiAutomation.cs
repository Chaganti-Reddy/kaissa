using System.Linq;
using UnityEngine.UIElements;

// Test-only helpers that drive the UI the way a user would - by dispatching real events through the
// panel, not by calling controller methods. Used by the -kaissa-*test self-test harnesses so the
// automated runs exercise the actual click wiring (button Clickables, row ClickEvent handlers).
public static class UiAutomation
{
    // Activate an element as a click would. Buttons respond to NavigationSubmit (the same path Enter/A
    // triggers); other elements carry a registered ClickEvent handler, so send that.
    public static void Click(VisualElement el)
    {
        if (el == null) return;
        if (el is Button)
        {
            using var e = NavigationSubmitEvent.GetPooled();
            e.target = el;
            el.SendEvent(e);
        }
        else
        {
            using var e = ClickEvent.GetPooled();
            e.target = el;
            el.SendEvent(e);
        }
    }

    // Find a Button by its visible text (first match in document order).
    public static Button FindButton(VisualElement root, string text) =>
        root?.Query<Button>().ToList().FirstOrDefault(b => b.text == text);

    // Send a pointer-enter so hover styling (UiKit.Interactive pop, row tint) can be captured.
    public static void Hover(VisualElement el)
    {
        if (el == null) return;
        using var e = PointerEnterEvent.GetPooled();
        e.target = el;
        el.SendEvent(e);
    }
}
