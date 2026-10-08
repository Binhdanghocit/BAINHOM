using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
#endif

// Use one backend when Both is enabled; Android builds can use Input System only.
public static class GameplayInput
{
    public readonly struct TouchSample
    {
        public readonly int fingerId, tapCount;
        public readonly Vector2 position, deltaPosition;
        public readonly UnityEngine.TouchPhase phase;
        public TouchSample(int id, Vector2 position, Vector2 delta, UnityEngine.TouchPhase phase, int taps)
        { fingerId = id; this.position = position; deltaPosition = delta; this.phase = phase; tapCount = taps; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
#if ENABLE_INPUT_SYSTEM
        if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static UnityEngine.InputSystem.Controls.KeyControl Control(KeyCode code)
    {
        if (Keyboard.current == null) return null;
        if (code >= KeyCode.A && code <= KeyCode.Z)
            return Keyboard.current[(Key)((int)Key.A + (int)code - (int)KeyCode.A)];
        switch (code)
        {
            case KeyCode.Space: return Keyboard.current.spaceKey;
            case KeyCode.Escape: return Keyboard.current.escapeKey;
            case KeyCode.LeftAlt: return Keyboard.current.leftAltKey;
            case KeyCode.LeftShift: return Keyboard.current.leftShiftKey;
            case KeyCode.RightShift: return Keyboard.current.rightShiftKey;
            case KeyCode.UpArrow: return Keyboard.current.upArrowKey;
            case KeyCode.DownArrow: return Keyboard.current.downArrowKey;
            case KeyCode.LeftArrow: return Keyboard.current.leftArrowKey;
            case KeyCode.RightArrow: return Keyboard.current.rightArrowKey;
            case KeyCode.Return: return Keyboard.current.enterKey;
            default:
                string name = code.ToString().Replace("Control", "Ctrl");
                if (name.StartsWith("Alpha")) name = "Digit" + name.Substring(5);
                if (name.StartsWith("Keypad")) name = "Numpad" + name.Substring(6);
                return System.Enum.TryParse(name, out Key key) && key != Key.None ? Keyboard.current[key] : null;
        }
    }
#endif
    public static bool GetKey(KeyCode code)
    {
#if ENABLE_INPUT_SYSTEM
        return Control(code)?.isPressed ?? false;
#else
        return Input.GetKey(code);
#endif
    }
    public static bool GetKeyDown(KeyCode code)
    {
#if ENABLE_INPUT_SYSTEM
        return Control(code)?.wasPressedThisFrame ?? false;
#else
        return Input.GetKeyDown(code);
#endif
    }
    public static float GetAxisRaw(string axis)
    {
#if ENABLE_INPUT_SYSTEM
        if (axis == "Horizontal") return (GetKey(KeyCode.D) || GetKey(KeyCode.RightArrow) ? 1 : 0)
            - (GetKey(KeyCode.A) || GetKey(KeyCode.LeftArrow) ? 1 : 0);
        if (axis == "Vertical") return (GetKey(KeyCode.W) || GetKey(KeyCode.UpArrow) ? 1 : 0)
            - (GetKey(KeyCode.S) || GetKey(KeyCode.DownArrow) ? 1 : 0);
        return 0;
#else
        return Input.GetAxisRaw(axis);
#endif
    }
    public static float GetAxis(string axis)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return 0;
        if (axis == "Mouse X") return Mouse.current.delta.ReadValue().x * 0.1f;
        if (axis == "Mouse Y") return Mouse.current.delta.ReadValue().y * 0.1f;
        if (axis == "Mouse ScrollWheel")
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            // Input System 1.20 normalizes native wheel ticks to 1 by default.
            // Only the optional platform-specific Windows range uses 120.
            if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange
                && (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor))
                scroll /= 120f;
            return scroll * 0.1f;
        }
        return GetAxisRaw(axis);
#else
        return Input.GetAxis(axis);
#endif
    }
    public static bool GetButtonDown(string button)
    {
#if ENABLE_INPUT_SYSTEM
        return button == "Jump" && GetKeyDown(KeyCode.Space);
#else
        return Input.GetButtonDown(button);
#endif
    }
    public static bool GetMouseButtonDown(int button)
    {
#if ENABLE_INPUT_SYSTEM
        return button == 0 && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(button);
#endif
    }
    public static Vector2 mousePosition
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }
    }
    public static bool touchSupported
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Touchscreen.current != null;
#else
            return Input.touchSupported;
#endif
        }
    }
    public static int touchCount
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            Initialize();
            return UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count;
#else
            return Input.touchCount;
#endif
        }
    }
    public static TouchSample GetTouch(int index)
    {
#if ENABLE_INPUT_SYSTEM
        var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[index];
        UnityEngine.TouchPhase phase;
        switch (touch.phase)
        {
            case UnityEngine.InputSystem.TouchPhase.Began: phase = UnityEngine.TouchPhase.Began; break;
            case UnityEngine.InputSystem.TouchPhase.Moved: phase = UnityEngine.TouchPhase.Moved; break;
            case UnityEngine.InputSystem.TouchPhase.Ended: phase = UnityEngine.TouchPhase.Ended; break;
            case UnityEngine.InputSystem.TouchPhase.Canceled: phase = UnityEngine.TouchPhase.Canceled; break;
            default: phase = UnityEngine.TouchPhase.Stationary; break;
        }
        return new TouchSample(touch.touchId, touch.screenPosition, touch.delta, phase, touch.tapCount);
#else
        var touch = Input.GetTouch(index);
        return new TouchSample(touch.fingerId, touch.position, touch.deltaPosition, touch.phase, touch.tapCount);
#endif
    }

    public static bool IsOverUI(Vector2 position)
    {
        if (EventSystem.current == null) return false;
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, results);
        foreach (var result in results)
            if (result.module is GraphicRaycaster) return true;
        return false;
    }
}
