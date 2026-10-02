using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PaintingUIManager : MonoBehaviour
{
    // Tạo Singleton để các script khác truy cập dễ dàng
    public static PaintingUIManager Instance;

    [Header("UI Canvas Components")]
    public GameObject popupPanel;          // Bảng khung chính
    public Image displayImage;             // Ô hiển thị ảnh
    public TextMeshProUGUI titleText;      // Ô hiển thị tên tranh
    public TextMeshProUGUI descriptionText;// Ô hiển thị mô tả

    // Cho biết popup có đang mở hay không (để bấm E lần nữa đóng)
    public bool IsPopupOpen { get; private set; }
    public PaintingInfo CurrentPainting { get; private set; }

    public bool IsShowingPainting(PaintingInfo info) => IsPopupOpen && info != null && CurrentPainting == info;

    private Button clickBlocker; // Vùng trong suốt phủ màn hình để click bên ngoài là thoát

    private void Awake()
    {
        // Khởi tạo Singleton
        if (Instance == null)
        {
            Instance = this;
            VRUIInputBridge.EnsureInstance();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Mới vào game thì ẩn bảng thông tin đi
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }

        CreateClickBlocker();
    }

    // Hàm gọi khi bấm vào tranh
    public void ShowPaintingInfo(PaintingInfo info)
    {
        if (info == null || popupPanel == null) return;

        if (titleText != null) titleText.text = info.paintingTitle;
        if (descriptionText != null) descriptionText.text = info.paintingDescription;
        if (displayImage != null)
        {
            displayImage.sprite = info.paintingSprite;
            displayImage.preserveAspect = true;
        }

        popupPanel.SetActive(true); // Hiện bảng UI
        IsPopupOpen = true;
        CurrentPainting = info;
        ViewModeController.PauseGameplayForModal();

        if (clickBlocker != null)
        {
            clickBlocker.gameObject.SetActive(true);
        }

        // Phát âm thanh thuyết minh của bức tranh nếu có
        if (AudioManager.Instance != null && info.voiceNarration != null)
        {
            AudioManager.Instance.PlayVoiceover(info.voiceNarration);
        }

        // Tắt tạm joystick ảo trên điện thoại để người chơi đọc/cuộn thông tin tranh không bị trôi nhân vật
        MobileControlsOverlay controls = FindAnyObjectByType<MobileControlsOverlay>();
        if (controls != null) controls.SetGameplayInputEnabled(false);

        // Mở khóa chuột phù hợp theo nền tảng (PC/VR/Mobile)
        PlatformHelper.SetCursorLocked(false);
    }

    // Hàm gọi khi bấm nút X để đóng
    public void ClosePopup()
    {
        if (popupPanel == null) return;

        popupPanel.SetActive(false); // Ẩn bảng UI
        IsPopupOpen = false;
        CurrentPainting = null;

        if (clickBlocker != null)
        {
            clickBlocker.gameObject.SetActive(false);
        }

        // Ngắt âm thanh thuyết minh khi đóng bảng
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopVoiceover();
        }

        // Bật lại joystick ảo trên điện thoại + khóa chuột CHỈ khi không còn modal
        // nào khác (menu cửa, workshop, settings, hội thoại...). Khôi phục vô điều
        // kiện ở đây sẽ mở input trong lúc modal khác còn che màn hình.
        ViewModeController.TryResumeGameplayIfClear();
    }

    // Tự tạo vùng trong suốt phủ kín màn hình, nằm DƯỚI popupPanel.
    // Bấm chuột vào bất kỳ đâu ngoài bảng thông tin sẽ nhấn vùng này và đóng popup.
    private void CreateClickBlocker()
    {
        if (popupPanel == null || popupPanel.transform.parent == null) return;

        var blockerObj = new GameObject("PopupClickBlocker");
        blockerObj.transform.SetParent(popupPanel.transform.parent, false);

        var image = blockerObj.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        var rect = blockerObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        clickBlocker = blockerObj.AddComponent<Button>();
        clickBlocker.onClick.AddListener(ClosePopup);

        // Đẩy xuống dưới cùng để popup nằm trên, không bị che
        blockerObj.transform.SetAsFirstSibling();
        blockerObj.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
