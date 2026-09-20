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

    private Button clickBlocker; // Vùng trong suốt phủ màn hình để click bên ngoài là thoát

    private void Awake()
    {
        // Khởi tạo Singleton
        if (Instance == null)
        {
            Instance = this;
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
        if (info == null) return;

        titleText.text = info.paintingTitle;
        descriptionText.text = info.paintingDescription;
        displayImage.sprite = info.paintingSprite;

        popupPanel.SetActive(true); // Hiện bảng UI
        IsPopupOpen = true;

        if (clickBlocker != null)
        {
            clickBlocker.gameObject.SetActive(true);
        }

        // Mở khóa chuột để click nút X hoặc thao tác UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Hàm gọi khi bấm nút X để đóng
    public void ClosePopup()
    {
        if (popupPanel == null) return;

        popupPanel.SetActive(false); // Ẩn bảng UI
        IsPopupOpen = false;

        if (clickBlocker != null)
        {
            clickBlocker.gameObject.SetActive(false);
        }

        // Khóa lại con trỏ chuột để điều khiển nhân vật tiếp
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
}