using UnityEngine;

[RequireComponent(typeof(InteractableOutline))]
public class PaintingTrigger : MonoBehaviour
{
    private PaintingInfo paintingInfo; // Khai báo để lấy dữ liệu tranh
    private bool isPlayerNearby = false;
    private InteractableOutline outline;

    private void Start()
    {
        // Tự động lấy component PaintingInfo nằm trên cùng GameObject bức tranh này
        paintingInfo = GetComponent<PaintingInfo>();

        if (paintingInfo == null)
        {
            Debug.LogError("Chưa gắn script PaintingInfo trên bức tranh này: " + gameObject.name);
        }

        // Viền vàng nhấp nháy khi player tới gần
        outline = GetComponent<InteractableOutline>();
    }

    private void Update()
    {
        // Bấm phím E hoặc trigger tay VR:
        // - Nếu đang mở bảng thông tin -> đóng (bấm lần nữa để thoát)
        // - Nếu chưa mở -> mở bảng thông tin
        if (!isPlayerNearby) return;

        bool pressed = Input.GetKeyDown(KeyCode.E) || HandTriggerInput.WasPressedThisFrame();
        if (!pressed) return;

        // FIX double-activation (bug trigger VR): khi popup tranh ĐANG MỞ thì
        // nút này chỉ dùng để ĐÓNG popup, không mở cái mới; và không cạnh tranh
        // với các trigger khác (hội thoại NPC...) đang xử lý cùng frame.
        if (PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsPopupOpen)
        {
            PaintingUIManager.Instance.ClosePopup();
            return;
        }

        if (IsOtherUIOpen()) return;

        ToggleInteract();
    }

    private static bool IsOtherUIOpen()
    {
        if (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking) return true;
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            isPlayerNearby = true;
            if (outline != null) outline.SetProximity(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            isPlayerNearby = false;
            if (outline != null) outline.SetProximity(false);

            // Tự động đóng Popup qua Manager khi đi xa
            if (PaintingUIManager.Instance != null)
            {
                PaintingUIManager.Instance.ClosePopup();
            }
        }
    }

    private void OnMouseDown()
    {
        // Chỉ hoạt động khi người chơi ĐANG ĐỨNG GẦN và có đủ dữ liệu
        if (isPlayerNearby && paintingInfo != null)
        {
            ToggleInteract();
        }
    }

    private void ToggleInteract()
    {
        if (PaintingUIManager.Instance == null) return;

        // Popup đang mở -> bấm E / click / trigger lần nữa là thoát
        if (PaintingUIManager.Instance.IsPopupOpen)
        {
            PaintingUIManager.Instance.ClosePopup();
            return;
        }

        if (paintingInfo == null) return;

        // Bật Popup và TRUYỀN DỮ LIỆU tranh vào UIManager
        PaintingUIManager.Instance.ShowPaintingInfo(paintingInfo);

        // Phát âm thanh khi người chơi click xem tranh
        if (AudioManager.Instance != null && AudioManager.Instance.inspectClip != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.inspectClip);
        }
    }
}