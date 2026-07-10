using UnityEngine;
using UnityEngine.InputSystem;

// chess.com "Real 3D"-style camera for the 3D board: middle-drag to orbit/tilt, scroll to zoom. Left
// button moves pieces; right button draws annotations. Orbits around the board centre; flip snaps yaw.
public sealed class CameraOrbit3D : MonoBehaviour
{
    public static CameraOrbit3D Instance;

    private static readonly Vector3 Center = new(3.5f, 0f, 3.5f);
    private float _yaw = 180f;   // 180 = behind White
    private float _pitch = 64f;  // elevation - steeper for a cleaner, less-foreshortened view
    private float _dist = 10.5f;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void LateUpdate()
    {
        var m = Mouse.current;
        if (m != null)
        {
            if (m.middleButton.isPressed)
            {
                var d = m.delta.ReadValue();
                _yaw += d.x * 0.25f;
                _pitch = Mathf.Clamp(_pitch - d.y * 0.25f, 18f, 85f);
            }
            float sc = m.scroll.ReadValue().y;
            if (Mathf.Abs(sc) > 0.01f)
                _dist = Mathf.Clamp(_dist - sc * 0.005f, 5.5f, 15f);
        }
        Apply();
    }

    public void SetSide(bool whiteBottom)
    {
        _yaw = whiteBottom ? 180f : 0f;
        Apply();
    }

    private void Apply()
    {
        var cam = Camera.main;
        if (cam == null) return;
        float y = _yaw * Mathf.Deg2Rad, p = _pitch * Mathf.Deg2Rad;
        var offset = new Vector3(
            Mathf.Cos(p) * Mathf.Sin(y),
            Mathf.Sin(p),
            Mathf.Cos(p) * Mathf.Cos(y)) * _dist;
        cam.transform.position = Center + offset;
        cam.transform.LookAt(Center);
    }
}
