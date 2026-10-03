using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI cảm ứng tạo runtime: joystick di chuyển (nổi, xuất hiện tại điểm chạm
/// trong nửa trái màn hình), vùng vuốt phải để nhìn, và nút Nhảy góc phải dưới.
/// Đọc Input.touch trực tiếp (không qua UI EventSystem) để chạy được cả trên
/// emulator không phát PointerEvent.
/// </summary>
public class MobileControlsOverlay : MonoBehaviour
{
    public static bool IsAvailable { get; private set; }
    public static Vector2 Move { get; private set; }

    // true = joystick nổi (xuất hiện tại điểm chạm), false = cố định góc trái dưới.
    // Đổi qua SettingsManager.SetFloatingJoystickEnabled().
    public static bool FloatingJoystickEnabled { get; private set; } = true;

    private static readonly Vector2 FixedStickAnchoredPos = new Vector2(170, 170);
    private static MobileControlsOverlay activeOverlay;
    private RectTransform jumpButton;

    private static Vector2 lookDelta;
    private static bool jumpPressed;
    private static bool gameplayInputEnabled = true;
    public static bool IsGameplayInputEnabled => gameplayInputEnabled;

    // Nhúm 2 ngón trên nửa phải = zoom camera (dãn = lại gần).
    // True trong frame đang pinch để PlayerInteraction hủy tap tương tác.
    public static bool PinchActive { get; private set; }
    private static float pinchAccum;

    [Header("Joystick")]
    public float stickRadius = 90f;

    private RectTransform stickRoot;
    private RectTransform knob;

    private int moveFingerId = -1;
    private bool moveHasDragged;
    private int lookFingerId = -1;
    private int jumpFingerId = -1;
    private int pinchIdA = -1;
    private int pinchIdB = -1;
    private float prevPinchDist;
    private bool prevPinchValid;
    private Vector2 previousCanvasSize;
    private Vector2 jumpStart;
    private float jumpTouchStartTime;

    public static Vector2 ConsumeLookDelta()
    {
        Vector2 result = lookDelta;
        lookDelta = Vector2.zero;
        return result;
    }

    public static bool ConsumeJumpPressed()
    {
        bool result = jumpPressed;
        jumpPressed = false;
        return result;
    }

    // Độ dãn 2 ngón từ frame trước (pixel, dương = dãn ra). Camera đọc để zoom.
    public static float ConsumePinchDelta()
    {
        float result = pinchAccum;
        pinchAccum = 0f;
        return result;
    }

    // Vùng dành cho joystick (nửa trái) và nút Nhảy (góc phải dưới), dùng chung
    // bởi PlayerInteraction để tap tương tác không đè lên 2 vùng này.
    public static bool IsInControlZone(Vector2 screenPos)
    {
        bool inMoveZone = screenPos.x < Screen.width * 0.5f;
        bool inJumpZone = IsInJumpZone(screenPos);
        return inMoveZone || inJumpZone;
    }

    public static bool IsInJumpZone(Vector2 screenPos)
    {
        if (activeOverlay == null || activeOverlay.jumpButton == null || !activeOverlay.jumpButton.gameObject.activeInHierarchy) return false;
        Canvas canvas = activeOverlay.jumpButton.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(activeOverlay.jumpButton, screenPos, camera);
    }

    public void SetGameplayInputEnabled(bool enabled)
    {
        gameplayInputEnabled = enabled;
        if (!enabled)
        {
            Move = Vector2.zero;
            lookDelta = Vector2.zero;
            jumpPressed = false;
            pinchAccum = 0f;
            PinchActive = false;
            moveFingerId = -1;
            lookFingerId = -1;
            jumpFingerId = -1;
            pinchIdA = -1;
            pinchIdB = -1;
            prevPinchValid = false;
            HideStick();
        }
    }

    // Gọi từ SettingsManager khi người dùng đổi toggle "Joystick nổi"
    public static void SetFloatingJoystickEnabled(bool enabled)
    {
        FloatingJoystickEnabled = enabled;
    }

    public static MobileControlsOverlay FindOrCreate()
    {
        MobileControlsOverlay existing = FindAnyObjectByType<MobileControlsOverlay>();
        if (existing != null) return existing;

        GameObject root = new GameObject("Mobile Controls Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MobileControlsOverlay));
        Canvas c = root.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 1000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        MobileControlsOverlay overlay = root.GetComponent<MobileControlsOverlay>();
        overlay.CreateControls();
        return overlay;
    }

    private void Awake()
    {
        IsAvailable = true;
    }

    private void OnEnable()
    {
        activeOverlay = this;
    }

    private void OnDisable()
    {
        if (activeOverlay == this) activeOverlay = null;
        bool previousInput = gameplayInputEnabled;
        SetGameplayInputEnabled(false);
        gameplayInputEnabled = previousInput;
    }

    private void OnDestroy()
    {
        IsAvailable = false;
        Move = Vector2.zero;
        lookDelta = Vector2.zero;
        jumpPressed = false;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        if (!visible) Move = Vector2.zero;
    }
    private void Update()
    {
        Vector2 canvasSize = ((RectTransform)transform).rect.size;
        if (canvasSize != previousCanvasSize)
        {
            previousCanvasSize = canvasSize;
            moveFingerId = -1;
            Move = Vector2.zero;
            HideStick();
        }
        if (!PlatformHelper.IsTouchDevice() || !gameplayInputEnabled) return;

        // Quét trước: 2 ngón cùng lúc trên nửa phải (trừ vùng Nhảy) = nhúm zoom.
        // Hai ngón này không xoay camera trong lúc pinch.
        UpdatePinchState();

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            bool isJumpArea = IsInJumpZone(touch.position);

            if (touch.phase == TouchPhase.Began)
            {
                if (isJumpArea && jumpFingerId < 0)
                {
                    jumpFingerId = touch.fingerId;
                    jumpStart = touch.position;
                    jumpTouchStartTime = Time.unscaledTime;
                }
                else if (touch.position.x < Screen.width * 0.5f && moveFingerId < 0)
                {
                    moveFingerId = touch.fingerId;
                    ShowStickAt(FloatingJoystickEnabled ? touch.position : (Vector2?)null);
                }
                else if (lookFingerId < 0)
                {
                    lookFingerId = touch.fingerId;
                }
            }

            if (touch.fingerId == moveFingerId)
            {
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    moveFingerId = -1;
                    Move = Vector2.zero;
                    HideStick();
                }
                else
                {
                    UpdateJoystickTouch(touch.position, touch.phase);
                }
            }
            else if (touch.fingerId == lookFingerId)
            {
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    lookFingerId = -1;
                else if (touch.phase == TouchPhase.Moved && !IsPinchFinger(touch.fingerId))
                    lookDelta += touch.deltaPosition;
            }
            else if (touch.fingerId == jumpFingerId &&
                     (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))
            {
                if (touch.phase == TouchPhase.Ended &&
                    Time.unscaledTime - jumpTouchStartTime <= 0.5f &&
                    (touch.position - jumpStart).sqrMagnitude <= 900f)
                    jumpPressed = true;
                jumpFingerId = -1;
            }
        }
    }

    private bool IsPinchFinger(int fingerId)
    {
        return PinchActive && (fingerId == pinchIdA || fingerId == pinchIdB);
    }

    private void UpdatePinchState()
    {
        pinchIdA = -1;
        pinchIdB = -1;
        PinchActive = false;

        float halfW = Screen.width * 0.5f;
        Vector2 posA = Vector2.zero;
        Vector2 posB = Vector2.zero;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
            if (t.fingerId == moveFingerId || t.fingerId == jumpFingerId) continue;
            if (IsInJumpZone(t.position)) continue;
            if (t.position.x < halfW) continue;

            if (pinchIdA < 0) { pinchIdA = t.fingerId; posA = t.position; }
            else if (pinchIdB < 0) { pinchIdB = t.fingerId; posB = t.position; }
        }

        if (pinchIdA >= 0 && pinchIdB >= 0)
        {
            PinchActive = true;
            float dist = Vector2.Distance(posA, posB);
            if (prevPinchValid)
            {
                pinchAccum += dist - prevPinchDist;
            }
            prevPinchDist = dist;
            prevPinchValid = true;
        }
        else
        {
            prevPinchValid = false;
        }
    }

    private void CreateControls()
    {
        stickRoot = CreatePanel("Move Joystick", new Vector2(280, 280), Vector2.zero, Vector2.zero, FixedStickAnchoredPos);
        stickRoot.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);
        CreateKnob();
        HideStick(); // Joystick nổi: ẩn tới khi có ngón tay chạm xuống

        jumpButton = CreatePanel("Jump Button", new Vector2(180, 180), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-150, 170));
        jumpButton.GetComponent<Image>().color = new Color(0.95f, 0.55f, 0.1f, 0.78f);
        CreateLabel(jumpButton, "NHẢY");
    }

    private RectTransform CreatePanel(string name, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        go.GetComponent<Image>().raycastTarget = false; // input đọc qua Input.touch trực tiếp
        return rect;
    }

    private void CreateKnob()
    {
        GameObject go = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(stickRoot, false);
        knob = go.GetComponent<RectTransform>();
        knob.sizeDelta = new Vector2(100, 100);
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.45f);
        go.GetComponent<Image>().raycastTarget = false;
    }

    private static void CreateLabel(Transform parent, string value)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        Text text = go.GetComponent<Text>();
        text.text = value;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.color = Color.white;
    }

    // screenPos == null -> dùng vị trí cố định góc trái dưới (khi tắt joystick nổi)
    private void ShowStickAt(Vector2? screenPos)
    {
        if (stickRoot == null) return;
        stickRoot.gameObject.SetActive(true);

        if (screenPos.HasValue)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screenPos.Value, InputCamera(), out Vector2 local);
            RectTransform canvasRect = (RectTransform)transform;
            Vector2 bottomLeftOrigin = local + canvasRect.rect.size * canvasRect.pivot;

            // Giữ joystick không tràn ra ngoài mép màn hình
            Vector2 half = stickRoot.rect.size * 0.5f;
            float maxX = canvasRect.rect.width * 0.5f - half.x;
            float maxY = canvasRect.rect.height - half.y;
            bottomLeftOrigin.x = maxX >= half.x ? Mathf.Clamp(bottomLeftOrigin.x, half.x, maxX) : canvasRect.rect.width * 0.25f;
            bottomLeftOrigin.y = maxY >= half.y ? Mathf.Clamp(bottomLeftOrigin.y, half.y, maxY) : canvasRect.rect.height * 0.5f;

            stickRoot.anchorMin = Vector2.zero;
            stickRoot.anchorMax = Vector2.zero;
            stickRoot.anchoredPosition = bottomLeftOrigin;
        }
        else
        {
            stickRoot.anchorMin = Vector2.zero;
            stickRoot.anchorMax = Vector2.zero;
            stickRoot.anchoredPosition = FixedStickAnchoredPos;
        }

        UpdateKnob(Vector2.zero);
    }

    private Camera InputCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private float JoystickRadius()
    {
        if (stickRoot == null) return 0f;
        Vector2 travel = stickRoot.rect.size * 0.5f - (knob != null ? knob.rect.size * 0.5f : Vector2.zero);
        return Mathf.Max(0f, Mathf.Min(stickRadius, Mathf.Min(travel.x, travel.y)));
    }

    private Vector2 CalculateJoystickMove(Vector2 screenPosition)
    {
        float radius = JoystickRadius();
        if (radius <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
            stickRoot, screenPosition, InputCamera(), out Vector2 local)) return Vector2.zero;
        // Displayed center after clamp and the radius both use stick-local units.
        return Vector2.ClampMagnitude((local - stickRoot.rect.center) / radius, 1f);
    }

    private void UpdateJoystickTouch(Vector2 screenPosition, TouchPhase phase)
    {
        if (phase == TouchPhase.Began) moveHasDragged = false;
        if (phase == TouchPhase.Moved) moveHasDragged = true;
        // Clamping the floating base away from the initial touch must not start
        // locomotion by itself. Once dragged, use the actual visible center.
        Move = moveHasDragged ? CalculateJoystickMove(screenPosition) : Vector2.zero;
        UpdateKnob(Move);
    }

    private void HideStick()
    {
        if (stickRoot == null) return;
        if (FloatingJoystickEnabled)
        {
            stickRoot.gameObject.SetActive(false);
        }
        else
        {
            stickRoot.gameObject.SetActive(true);
            stickRoot.anchoredPosition = FixedStickAnchoredPos;
        }
        UpdateKnob(Vector2.zero);
    }

    private void UpdateKnob(Vector2 normalizedMove)
    {
        if (knob == null || stickRoot == null) return;
        knob.anchoredPosition = normalizedMove * JoystickRadius();
    }
}
