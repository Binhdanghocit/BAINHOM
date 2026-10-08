using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerInteraction : MonoBehaviour
{
    public float interactDistance = 3.5f;

    [Header("Tap cảm ứng (điện thoại)")]
    [Tooltip("Tap nhanh + ít di chuyển = tương tác (giống click chuột)")]
    public float tapMaxDuration = 0.35f;
    public float tapMaxMovePx = 25f;

    private int tapFingerId = -1;
    private float tapStartTime;
    private Vector2 tapStartPos;
    private SettingsManager settingsManager;
    private static int lastInteractionFrame = -1;

    private void Start()
    {
        settingsManager = FindAnyObjectByType<SettingsManager>();
    }

    private void Update()
    {
        if (GameplayInput.GetKeyDown(KeyCode.E) || (HandTriggerInput.WasPressedThisFrame()
            && !VRUIInputBridge.ConsumedPressThisFrame && !IsBlockingUIOpen()))
            HandlePrimaryInteraction();
        if (GameplayInput.GetMouseButtonDown(0) && !PlatformHelper.IsXRDisplayRunning())
        {
            if (Cursor.lockState == CursorLockMode.Locked) TryInteract();
            else TryInteractAtScreenPoint(GameplayInput.mousePosition);
        }
        HandleTouchTap();
    }

    private void HandlePrimaryInteraction()
    {
        if (lastInteractionFrame == Time.frameCount) return;
        lastInteractionFrame = Time.frameCount;
        if (settingsManager != null && settingsManager.IsSettingsOpen() || ExitToExteriorUI.IsAnyOpen) return;

        // Consume the press when closing/advancing a modal so it cannot open another target.
        if (PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsPopupOpen)
        {
            PaintingUIManager.Instance.ClosePopup();
            return;
        }
        if (DoorMenuTrigger.IsAnyOpen)
        {
            foreach (var door in FindObjectsByType<DoorMenuTrigger>())
            {
                if (!door.IsOpen) continue;
                door.CloseMinigame();
                door.StayInGallery();
                return;
            }
            return;
        }
        if (MinigameTrigger.IsAnyOpen)
        {
            foreach (var workshop in FindObjectsByType<MinigameTrigger>())
                if (workshop.IsMinigameOpen) { workshop.CloseMinigame(); return; }
            return;
        }
        if (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking)
        {
            DialogueUIManager.Instance.AdvanceLine();
            return;
        }
        TryInteractAim();
    }

    // Gọi tay từ UnityEvent của một UI mobile tự thiết kế (nếu có).
    public void Interact()
    {
        TryInteract();
    }

    private bool IsBlockingUIOpen()
    {
        if (settingsManager != null && settingsManager.IsSettingsOpen()) return true;
        if (PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsPopupOpen) return true;
        if (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking) return true;
        if (MinigameTrigger.IsAnyOpen) return true;
        if (DoorMenuTrigger.IsAnyOpen) return true;
        if (ExitToExteriorUI.IsAnyOpen) return true;
        return false;
    }

    // Tap 1 ngón nhanh trên điện thoại = click tương tác.
    // Vuốt dài (xoay/joystick) và chạm trên UI tự bị loại.
    private void HandleTouchTap()
    {
        // Đang nhúm 2 ngón zoom: hủy tap tương tác, không mở popup nhầm
        if (MobileControlsOverlay.PinchActive)
        {
            tapFingerId = -1;
            return;
        }

        // Bảng UI đang mở: tap chỉ dành cho UI, không mở đè popup tranh/NPC/cửa
        if (IsBlockingUIOpen())
        {
            tapFingerId = -1;
            return;
        }

        if (GameplayInput.touchCount == 0)
        {
            tapFingerId = -1;
            return;
        }

        for (int i = 0; i < GameplayInput.touchCount; i++)
        {
            GameplayInput.TouchSample t = GameplayInput.GetTouch(i);

            if (t.phase == TouchPhase.Began)
            {
                if (tapFingerId >= 0) continue;
                if (GameplayInput.IsOverUI(t.position)) continue;
                if (MobileControlsOverlay.IsInControlZone(t.position)) continue; // nửa trái (joystick) + góc phải dưới (Nhảy)
                tapFingerId = t.fingerId;
                tapStartTime = Time.unscaledTime;
                tapStartPos = t.position;
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                if (t.fingerId != tapFingerId) continue;
                tapFingerId = -1;
                if (t.phase != TouchPhase.Ended) continue;
                if (t.tapCount > 1) continue; // chỉ tap 1 ngón mới tương tác
                if (Time.unscaledTime - tapStartTime > tapMaxDuration) continue;
                if ((t.position - tapStartPos).magnitude > tapMaxMovePx) continue;

                TryInteractAtScreenPoint(t.position);
                break;
            }
        }
    }

    public void TryInteract()
    {
        if (lastInteractionFrame == Time.frameCount) return;
        lastInteractionFrame = Time.frameCount;
        TryInteractAim();
    }

    private bool TryInteractAim()
    {
        if (IsBlockingUIOpen()) return false;
        Camera cam = Camera.main;
        if (cam == null) return false;
        bool xr = PlatformHelper.IsXRDisplayRunning();
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        bool hasRay = !xr || VRUIInputBridge.TryGetControllerRay(out ray);
        if (!InteractionTargetResolver.TrySelect(ray, transform, interactDistance, xr, hasRay, out var target)) return false;
        target.Activate();
        return true;
    }

    private bool TryInteractFromCamera()
    {
        if (IsBlockingUIOpen()) return false;
        Camera cam = Camera.main;
        if (cam == null) return false;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (PlatformHelper.IsXRDisplayRunning() && !VRUIInputBridge.TryGetControllerRay(out ray)) return false;
        if (!InteractionTargetResolver.TryRay(ray, transform, interactDistance, out var target)) return false;
        target.Activate();
        return true;
    }

    private bool TryInteractNearby()
    {
        if (IsBlockingUIOpen() || PlatformHelper.IsXRDisplayRunning()) return false;
        Camera cam = Camera.main;
        if (cam == null || !InteractionTargetResolver.TryNearby(transform, cam.transform.forward,
            interactDistance, out var target)) return false;
        target.Activate();
        return true;
    }

    private void TryInteractAtScreenPoint(Vector2 position)
    {
        if (lastInteractionFrame == Time.frameCount || IsBlockingUIOpen() || GameplayInput.IsOverUI(position)) return;
        lastInteractionFrame = Time.frameCount;
        Camera cam = Camera.main;
        // A direct click/tap selects exactly what its ray hits, never a nearby fallback.
        if (cam != null && InteractionTargetResolver.TryRay(cam.ScreenPointToRay(position), transform,
            interactDistance, out var target)) target.Activate();
    }
}
