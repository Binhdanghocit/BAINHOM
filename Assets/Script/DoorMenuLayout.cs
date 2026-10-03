using UnityEngine;

// Keep the card inside the usable canvas without changing the VR canvas transform.
[DisallowMultipleComponent]
public class DoorMenuLayout : MonoBehaviour
{
    private Rect lastSafeArea;
    private Vector2 lastScreenSize;
    private Vector2 lastCanvasSize;
    private RenderMode lastRenderMode;

    private void OnEnable() => RefreshLayout();

    private void Update()
    {
        var canvas = GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        Vector2 size = new Vector2(Screen.width, Screen.height);
        if (lastSafeArea != Screen.safeArea || lastScreenSize != size
            || (canvas != null && lastRenderMode != canvas.renderMode)
            || (canvasRect != null && lastCanvasSize != canvasRect.rect.size))
            RefreshLayout();
    }

    public void RefreshLayout() => ApplyLayout(Screen.safeArea, new Vector2(Screen.width, Screen.height));

    public void ApplyLayout(Rect safeArea, Vector2 screenSize)
    {
        var panel = transform as RectTransform;
        var canvas = GetComponentInParent<Canvas>();
        var parent = panel != null ? panel.parent as RectTransform : null;
        if (panel == null || canvas == null || parent == null || screenSize.x <= 0 || screenSize.y <= 0) return;
        Vector2 canvasSize = parent.rect.size;
        if (canvasSize.x <= 0 || canvasSize.y <= 0) return;
        lastSafeArea = safeArea;
        lastScreenSize = screenSize;
        lastRenderMode = canvas.renderMode;
        var canvasRect = canvas.transform as RectTransform;
        lastCanvasSize = canvasRect != null ? canvasRect.rect.size : canvasSize;

        // Screen insets have no meaning on a world-space canvas positioned by the XR bridge.
        if (canvas.renderMode == RenderMode.WorldSpace)
        {
            screenSize = canvasSize;
            safeArea = new Rect(Vector2.zero, canvasSize);
        }
        else
        {
            if (safeArea.width <= 0 || safeArea.height <= 0) safeArea = new Rect(Vector2.zero, screenSize);
            safeArea.xMin = Mathf.Clamp(safeArea.xMin, 0, screenSize.x);
            safeArea.xMax = Mathf.Clamp(safeArea.xMax, safeArea.xMin, screenSize.x);
            safeArea.yMin = Mathf.Clamp(safeArea.yMin, 0, screenSize.y);
            safeArea.yMax = Mathf.Clamp(safeArea.yMax, safeArea.yMin, screenSize.y);
        }

        Vector2 usable = new Vector2(safeArea.width * canvasSize.x / screenSize.x,
                                     safeArea.height * canvasSize.y / screenSize.y);
        Vector2 padding = new Vector2(24 * canvasSize.x / screenSize.x, 24 * canvasSize.y / screenSize.y);
        float scale = Mathf.Min(1f, Mathf.Max(0.01f, (usable.x - padding.x * 2) / 560f),
                                   Mathf.Max(0.01f, (usable.y - padding.y * 2) / 390f));
        panel.anchorMin = panel.anchorMax = new Vector2(safeArea.center.x / screenSize.x, safeArea.center.y / screenSize.y);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition3D = Vector3.zero;
        panel.sizeDelta = new Vector2(560, 390);
        panel.localRotation = Quaternion.identity;
        panel.localScale = Vector3.one * scale;

        Place("Title_Text", 24, 48);
        Place("Subtitle_Text", 82, 32);
        Place("Btn_Minigame", 134, 64);
        Place("Btn_O_Lai", 214, 64);
        Place("Btn_Thoat", 294, 64);
    }

    private void Place(string name, float top, float height)
    {
        var rect = transform.Find(name) as RectTransform;
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition3D = new Vector3(0, -top, 0);
        rect.sizeDelta = new Vector2(496, height);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }
}
