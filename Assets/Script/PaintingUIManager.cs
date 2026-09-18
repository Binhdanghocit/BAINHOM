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
    }

    // Hàm gọi khi bấm vào tranh
    public void ShowPaintingInfo(PaintingInfo info)
    {
        titleText.text = info.paintingTitle;
        descriptionText.text = info.paintingDescription;
        displayImage.sprite = info.paintingSprite;

        popupPanel.SetActive(true); // Hiện bảng UI

        // Mở khóa chuột để click nút X hoặc thao tác UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Hàm gọi khi bấm nút X để đóng
    public void ClosePopup()
    {
        popupPanel.SetActive(false); // Ẩn bảng UI

        // Khóa lại con trỏ chuột để điều khiển nhân vật tiếp
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}