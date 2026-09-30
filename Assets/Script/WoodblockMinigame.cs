using UnityEngine;
using UnityEngine.UI;

public class WoodblockMinigame : MonoBehaviour
{
    [Header("Các lớp màu của tranh (Images)")]
    [Tooltip("Kéo các Image chứa mảnh PNG màu vào đây (Ví dụ: 0: Đỏ, 1: Xanh, 2: Vàng, 3: Nét Đen)")]
    public GameObject[] colorLayers; 

    [Header("Các nút in tương ứng (Buttons)")]
    [Tooltip("Kéo các Button dùng để bấm in vào đây (cùng thứ tự với mảng trên)")]
    public Button[] printButtons; 

    [Header("Hiệu ứng & Âm thanh")]
    [Tooltip("Giao diện/Dòng chữ hiện lên khi hoàn thành")]
    public GameObject successUI; 
    public AudioClip stampSound;   // Tiếng "Cộp!" đóng dấu
    public AudioClip successSound; // Tiếng vỗ tay/nhạc hoàn thành

    private int printedCount = 0;

    private void Start()
    {
        // Khởi tạo: Ẩn tất cả các lớp màu đi
        foreach (var layer in colorLayers)
        {
            if (layer != null) layer.SetActive(false);
        }

        if (successUI != null) successUI.SetActive(false);
        printedCount = 0;
    }

    // Hàm này gọi khi nhấn vào Button In màu
    public void PrintLayer(int layerIndex)
    {
        // Kiểm tra xem index có hợp lệ không và lớp này đã in chưa
        if (layerIndex >= 0 && layerIndex < colorLayers.Length && !colorLayers[layerIndex].activeSelf)
        {
            // Bật hình ảnh của lớp màu đó lên
            colorLayers[layerIndex].SetActive(true);
            printedCount++;

            // Phát tiếng mộc bản (Cộp!) qua AudioManager có sẵn của bạn
            if (AudioManager.Instance != null && stampSound != null)
            {
                AudioManager.Instance.PlaySFX(stampSound);
            }

            // Tắt nút không cho bấm lại nữa
            if (layerIndex < printButtons.Length && printButtons[layerIndex] != null)
            {
                printButtons[layerIndex].interactable = false; 
            }

            // Check xem đã in đủ số lớp chưa -> Thắng
            if (printedCount >= colorLayers.Length)
            {
                OnGameComplete();
            }
        }
    }

    private void OnGameComplete()
    {
        Debug.Log("Đã in xong bức tranh Đông Hồ!");
        
        // Hiện UI Hoàn thành
        if (successUI != null) successUI.SetActive(true);

        // Phát âm thanh ăn mừng
        if (AudioManager.Instance != null && successSound != null)
        {
            AudioManager.Instance.PlaySFX(successSound);
        }
    }
    
    // Hàm gọi khi nhấn nút Chơi Lại (nếu cần)
    public void ResetGame()
    {
        printedCount = 0;
        if (successUI != null) successUI.SetActive(false);

        for (int i = 0; i < colorLayers.Length; i++)
        {
            if (colorLayers[i] != null) colorLayers[i].SetActive(false);
            
            // Bật lại các nút bấm
            if (i < printButtons.Length && printButtons[i] != null)
            {
                printButtons[i].interactable = true;
            }
        }
    }
}
