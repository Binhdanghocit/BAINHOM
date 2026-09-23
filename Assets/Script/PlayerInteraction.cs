/*using UnityEngine;
public class PlayerInteraction : MonoBehaviour

{

    public float interactDistance = 3.5f; // Khoảng cách tối đa để tương tác

    public LayerMask paintingLayer; // Gán layer Tranh để tối ưu



    void Update()

    {

        // Khi bấm chuột trái (hoặc phím E) và chuột đang khóa

        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked)

        {

            TryInteract();

        }

    }



    void TryInteract()

    {

        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)); // Bắn tia từ tâm màn hình

        RaycastHit hit;



        if (Physics.Raycast(ray, out hit, interactDistance))

        {

            PaintingInfo painting = hit.collider.GetComponent<PaintingInfo>();

            if (painting != null)

            {

                PaintingUIManager.Instance.ShowPaintingInfo(painting);

            }

        }

    }

}*/

using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerInteraction : MonoBehaviour
{
    public float interactDistance = 3.5f;

    [Header("Tap cảm ứng (điện thoại)")]
    [Tooltip("Tap nhanh + ít di chuyển = tương tác (giống click chuột)")]
    public float tapMaxDuration = 0.35f;
    public float tapMaxMovePx = 25f;

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked)
        {
            TryInteract();
        }
        HandleTouchTap();
    }

    // Gọi tay từ UnityEvent của một UI mobile tự thiết kế (nếu có).
    public void Interact()
    {
        TryInteract();
    }

    private int tapFingerId = -1;
    private float tapStartTime;
    private Vector2 tapStartPos;
    private SettingsManager settingsManager;

    private void Start()
    {
        settingsManager = FindAnyObjectByType<SettingsManager>();
    }

    // Tap 1 ngón nhanh trên điện thoại = click tương tác.
    // Vuốt dài (xoay/joystick) và chạm trên UI tự bị loại.
    private void HandleTouchTap()
    {
        // Đang nhúm 2 ngón zoom: hủy tap tương tác, không mở popup nhầm
        if (MobileControlsOverlay.PinchActive)
        {
            tapFingerId = -1;
            return;
        }

        // Settings đang mở: tap chỉ dành cho UI, không mở popup tranh/NPC
        if (settingsManager != null && settingsManager.IsSettingsOpen())
        {
            tapFingerId = -1;
            return;
        }

        if (Input.touchCount == 0)
        {
            tapFingerId = -1;
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);

            if (t.phase == TouchPhase.Began)
            {
                if (tapFingerId >= 0) continue;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId)) continue;
                if (MobileControlsOverlay.IsInControlZone(t.position)) continue; // nửa trái (joystick) + góc phải dưới (Nhảy)
                tapFingerId = t.fingerId;
                tapStartTime = Time.unscaledTime;
                tapStartPos = t.position;
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                if (t.fingerId != tapFingerId) continue;
                tapFingerId = -1;
                if (t.phase != TouchPhase.Ended) continue;
                if (t.tapCount != 1) continue; // chỉ tap 1 ngón mới tương tác
                if (Time.unscaledTime - tapStartTime > tapMaxDuration) continue;
                if ((t.position - tapStartPos).magnitude > tapMaxMovePx) continue;

                TryInteract();
                break;
            }
        }
    }

    public void TryInteract()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // Lấy tất cả các Object bị tia Raycast đâm xuyên qua (xếp theo thứ tự từ gần đến xa)
        RaycastHit[] hits = Physics.RaycastAll(ray, interactDistance);

        // Ưu tiên 1: tranh (giữ nguyên hành vi cũ)
        foreach (RaycastHit hit in hits)
        {
            // Bỏ qua nếu tia đâm trúng Nhân vật hoặc bất kỳ phần nào của Nhân vật
            if (PlayerDetector.IsPlayer(hit.collider.transform) || hit.collider.transform.IsChildOf(transform))
            {
                continue; // Chuyển sang Object tiếp theo đằng sau lưng nhân vật
            }

            // Tìm PaintingInfo trên Object bị đâm trúng (hoặc Cha/Con của nó)
            PaintingInfo painting = hit.collider.GetComponent<PaintingInfo>();
            if (painting == null) painting = hit.collider.GetComponentInParent<PaintingInfo>();
            if (painting == null) painting = hit.collider.GetComponentInChildren<PaintingInfo>();

            // Nếu tìm thấy bức tranh -> Mở UI và DỪNG VÒNG LẶP ngay
            if (painting != null)
            {
                if (PaintingUIManager.Instance != null)
                {
                    PaintingUIManager.Instance.ShowPaintingInfo(painting);
                }
                break; // Đã tìm thấy tranh thì không cần duyệt các vật thể đằng sau nữa
            }
        }

        // Ưu tiên 2: NPC — cho điện thoại (tap) dùng được hội thoại mà không cần phím E.
        // PC/VR vẫn đi đường phím E / trigger như cũ; tap chỉ phát sinh từ cảm ứng.
        if (DialogueUIManager.Instance != null)
        {
            foreach (RaycastHit hit in hits)
            {
                if (PlayerDetector.IsPlayer(hit.collider.transform) || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                NPCInteractable npc = hit.collider.GetComponent<NPCInteractable>();
                if (npc == null) npc = hit.collider.GetComponentInParent<NPCInteractable>();
                if (npc == null) continue;

                if (DialogueUIManager.Instance.IsSpeaking)
                {
                    DialogueUIManager.Instance.AdvanceLine();
                }
                else
                {
                    DialogueUIManager.Instance.StartDialogue(npc);
                }
                break;
            }
        }
    }
}
