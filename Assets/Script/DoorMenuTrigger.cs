using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Gắn vào Cánh Cửa trong phòng triển lãm.
/// Lại gần bấm E / Click / Trigger VR sẽ mở Menu Cửa:
/// 1. Chơi Minigame (Mở giao diện tô màu, tạm khóa di chuyển nhân vật)
/// 2. Ở lại tham quan (Đóng menu, tiếp tục xem tranh)
/// 3. Về menu chính
/// </summary>
[RequireComponent(typeof(InteractableOutline))]
public class DoorMenuTrigger : MonoBehaviour
{
    [Header("Giao diện")]
    [Tooltip("Menu cửa: Workshop, tiếp tục tham quan và về menu chính")]
    public GameObject doorMenuUI;
    [Tooltip("Bảng giao diện Minigame Tô Màu")]
    public GameObject minigameUI;

    [Header("Khoảng cách tương tác tối đa")]
    public float maxInteractDistance = 4.5f;

    [Header("Scene Điều hướng")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Khóa di chuyển khi chơi Minigame")]
    [Tooltip("Kéo Player vào đây để khóa di chuyển lúc đang tô màu (để trống sẽ tự tìm)")]
    public MonoBehaviour playerController;
    private ThirdPersonCamera thirdPersonCamera;

    // Cho phép các hệ thống khác (Crosshair, MobileControls, PlayerInteraction) biết UI cửa đang mở.
    // FIX kẹt input: track theo PANEL đang bật, không theo instance. Hai DoorMenuTrigger
    // (cua + Glass Door.004) dùng chung một Panel_DoorMenu: mở bằng instance này rồi đóng
    // bằng instance kia (nút StayInGallery gắn cứng vào cua) vẫn dọn đúng một cờ duy nhất.
    // Track theo instance trước đây để lại cờ của instance còn lại -> IsAnyOpen kẹt true
    // vĩnh viễn -> TryResumeGameplayIfClear luôn false -> cursor mở, mobile tắt, mọi
    // raycast tương tác bị chặn và cửa không mở lại được.
    private static readonly System.Collections.Generic.HashSet<GameObject> openPanels =
        new System.Collections.Generic.HashSet<GameObject>();
    private static readonly HashSet<DoorMenuTrigger> activeInstances = new HashSet<DoorMenuTrigger>();
    public static bool IsAnyOpen => openPanels.Count > 0;

    public bool IsOpen =>
        (doorMenuUI != null && doorMenuUI.activeSelf) ||
        (minigameUI != null && minigameUI.activeSelf);

    private bool isPlayerNear = false;
    private readonly HashSet<Collider> playerCollidersInRange = new HashSet<Collider>();
    private InteractableOutline outline;
    private int lastToggleFrame = -1;
    private int lastInputFrame = -1;

    // BUG 2 fix: theo dõi minigame mở qua cửa để đồng bộ cờ MinigameTrigger.
    // borrowedTable = mượn MinigameTrigger ở bàn vẽ (cùng panel) -> ủy thác Open/Close.
    // externalMinigameOpen = panel riêng của cửa -> tự đăng ký cờ external.
    private MinigameTrigger borrowedTable;
    private bool externalMinigameOpen;

    private void OnEnable()
    {
        activeInstances.Add(this);
    }

    public static float GetRaycastDistance(float minimumDistance)
    {
        float result = minimumDistance;
        foreach (DoorMenuTrigger door in activeInstances)
        {
            if (door != null) result = Mathf.Max(result, door.maxInteractDistance);
        }
        return result;
    }

    private void Awake()
    {
        // Đảm bảo tay cầm VR bấm được UI Menu Cửa / Minigame
        VRUIInputBridge.EnsureInstance();

        // Tự động gắn relay chuyển tiếp click từ tất cả các mesh con (như Glass Door.004) lên cánh cửa chính
        Collider[] childColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in childColliders)
        {
            if (col.gameObject != this.gameObject && col.GetComponent<DoorChildRelay>() == null)
            {
                var relay = col.gameObject.AddComponent<DoorChildRelay>();
                relay.targetDoor = this;
            }
        }
    }

    private void Start()
    {
        outline = GetComponent<InteractableOutline>();
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }
        ResolveThirdPersonCamera();

        ResolveDoorMenuUI();

        if (doorMenuUI != null)
        {
            if (doorMenuUI.GetComponent<DoorMenuLayout>() == null)
                doorMenuUI.AddComponent<DoorMenuLayout>();
            WireButtonsAtRuntime();
            doorMenuUI.SetActive(false);
        }

        if (minigameUI != null) minigameUI.SetActive(false);
        SyncOpenState();
    }

    private void ResolveDoorMenuUI()
    {
        if (doorMenuUI != null) return;

        // 1. Tìm trong các con của cánh cửa (kể cả đang tắt SetActive = false)
        var allChildren = GetComponentsInChildren<Transform>(true);
        foreach (var t in allChildren)
        {
            if (t.name.Equals("Panel_DoorMenu", System.StringComparison.OrdinalIgnoreCase))
            {
                doorMenuUI = t.gameObject;
                Debug.Log("[DoorMenuTrigger] Đã tự động kết nối với Panel_DoorMenu ở đối tượng con!");
                return;
            }
        }

        // 2. Tìm trong toàn bộ Scene (kể cả đang ẩn)
        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in allObjects)
        {
            if (go.name.Equals("Panel_DoorMenu", System.StringComparison.OrdinalIgnoreCase) && go.scene.isLoaded)
            {
                doorMenuUI = go;
                Debug.Log("[DoorMenuTrigger] Đã tự động tìm thấy Panel_DoorMenu trong Scene!");
                return;
            }
        }
    }

    private void WireButtonsAtRuntime()
    {
        if (doorMenuUI == null) return;
        Button[] buttons = doorMenuUI.GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            string n = b.gameObject.name.ToLower();
            if (n.Contains("minigame") || n.Contains("workshop"))
            {
                WireRuntimeListenerIfNeeded(b, OpenMinigame, nameof(OpenMinigame));
            }
            else if (n.Equals("btn_o_lai") || n.Contains("o_lai") || n.Contains("stay") || n.Contains("o lai"))
            {
                WireRuntimeListenerIfNeeded(b, StayInGallery, nameof(StayInGallery));
            }
            else if (n.Contains("thoat") || n.Contains("quit") || n.Contains("exit"))
            {
                b.onClick.RemoveListener(ExitGame);
                WireRuntimeListenerIfNeeded(b, GoToMainMenu, nameof(GoToMainMenu));
            }
            else if (n.Contains("menu"))
            {
                WireRuntimeListenerIfNeeded(b, GoToMainMenu, nameof(GoToMainMenu));
            }
        }
    }

    private void WireRuntimeListenerIfNeeded(Button button, UnityEngine.Events.UnityAction action, string methodName)
    {
        // Keep scene-authored callbacks as the source of truth. A second door
        // component may resolve the same shared panel, so method-only matching
        // prevents it from adding a duplicate callback.
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentMethodName(i) == methodName
                || button.onClick.GetPersistentTarget(i) is DoorMenuTrigger)
                return;
        }
        if (!runtimeWiredButtons.Add(button)) return;
        button.onClick.AddListener(action);
    }

    private static readonly HashSet<Button> runtimeWiredButtons = new HashSet<Button>();

    private void OnDisable()
    {
        activeInstances.Remove(this);
        TrackPanel(doorMenuUI, false);
        TrackPanel(minigameUI, false);
        playerCollidersInRange.Clear();
        UpdateProximityState();
        if (borrowedTable != null)
        {
            borrowedTable.CloseMinigame();
            borrowedTable = null;
        }
        else if (minigameUI != null)
        {
            minigameUI.SetActive(false);
        }
        if (doorMenuUI != null) doorMenuUI.SetActive(false);
        if (externalMinigameOpen)
        {
            MinigameTrigger.SetExternalOpen(false);
            externalMinigameOpen = false;
        }
        SyncOpenState();
        ViewModeController.TryResumeGameplayIfClear();
    }



    private void ResolveThirdPersonCamera()
    {
        if (playerController is PlayerController controller && controller.cameraTransform != null)
            thirdPersonCamera = controller.cameraTransform.GetComponent<ThirdPersonCamera>();
        if (thirdPersonCamera == null && Camera.main != null)
            thirdPersonCamera = Camera.main.GetComponent<ThirdPersonCamera>();
    }

    public bool IsPlayerInRange()
    {
        // Prompt trigger không được nới lỏng giới hạn khoảng cách tương tác.
        Camera cam = Camera.main;
        if (cam != null && Vector3.Distance(transform.position, cam.transform.position) <= maxInteractDistance)
        {
            return true;
        }

        if (playerController != null && Vector3.Distance(transform.position, playerController.transform.position) <= maxInteractDistance)
        {
            return true;
        }

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            playerCollidersInRange.Add(other);
            UpdateProximityState();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            playerCollidersInRange.Remove(other);
            UpdateProximityState();

            // A collider exit only ends proximity after the last player collider leaves.
            if (isPlayerNear) return;

            // BUG 9 fix: chỉ đóng menu cửa, GIỮ minigame (giữ progress tô màu).
            // Minigame chỉ đóng khi bấm nút X / CloseMinigame().
            if (doorMenuUI != null) doorMenuUI.SetActive(false);
            SyncOpenState();

            // Minigame còn mở -> giữ khóa di chuyển + chuột mở để chơi tiếp.
            bool minigameStillOpen = minigameUI != null && minigameUI.activeSelf;
            if (!minigameStillOpen)
            {
                ViewModeController.TryResumeGameplayIfClear();
            }
        }
    }

    private void UpdateProximityState()
    {
        playerCollidersInRange.RemoveWhere(collider => collider == null);
        isPlayerNear = playerCollidersInRange.Count > 0;
        if (outline != null) outline.SetProximity(isPlayerNear);
    }

    private void OnMouseDown()
    {
        HandleDirectClick();
    }

    public void OnChildMouseDown()
    {
        HandleDirectClick();
    }

    private void HandleDirectClick()
    {
        // Không nhận click xuyên qua các bảng UI đang mở
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        Camera cam = Camera.main;
        if (cam != null)
            TryInteractFromRay(cam.ScreenPointToRay(Input.mousePosition));
    }

    public bool TryInteractFromRay(Ray ray)
    {
        if (lastInputFrame == Time.frameCount) return true;
        RaycastHit[] hits = Physics.RaycastAll(ray, maxInteractDistance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.collider.transform;
            if (PlayerDetector.IsPlayer(hit.collider))
                continue;

            DoorMenuTrigger hitDoor = hitTransform.GetComponent<DoorMenuTrigger>();
            if (hitDoor == null) hitDoor = hitTransform.GetComponentInParent<DoorMenuTrigger>();
            if (hitDoor != null)
            {
                if (hitDoor != this || hit.distance > maxInteractDistance) return false;
                if (!IsOpen && IsBlockingUIOpen()) return false;
                lastInputFrame = Time.frameCount;
                if (minigameUI != null && minigameUI.activeSelf) CloseMinigame();
                else ToggleDoorMenu();
                return true;
            }

            // Triggers without an interactable are volumes, not line-of-sight blockers.
            if (!hit.collider.isTrigger) return false;
        }
        return false;
    }

    private static bool IsBlockingUIOpen()
    {
        if (IsAnyOpen) return true;
        if (PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsPopupOpen) return true;
        if (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking) return true;
        if (MinigameTrigger.IsAnyOpen) return true;
        if (ExitToExteriorUI.IsAnyOpen) return true;
        SettingsManager settings = FindAnyObjectByType<SettingsManager>();
        return settings != null && settings.IsSettingsOpen();
    }

    public void ToggleDoorMenu()
    {
        // Chống double-toggle trong cùng 1 frame
        if (lastToggleFrame == Time.frameCount) return;

        // Nếu menu cửa đang mở thì luôn cho đóng (ưu tiên đóng).
        bool isCurrentlyOpen = doorMenuUI != null && doorMenuUI.activeSelf;
        if (!isCurrentlyOpen && IsOtherUIOpen())
        {
            Debug.Log("[DoorMenuTrigger] Có UI khác đang mở (tranh/minigame/hội thoại/cài đặt) nên không mở menu cửa.");
            return;
        }
        lastToggleFrame = Time.frameCount;

        if (doorMenuUI == null)
        {
            ResolveDoorMenuUI();
        }

        if (doorMenuUI == null)
        {
            Debug.LogError("[DoorMenuTrigger] ❌ Chưa tìm thấy 'Panel_DoorMenu' trong Scene! Hãy vào menu Tools > 'Tự Động Setup 3 Nút Menu Cánh Cửa' trong Unity để tạo.");
            return;
        }

        bool isNowActive = !doorMenuUI.activeSelf;
        doorMenuUI.SetActive(isNowActive);
        SyncOpenState();

        Debug.Log($"[DoorMenuTrigger] 👉 Trạng thái Menu Cửa hiện tại: {(isNowActive ? "BẬT (Mở)" : "TẮT (Đóng)")}");

        // Đóng menu -> khôi phục FULL gameplay (movement/camera/interaction/cursor/mobile)
        // qua luồng tập trung. Mở menu -> khóa gameplay để bấm nút.
        if (IsOpen)
        {
            ViewModeController.PauseGameplayForModal();
        }
        else
        {
            ViewModeController.TryResumeGameplayIfClear();
        }
    }

    private static void TrackPanel(GameObject panel, bool open)
    {
        if (panel == null) return;
        if (open) openPanels.Add(panel);
        else openPanels.Remove(panel);
        openPanels.RemoveWhere(go => go == null);
    }

    private void SyncOpenState()
    {
        TrackPanel(doorMenuUI, doorMenuUI != null && doorMenuUI.activeSelf);
        TrackPanel(minigameUI, minigameUI != null && minigameUI.activeSelf);
        MobileControlsOverlay controls = FindAnyObjectByType<MobileControlsOverlay>();
        if (controls != null)
        {
            controls.SetGameplayInputEnabled(!IsGameplayBlocked());
        }
    }

    private static bool IsGameplayBlocked()
    {
        if (IsAnyOpen || MinigameTrigger.IsAnyOpen) return true;
        if (PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsPopupOpen) return true;
        if (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking) return true;
        if (ExitToExteriorUI.IsAnyOpen) return true;
        SettingsManager settings = FindAnyObjectByType<SettingsManager>();
        return settings != null && settings.IsSettingsOpen();
    }

    // Chặn mở menu cửa đè lên UI khác (tranh / workshop / hội thoại / cài đặt).
    // PaintingTrigger/NPCInteractable đã guard chiều ngược lại, đây là chiều còn thiếu.
    private static bool IsOtherUIOpen()
    {
        if (IsAnyOpen) return true;
        if (PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsPopupOpen) return true;
        if (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking) return true;
        if (MinigameTrigger.IsAnyOpen) return true;
        if (ExitToExteriorUI.IsAnyOpen) return true;
        var settings = FindAnyObjectByType<SettingsManager>();
        if (settings != null && settings.IsSettingsOpen()) return true;
        return false;
    }

    // ============================================
    // CÁC HÀM GẮN VÀO BUTTON TRÊN MENU CÁNH CỬA
    // ============================================

    // 1. NÚT "CHƠI MINIGAME" (Mở bảng tô màu)
    public void OpenMinigame()
    {
        lastToggleFrame = Time.frameCount;

        // Đóng các UI khác đang mở tránh overlay chồng (giống MinigameTrigger)
        if (PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsPopupOpen)
            PaintingUIManager.Instance.ClosePopup();
        if (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking)
            DialogueUIManager.Instance.EndDialogue();

        // Fallback: nếu chưa kéo Minigame UI thì mượn tạm UI của bàn workshop
        // (MinigameTrigger ở bàn vẽ) để nút không chết.
        if (minigameUI == null)
        {
            var table = FindAnyObjectByType<MinigameTrigger>();
            if (table != null && table.minigameUI != null)
            {
                minigameUI = table.minigameUI;
                Debug.Log("[DoorMenuTrigger] Tự mượn Workshop UI từ MinigameTrigger: " + minigameUI.name);
            }
        }

        if (doorMenuUI != null) doorMenuUI.SetActive(false); // Ẩn menu cửa
        SyncOpenState();

        // BUG 2 fix: cùng panel với bàn vẽ -> ủy thác cho MinigameTrigger.OpenMinigame()
        // để cờ MinigameTrigger.IsAnyOpen đồng bộ (NPC/tranh guard đúng).
        var owner = FindAnyObjectByType<MinigameTrigger>();
        if (owner != null && owner.minigameUI != null && owner.minigameUI == minigameUI)
        {
            owner.OpenMinigame();
            borrowedTable = owner;
            SyncOpenState();
            return;
        }

        if (minigameUI != null)
        {
            minigameUI.SetActive(true); // Hiện bảng Minigame
            // Panel riêng của cửa -> tự đăng ký cờ external để guard NPC/tranh thấy.
            if (!externalMinigameOpen)
            {
                MinigameTrigger.SetExternalOpen(true);
                externalMinigameOpen = true;
            }
            SyncOpenState();

            ViewModeController.PauseGameplayForModal();
            // Khóa di chuyển nhân vật khi đang tô màu
            if (playerController != null)
                playerController.enabled = false;
            if (thirdPersonCamera != null)
                thirdPersonCamera.enabled = false;

            // Mở chuột để người chơi chọn màu và tô
            PlatformHelper.SetCursorLocked(false);
        }
        else
        {
            Debug.LogWarning("[DoorMenuTrigger] Chưa kéo bảng Minigame UI vào ô 'Minigame UI' của DoorMenuTrigger!");
        }
    }

    // 2. NÚT "Ở LẠI THAM QUAN" (Đóng menu cửa, tiếp tục xem tranh)
    public void StayInGallery()
    {
        lastToggleFrame = Time.frameCount;

        if (doorMenuUI != null) doorMenuUI.SetActive(false);
        SyncOpenState();

        // Giữ trigger/range hiện tại để E và click có thể mở lại menu từ vị trí này.
        ViewModeController.TryResumeGameplayIfClear();
    }

    // 3. NÚT "QUAY LẠI TRIỂN LÃM" (Gắn vào nút [X] hoặc nút Thoát Minigame)
    public void CloseMinigame()
    {
        lastToggleFrame = Time.frameCount;

        if (borrowedTable != null)
        {
            // Minigame do bàn vẽ quản lý -> ủy thác đóng để cờ đồng bộ.
            borrowedTable.CloseMinigame();
            borrowedTable = null;
        }
        else
        {
            if (minigameUI != null)
            {
                minigameUI.SetActive(false);
            }
            if (externalMinigameOpen)
            {
                MinigameTrigger.SetExternalOpen(false);
                externalMinigameOpen = false;
            }
        }
        SyncOpenState();
        ViewModeController.TryResumeGameplayIfClear();
    }

    // 4. NÚT "VỀ MENU CHÍNH"
    public void GoToMainMenu()
    {
        if (doorMenuUI != null) doorMenuUI.SetActive(false);
        if (minigameUI != null) minigameUI.SetActive(false);
        borrowedTable = null;
        if (externalMinigameOpen)
        {
            MinigameTrigger.SetExternalOpen(false);
            externalMinigameOpen = false;
        }
        SyncOpenState();

        // Ngắt thuyết minh tranh (AudioManager sống xuyên scene, không tắt ở
        // đây thì tiếng chạy tiếp sang MainMenu).
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopVoiceover();

        if (playerController != null) playerController.enabled = true;
        PlatformHelper.SetCursorLocked(false);

        if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else
        {
            Debug.LogError("[DoorMenuTrigger] Scene MainMenu không có trong Build Settings: " + mainMenuSceneName);
        }
    }

    // 5. NÚT "THOÁT GAME" (Thoát hẳn ứng dụng)
    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

/// <summary>
/// Component gắn tự động vào các phần tử con của cánh cửa để chuyển tiếp sự kiện click chuột lên script chính
/// </summary>
public class DoorChildRelay : MonoBehaviour
{
    public DoorMenuTrigger targetDoor;

    private void OnMouseDown()
    {
        if (targetDoor != null)
        {
            targetDoor.OnChildMouseDown();
        }
    }
}
