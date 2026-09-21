using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI cảm ứng được tạo một lần lúc chạy: joystick trái, vùng vuốt phải và nút
/// tương tác. Nó dùng EventSystem/Input System UI Module đang có trong scene;
/// PlayerController đọc các giá trị qua thuộc tính static bên dưới.
/// </summary>
public class MobileControlsOverlay : MonoBehaviour
{
    public static bool IsAvailable { get; private set; }
    public static Vector2 Move { get; private set; }
    private static Vector2 lookDelta;
    private static bool interactionPressed;
    private static bool gameplayInputEnabled = true;

    private int moveFingerId = -1;
    private int lookFingerId = -1;
    private int interactFingerId = -1;
    private Vector2 moveStart;
    private Vector2 interactStart;
    private float interactStartTime;

    public static Vector2 ConsumeLookDelta()
    {
        Vector2 result = lookDelta;
        lookDelta = Vector2.zero;
        return result;
    }

    public static bool ConsumeInteractionPressed()
    {
        bool result = interactionPressed;
        interactionPressed = false;
        return result;
    }

    public void SetGameplayInputEnabled(bool enabled)
    {
        gameplayInputEnabled = enabled;
        if (!enabled)
        {
            Move = Vector2.zero;
            lookDelta = Vector2.zero;
            interactionPressed = false;
            moveFingerId = -1;
            lookFingerId = -1;
            interactFingerId = -1;
        }
    }

    public static MobileControlsOverlay FindOrCreate()
    {
        MobileControlsOverlay existing = FindAnyObjectByType<MobileControlsOverlay>();
        if (existing != null) return existing;

        GameObject root = new GameObject("Mobile Controls Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MobileControlsOverlay));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
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

    private void OnDestroy()
    {
        IsAvailable = false;
        Move = Vector2.zero;
        lookDelta = Vector2.zero;
        interactionPressed = false;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        if (!visible) Move = Vector2.zero;
    }

    // Một số emulator Android không chuyển Touch thành PointerEvent của
    // InputSystemUIInputModule. Đọc Touch trực tiếp ở đây để joystick/nút vẫn
    // chạy; các callback UI bên dưới vẫn giữ cho thiết bị thật khi chúng hoạt động.
    private void Update()
    {
        if (!PlatformHelper.IsTouchDevice() || !gameplayInputEnabled) return;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            bool isInteractArea = touch.position.x >= Screen.width - 300f && touch.position.y <= 320f;

            if (touch.phase == TouchPhase.Began)
            {
                if (isInteractArea && interactFingerId < 0)
                {
                    interactFingerId = touch.fingerId;
                    interactStart = touch.position;
                    interactStartTime = Time.unscaledTime;
                }
                else if (touch.position.x < Screen.width * 0.5f && moveFingerId < 0)
                {
                    moveFingerId = touch.fingerId;
                    moveStart = touch.position;
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
                }
                else
                {
                    Vector2 offset = (touch.position - moveStart) / 90f;
                    Move = Vector2.ClampMagnitude(offset, 1f);
                }
            }
            else if (touch.fingerId == lookFingerId)
            {
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    lookFingerId = -1;
                else if (touch.phase == TouchPhase.Moved)
                    lookDelta += touch.deltaPosition;
            }
            else if (touch.fingerId == interactFingerId &&
                     (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))
            {
                if (touch.phase == TouchPhase.Ended &&
                    Time.unscaledTime - interactStartTime <= 0.5f &&
                    (touch.position - interactStart).sqrMagnitude <= 900f)
                    interactionPressed = true;
                interactFingerId = -1;
            }
        }
    }

    private void CreateControls()
    {
        VirtualStick stick = CreatePanel<VirtualStick>("Move Joystick", new Vector2(280, 280), new Vector2(0, 0), new Vector2(0, 0), new Vector2(170, 170));
        stick.background.color = new Color(1f, 1f, 1f, 0.18f);
        stick.CreateKnob();

        LookZone look = CreatePanel<LookZone>("Look Swipe Zone", Vector2.zero, new Vector2(0.5f, 0), new Vector2(1, 1), Vector2.zero);
        look.background.color = Color.clear;

        Button interact = CreatePanel<Button>("Interact Button", new Vector2(180, 180), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-150, 170));
        interact.GetComponent<Image>().color = new Color(0.12f, 0.5f, 0.95f, 0.78f);
        Text label = CreateLabel(interact.transform, "TƯƠNG TÁC");
        // Không dùng Button.onClick: emulator có thể không phát UI pointer event;
        // Update() phía trên nhận Touch trực tiếp cho cả joystick và nút này.
    }

    private T CreatePanel<T>(string name, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition) where T : Component
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(T));
        go.transform.SetParent(transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        // Điều khiển dùng Input.touch trực tiếp, vì vậy không được chặn raycast
        // của các popup, Slider và Button Settings nằm phía dưới.
        go.GetComponent<Image>().raycastTarget = false;
        return go.GetComponent<T>();
    }

    private static Text CreateLabel(Transform parent, string value)
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
        return text;
    }

    private class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Image background;
        private RectTransform rect;
        private RectTransform knob;

        private void Awake()
        {
            background = GetComponent<Image>();
            rect = GetComponent<RectTransform>();
        }

        public void CreateKnob()
        {
            GameObject go = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            knob = go.GetComponent<RectTransform>();
            knob.sizeDelta = new Vector2(100, 100);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.45f);
            go.GetComponent<Image>().raycastTarget = false;
        }

        public void OnPointerDown(PointerEventData eventData) => UpdateStick(eventData);
        public void OnDrag(PointerEventData eventData) => UpdateStick(eventData);
        public void OnPointerUp(PointerEventData eventData)
        {
            Move = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
        }

        private void UpdateStick(PointerEventData eventData)
        {
            if (rect == null) rect = GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local);
            Vector2 radius = rect.rect.size * 0.5f;
            Move = new Vector2(local.x / radius.x, local.y / radius.y);
            Move = Vector2.ClampMagnitude(Move, 1f);
            if (knob != null) knob.anchoredPosition = Move * (radius - knob.sizeDelta * 0.5f);
        }
    }

    private class LookZone : MonoBehaviour
    {
        public Image background;
        private void Awake() => background = GetComponent<Image>();
    }
}
