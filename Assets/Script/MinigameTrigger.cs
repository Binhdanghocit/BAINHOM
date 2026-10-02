using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Gắn vào Bàn Vẽ / Góc Tô Màu trong triển lãm.
/// Lại gần + bấm [E] / Trigger VR / Tap màn hình → Mở Minigame.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(InteractableOutline))]
public class MinigameTrigger : MonoBehaviour
{
    [Header("Giao diện Minigame")]
    [Tooltip("Kéo Panel UI Minigame vào đây")]
    public GameObject minigameUI;

    [Header("Khoá di chuyển khi chơi")]
    [Tooltip("Kéo GameObject chứa PlayerController vào để khoá nhân vật lúc đang chơi minigame (để trống sẽ tự tìm)")]
    public MonoBehaviour playerController;

    private ThirdPersonCamera thirdPersonCamera;

    private bool isPlayerNear = false;
    private InteractableOutline outline;
    private int lastToggleFrame = -1;
    private readonly HashSet<Collider> playerCollidersInRange = new HashSet<Collider>();

    // Property để các script khác kiểm tra trạng thái (PaintingTrigger, NPCInteractable, Crosshair...)
    public bool IsMinigameOpen { get; private set; }

    // BUG 1 fix: static flag dạng HashSet — nhiều bàn vẽ cùng mở thì đóng 1 bàn
    // không clear oan cờ của bàn còn lại.
    private static readonly HashSet<MinigameTrigger> openInstances = new HashSet<MinigameTrigger>();
    // Đếm panel minigame do DoorMenuTrigger mở trực tiếp (panel riêng, không qua
    // instance MinigameTrigger nào) — BUG 2 fix.
    private static int externalOpenCount = 0;
    public static bool IsAnyOpen => openInstances.Count > 0 || externalOpenCount > 0;

    // DoorMenuTrigger gọi khi nó tự bật/tắt panel workshop riêng.
    public static void SetExternalOpen(bool open)
    {
        externalOpenCount += open ? 1 : -1;
        if (externalOpenCount < 0) externalOpenCount = 0;
    }

    private void Awake()
    {
        VRUIInputBridge.EnsureInstance();
    }

    private void Start()
    {
        outline = GetComponent<InteractableOutline>();
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }
        ResolveThirdPersonCamera();
        if (minigameUI != null) minigameUI.SetActive(false);
        IsMinigameOpen = false;
        openInstances.Remove(this);
    }

    private void OnDisable()
    {
        playerCollidersInRange.Clear();
        isPlayerNear = false;
        if (outline != null) outline.SetProximity(false);
        if (IsMinigameOpen || (minigameUI != null && minigameUI.activeSelf))
        {
            CloseMinigame();
        }
        else
        {
            openInstances.Remove(this);
        }
    }



    private void ResolveThirdPersonCamera()
    {
        if (playerController is PlayerController controller && controller.cameraTransform != null)
            thirdPersonCamera = controller.cameraTransform.GetComponent<ThirdPersonCamera>();
        if (thirdPersonCamera == null && Camera.main != null)
            thirdPersonCamera = Camera.main.GetComponent<ThirdPersonCamera>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            if (playerCollidersInRange.Add(other)) UpdateNearbyState();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            if (playerCollidersInRange.Remove(other)) UpdateNearbyState();
        }
    }

    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!isPlayerNear || IsMinigameOpen) return;

        ToggleMinigame();
    }

    private void UpdateNearbyState()
    {
        bool nearby = playerCollidersInRange.Count > 0;
        if (isPlayerNear == nearby) return;
        isPlayerNear = nearby;
        if (outline != null) outline.SetProximity(nearby);
        if (!nearby && IsMinigameOpen) CloseMinigame();
    }

    private void LateUpdate()
    {
        if (playerCollidersInRange.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy) > 0)
            UpdateNearbyState();
    }

    public void ToggleMinigame()
    {
        if (lastToggleFrame == Time.frameCount) return;
        lastToggleFrame = Time.frameCount;

        if (minigameUI != null && minigameUI.activeSelf)
        {
            CloseMinigame();
        }
        else
        {
            OpenMinigame();
        }
    }

    public void OpenMinigame()
    {
        if (minigameUI == null || IsMinigameOpen || IsOpeningBlocked()) return;
        lastToggleFrame = Time.frameCount;

        // Đóng các UI khác đang mở tránh xung đột
        if (PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsPopupOpen)
            PaintingUIManager.Instance.ClosePopup();
        if (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking)
            DialogueUIManager.Instance.EndDialogue();

        minigameUI.SetActive(true);
        IsMinigameOpen = true;
        openInstances.Add(this);
        ViewModeController.PauseGameplayForModal();

        // Khoá di chuyển nhân vật & tắt joystick ảo trên Mobile khi đang chơi
        if (playerController != null)
            playerController.enabled = false;
        if (thirdPersonCamera == null) ResolveThirdPersonCamera();
        if (thirdPersonCamera != null)
            thirdPersonCamera.enabled = false;

        MobileControlsOverlay controls = FindAnyObjectByType<MobileControlsOverlay>();
        if (controls != null)
            controls.SetGameplayInputEnabled(false);

        // Hiện chuột để tô màu
        PlatformHelper.SetCursorLocked(false);
    }

    private static bool IsOpeningBlocked()
    {
        SettingsManager settings = FindAnyObjectByType<SettingsManager>();
        return (settings != null && settings.IsSettingsOpen())
            || DoorMenuTrigger.IsAnyOpen || ExitToExteriorUI.IsAnyOpen || IsAnyOpen;
    }

    public void CloseMinigame()
    {
        openInstances.Remove(this);
        lastToggleFrame = Time.frameCount;

        if (minigameUI != null && minigameUI.activeSelf)
            minigameUI.SetActive(false);
        IsMinigameOpen = false;

        bool anotherUIOpen = IsAnyOpen || DoorMenuTrigger.IsAnyOpen
            || (PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsPopupOpen)
            || (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking)
            || ExitToExteriorUI.IsAnyOpen;
        SettingsManager settings = FindAnyObjectByType<SettingsManager>();
        if (settings != null && settings.IsSettingsOpen()) anotherUIOpen = true;

        // Mở lại di chuyển nhân vật & bật lại joystick ảo trên Mobile
        if (playerController != null)
            playerController.enabled = !anotherUIOpen;
        if (thirdPersonCamera != null)
            thirdPersonCamera.enabled = !anotherUIOpen;

        MobileControlsOverlay controls = FindAnyObjectByType<MobileControlsOverlay>();
        if (controls != null)
            controls.SetGameplayInputEnabled(!anotherUIOpen);

        // Chỉ khóa chuột lại khi không còn UI gameplay nào đang mở.
        if (!anotherUIOpen)
            ViewModeController.TryResumeGameplayIfClear();
    }
}
