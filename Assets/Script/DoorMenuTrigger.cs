using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Gắn vào Cánh Cửa trong phòng triển lãm.
/// Lại gần bấm E / Click / Trigger VR sẽ mở Menu Cửa:
/// 1. Chơi Minigame (Mở giao diện tô màu, tạm khóa di chuyển nhân vật)
/// 2. Ở lại tham quan (Đóng menu, tiếp tục xem tranh)
/// 3. Về Menu / Thoát Game
/// </summary>
[RequireComponent(typeof(InteractableOutline))]
public class DoorMenuTrigger : MonoBehaviour
{
    [Header("Giao diện")]
    [Tooltip("Bảng Menu 3 nút của Cánh Cửa (Chơi Minigame, Ở lại, Thoát)")]
    public GameObject doorMenuUI;
    [Tooltip("Bảng giao diện Minigame Tô Màu")]
    public GameObject minigameUI;

    [Header("Scene Điều hướng")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Khóa di chuyển khi chơi Minigame")]
    [Tooltip("Kéo Player vào đây để khóa di chuyển lúc đang tô màu (để trống sẽ tự tìm)")]
    public MonoBehaviour playerController;

    // Cho phép các hệ thống khác (Crosshair, MobileControls, PlayerInteraction) biết UI cửa đang mở
    public static bool IsAnyOpen { get; private set; }

    public bool IsOpen =>
        (doorMenuUI != null && doorMenuUI.activeSelf) ||
        (minigameUI != null && minigameUI.activeSelf);

    private bool isPlayerNear = false;
    private InteractableOutline outline;
    private int lastToggleFrame = -1;

    private void Awake()
    {
        // Đảm bảo tay cầm VR bấm được UI Menu Cửa / Minigame
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

        if (doorMenuUI == null)
        {
            Transform found = transform.Find("Canvas/Panel_DoorMenu");
            if (found != null) doorMenuUI = found.gameObject;
            else
            {
                var panel = GameObject.Find("Panel_DoorMenu");
                if (panel != null) doorMenuUI = panel;
            }
        }

        if (doorMenuUI != null)
        {
            WireButtonsAtRuntime();
            doorMenuUI.SetActive(false);
        }

        if (minigameUI != null) minigameUI.SetActive(false);
        SyncOpenState();
    }

    private void WireButtonsAtRuntime()
    {
        if (doorMenuUI == null) return;
        Button[] buttons = doorMenuUI.GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            string n = b.gameObject.name.ToLower();
            if (n.Contains("minigame"))
            {
                b.onClick.RemoveListener(OpenMinigame);
                b.onClick.AddListener(OpenMinigame);
            }
            else if (n.Contains("lai") || n.Contains("stay") || n.Contains("o_lai"))
            {
                b.onClick.RemoveListener(StayInGallery);
                b.onClick.AddListener(StayInGallery);
            }
            else if (n.Contains("thoat") || n.Contains("menu") || n.Contains("quit") || n.Contains("exit"))
            {
                b.onClick.RemoveListener(GoToMainMenu);
                b.onClick.AddListener(GoToMainMenu);
            }
        }
    }

    private void OnDisable()
    {
        IsAnyOpen = false;
        if (playerController != null) playerController.enabled = true;
    }

    private void Update()
    {
        if (!isPlayerNear) return;

        // Bấm E hoặc trigger VR để tương tác với cánh cửa
        if (Input.GetKeyDown(KeyCode.E) || HandTriggerInput.WasPressedThisFrame())
        {
            // Nếu đang mở Minigame -> bấm E sẽ đóng Minigame và quay lại triển lãm
            if (minigameUI != null && minigameUI.activeSelf)
            {
                CloseMinigame();
                return;
            }

            ToggleDoorMenu();
        }
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

            // Đi xa thì tự đóng cả Menu Cửa lẫn Minigame (nếu đang mở)
            if (doorMenuUI != null) doorMenuUI.SetActive(false);
            if (minigameUI != null) minigameUI.SetActive(false);
            SyncOpenState();
            if (playerController != null) playerController.enabled = true;
            PlatformHelper.SetCursorLocked(true); // Khóa lại chuột
        }
    }

    private void OnMouseDown()
    {
        // Không nhận click xuyên qua các bảng UI đang mở
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (IsOpen) return;

        if (isPlayerNear)
        {
            ToggleDoorMenu();
        }
    }

    public void ToggleDoorMenu()
    {
        // Chống double-toggle trong cùng 1 frame
        if (lastToggleFrame == Time.frameCount) return;
        lastToggleFrame = Time.frameCount;

        if (doorMenuUI == null) return;

        bool isNowActive = !doorMenuUI.activeSelf;
        doorMenuUI.SetActive(isNowActive);
        SyncOpenState();

        // Mở hoặc khóa chuột phù hợp theo nền tảng
        PlatformHelper.SetCursorLocked(!IsOpen);
    }

    private void SyncOpenState()
    {
        IsAnyOpen = IsOpen;
        MobileControlsOverlay controls = FindAnyObjectByType<MobileControlsOverlay>();
        if (controls != null)
        {
            controls.SetGameplayInputEnabled(!IsAnyOpen);
        }
    }

    // ============================================
    // CÁC HÀM GẮN VÀO BUTTON TRÊN MENU CÁNH CỬA
    // ============================================

    // 1. NÚT "CHƠI MINIGAME" (Mở bảng tô màu)
    public void OpenMinigame()
    {
        lastToggleFrame = Time.frameCount;

        if (doorMenuUI != null) doorMenuUI.SetActive(false); // Ẩn menu cửa

        if (minigameUI != null)
        {
            minigameUI.SetActive(true); // Hiện bảng Minigame
            SyncOpenState();

            // Khóa di chuyển nhân vật khi đang tô màu
            if (playerController != null)
                playerController.enabled = false;

            // Mở chuột để người chơi chọn màu và tô
            PlatformHelper.SetCursorLocked(false);
        }
    }

    // 2. NÚT "Ở LẠI THAM QUAN" (Đóng menu cửa, tiếp tục xem tranh)
    public void StayInGallery()
    {
        lastToggleFrame = Time.frameCount;

        if (doorMenuUI != null) doorMenuUI.SetActive(false);
        SyncOpenState();

        // Mở lại di chuyển & khóa chuột để chơi tiếp
        if (playerController != null)
            playerController.enabled = true;

        PlatformHelper.SetCursorLocked(true);
    }

    // 3. NÚT "QUAY LẠI TRIỂN LÃM" (Gắn vào nút [X] hoặc nút Thoát Minigame)
    public void CloseMinigame()
    {
        lastToggleFrame = Time.frameCount;

        if (minigameUI != null)
        {
            minigameUI.SetActive(false);
        }
        SyncOpenState();

        // Mở lại di chuyển nhân vật
        if (playerController != null)
            playerController.enabled = true;

        // Khóa lại chuột để tiếp tục đi dạo trong bảo tàng
        PlatformHelper.SetCursorLocked(true);
    }

    // 4. NÚT "VỀ MENU CHÍNH"
    public void GoToMainMenu()
    {
        if (doorMenuUI != null) doorMenuUI.SetActive(false);
        if (minigameUI != null) minigameUI.SetActive(false);
        SyncOpenState();

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
