using UnityEngine;
using UnityEngine.EventSystems;

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

    private bool isPlayerNear = false;
    private InteractableOutline outline;
    private int lastToggleFrame = -1;

    // Property để các script khác kiểm tra trạng thái (PaintingTrigger, NPCInteractable, Crosshair...)
    public bool IsMinigameOpen { get; private set; }
    public static bool IsAnyOpen { get; private set; }

    private void Awake()
    {
        if (GetComponent<VRUIInputBridge>() == null)
        {
            gameObject.AddComponent<VRUIInputBridge>();
        }
    }

    private void Start()
    {
        outline = GetComponent<InteractableOutline>();
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }
        if (minigameUI != null) minigameUI.SetActive(false);
        IsMinigameOpen = false;
        IsAnyOpen = false;
    }

    private void OnDisable()
    {
        if (IsMinigameOpen)
        {
            IsMinigameOpen = false;
            IsAnyOpen = false;
            if (playerController != null) playerController.enabled = true;
        }
    }

    private void Update()
    {
        if (!isPlayerNear) return;

        bool pressed = Input.GetKeyDown(KeyCode.E) || HandTriggerInput.WasPressedThisFrame();
        if (!pressed) return;

        ToggleMinigame();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            isPlayerNear = true;
            if (outline != null) outline.SetProximity(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            isPlayerNear = false;
            if (outline != null) outline.SetProximity(false);

            // Tự đóng minigame khi đi ra xa
            CloseMinigame();
        }
    }

    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!isPlayerNear || IsMinigameOpen) return;

        ToggleMinigame();
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
        if (minigameUI == null) return;
        lastToggleFrame = Time.frameCount;

        // Đóng các UI khác đang mở tránh xung đột
        if (PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsPopupOpen)
            PaintingUIManager.Instance.ClosePopup();
        if (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking)
            DialogueUIManager.Instance.EndDialogue();

        minigameUI.SetActive(true);
        IsMinigameOpen = true;
        IsAnyOpen = true;

        // Khoá di chuyển nhân vật & tắt joystick ảo trên Mobile khi đang chơi
        if (playerController != null)
            playerController.enabled = false;

        MobileControlsOverlay controls = FindAnyObjectByType<MobileControlsOverlay>();
        if (controls != null)
            controls.SetGameplayInputEnabled(false);

        // Hiện chuột để tô màu
        PlatformHelper.SetCursorLocked(false);
    }

    public void CloseMinigame()
    {
        if (minigameUI == null || !minigameUI.activeSelf) return;
        lastToggleFrame = Time.frameCount;

        minigameUI.SetActive(false);
        IsMinigameOpen = false;
        IsAnyOpen = false;

        // Mở lại di chuyển nhân vật & bật lại joystick ảo trên Mobile
        if (playerController != null)
            playerController.enabled = true;

        MobileControlsOverlay controls = FindAnyObjectByType<MobileControlsOverlay>();
        if (controls != null)
            controls.SetGameplayInputEnabled(true);

        // Khoá chuột lại để điều khiển nhân vật
        PlatformHelper.SetCursorLocked(true);
    }
}
