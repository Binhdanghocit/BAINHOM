using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(InteractableOutline))]
public class DoorMenuTrigger : MonoBehaviour
{
    [Header("Giao diện")]
    public GameObject doorMenuUI; // Kéo bảng UI chứa 3 nút (Vào Triển Lãm, Minigame, Thoát) vào đây
    public GameObject minigameUI; // Kéo giao diện Minigame vào đây

    [Header("Fallback Scene (nếu không có MainMenuManager)")]
    public string fallbackGallerySceneName = "ExhibitionScene";

    // Cho phép các hệ thống khác (Crosshair, MobileControls, PlayerInteraction) biết UI cửa đang mở
    public static bool IsAnyOpen { get; private set; }

    public bool IsOpen =>
        (doorMenuUI != null && doorMenuUI.activeSelf) ||
        (minigameUI != null && minigameUI.activeSelf);

    private bool isPlayerNear = false;
    private InteractableOutline outline;
    private MainMenuManager menuManager; // Trỏ tới script chuyển scene có sẵn
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
        menuManager = FindAnyObjectByType<MainMenuManager>();

        if (doorMenuUI != null) doorMenuUI.SetActive(false);
        if (minigameUI != null) minigameUI.SetActive(false);
        SyncOpenState();
    }

    private void OnDisable()
    {
        IsAnyOpen = false;
    }

    private void Update()
    {
        if (!isPlayerNear) return;

        // Bấm E hoặc trigger VR để tương tác với cánh cửa
        if (Input.GetKeyDown(KeyCode.E) || HandTriggerInput.WasPressedThisFrame())
        {
            // Nếu đang mở Minigame từ cửa -> bấm E sẽ đóng Minigame thay vì bật đè Menu Cửa
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
        // Chống double-toggle trong cùng 1 frame (do OnMouseDown + PlayerInteraction cùng bắt click)
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
    // CÁC HÀM NÀY GẮN VÀO BUTTON TRÊN MENU CÁNH CỬA
    // ============================================

    // 1. Nút "Vào Triển Lãm"
    public void GoToGallery()
    {
        if (doorMenuUI != null) doorMenuUI.SetActive(false);
        SyncOpenState();

        if (menuManager == null)
        {
            menuManager = FindAnyObjectByType<MainMenuManager>();
        }

        if (menuManager != null)
        {
            menuManager.PlayGame(); // Gọi hàm load scene có màn hình Loading của hệ thống cũ
        }
        else if (Application.CanStreamedLevelBeLoaded(fallbackGallerySceneName))
        {
            SceneManager.LoadScene(fallbackGallerySceneName);
        }
        else
        {
            Debug.LogError("[DoorMenuTrigger] Chưa có MainMenuManager hoặc tên Scene chưa thêm vào Build Settings!");
        }
    }

    // 2. Nút "Chơi Mini Game"
    public void OpenMinigame()
    {
        if (doorMenuUI != null) doorMenuUI.SetActive(false);

        if (minigameUI != null)
        {
            minigameUI.SetActive(true); // Mở bảng Minigame lên
            SyncOpenState();

            // Hiện chuột để chơi game tô màu theo đúng nền tảng
            PlatformHelper.SetCursorLocked(false);
        }
    }

    // Nút đóng Minigame (nếu có nút X hoặc quay lại)
    public void CloseMinigame()
    {
        lastToggleFrame = Time.frameCount;
        if (minigameUI != null)
        {
            minigameUI.SetActive(false);
        }
        SyncOpenState();
        PlatformHelper.SetCursorLocked(true);
    }

    // 3. Nút "Thoát Game"
    public void ExitGame()
    {
        if (menuManager != null)
        {
            menuManager.QuitGame();
        }
        else
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
