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
        if (Input.GetKeyDown(KeyCode.E) || (HandTriggerInput.WasPressedThisFrame()
            && !VRUIInputBridge.ConsumedPressThisFrame && !IsBlockingUIOpen()))
            HandlePrimaryInteraction();
        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked)
        {
            TryInteract();
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
            foreach (var door in FindObjectsByType<DoorMenuTrigger>(FindObjectsSortMode.None))
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
            foreach (var workshop in FindObjectsByType<MinigameTrigger>(FindObjectsSortMode.None))
                if (workshop.IsMinigameOpen) { workshop.CloseMinigame(); return; }
            return;
        }
        if (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking)
        {
            DialogueUIManager.Instance.AdvanceLine();
            return;
        }
        TryInteractFromCamera();
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

        if (Input.touchCount == 0)
        {
            tapFingerId = -1;
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);

            if (t.phase == TouchPhase.Began)
            {
                if (tapFingerId >= 0) continue;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId)) continue;
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
                if (t.tapCount != 1) continue; // chỉ tap 1 ngón mới tương tác
                if (Time.unscaledTime - tapStartTime > tapMaxDuration) continue;
                if ((t.position - tapStartPos).magnitude > tapMaxMovePx) continue;

                TryInteract();
                break;
            }
        }
    }

    public void TryInteract()
    {
        if (lastInteractionFrame == Time.frameCount) return;
        lastInteractionFrame = Time.frameCount;
        TryInteractFromCamera();
    }

    private void TryInteractFromCamera()
    {
        if (IsBlockingUIOpen()) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (PlatformHelper.IsXRDisplayRunning())
        {
            if (!VRUIInputBridge.TryGetControllerRay(out ray)) return;
        }

        // Doorways can use their serialized maxInteractDistance; other targets
        // retain this component's interactDistance. Sorting makes nearer walls
        // and targets block anything behind them.
        RaycastHit[] hits = Physics.RaycastAll(ray, DoorMenuTrigger.GetRaycastDistance(interactDistance), ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.collider.transform;
            if (PlayerDetector.IsPlayer(hit.collider) || hitTransform.IsChildOf(transform))
                continue;

            PaintingTrigger pTrigger = hit.collider.GetComponent<PaintingTrigger>();
            if (pTrigger == null) pTrigger = hit.collider.GetComponentInParent<PaintingTrigger>();
            if (pTrigger != null)
            {
                if (hit.distance <= interactDistance) pTrigger.ToggleInteract();
                return;
            }

            PaintingInfo painting = hit.collider.GetComponent<PaintingInfo>();
            if (painting == null) painting = hit.collider.GetComponentInParent<PaintingInfo>();
            if (painting == null) painting = hit.collider.GetComponentInChildren<PaintingInfo>();
            if (painting != null)
            {
                if (hit.distance <= interactDistance && PaintingUIManager.Instance != null)
                    PaintingUIManager.Instance.ShowPaintingInfo(painting);
                return;
            }

            DoorMenuTrigger door = hit.collider.GetComponent<DoorMenuTrigger>();
            if (door == null) door = hit.collider.GetComponentInParent<DoorMenuTrigger>();
            if (door != null)
            {
                door.TryInteractFromRay(ray);
                return;
            }

            if (DialogueUIManager.Instance != null)
            {
                NPCInteractable npc = hit.collider.GetComponent<NPCInteractable>();
                if (npc == null) npc = hit.collider.GetComponentInParent<NPCInteractable>();
                if (npc != null)
                {
                    if (!npc.IsInteractionCollider(hit.collider)) continue;
                    if (hit.distance <= interactDistance) npc.TriggerDialogue();
                    return;
                }
            }

            MinigameTrigger mg = hit.collider.GetComponent<MinigameTrigger>();
            if (mg == null) mg = hit.collider.GetComponentInParent<MinigameTrigger>();
            if (mg != null)
            {
                if (hit.distance <= interactDistance) mg.ToggleMinigame();
                return;
            }

            // Non-interactive triggers are volumes; solid colliders block LOS.
            if (!hit.collider.isTrigger) return;
        }
    }
}
