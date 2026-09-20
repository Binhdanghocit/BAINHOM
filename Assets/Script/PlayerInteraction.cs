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

public class PlayerInteraction : MonoBehaviour
{
    public float interactDistance = 3.5f;

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked)
        {
            TryInteract();
        }
    }

    void TryInteract()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // Lấy tất cả các Object bị tia Raycast đâm xuyên qua (xếp theo thứ tự từ gần đến xa)
        RaycastHit[] hits = Physics.RaycastAll(ray, interactDistance);

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
    }
}