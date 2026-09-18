using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target & Offset")]
    public Transform target;                  // Nhân vật cần đi theo
    public Vector3 offset = new Vector3(0, 1.2f, 0);

    [Header("Zoom Settings")]
    public float distance = 2.0f;             // Khoảng cách ban đầu
    public float minDistance = 0.0f;           // 0 = Góc nhìn thứ nhất (First-Person)
    public float maxDistance = 6.0f;           // Tối đa góc nhìn thứ 3
    public float zoomSpeed = 2.0f;             // Tốc độ Zoom

    [Header("Character Renderer (Ẩn mesh khi First-Person)")]
    public Renderer characterRenderer;        // Kéo SkinnedMeshRenderer của nhân vật vào đây

    [Header("Sensitivity & Limits")]
    public float mouseSensitivity = 3f;       // Tốc độ xoay chuột
    public float pitchMin = -40f;             // Góc giới hạn nhìn xuống
    public float pitchMax = 60f;              // Góc giới hạn nhìn lên

    public float currentX = 0f;
    private float currentY = 0f;

    void Start()
    {
        // Khóa con trỏ chuột vào giữa màn hình
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (target != null)
        {
            currentX = target.eulerAngles.y;
        }
    }

    void Update()
    {
        // 1. Bấm LeftAlt để Bật/Tắt trạng thái khóa chuột
        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // 2. CHỈ cho phép xoay camera và Zoom khi chuột đang bị khóa
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            // Kiểm tra nếu đang ở Góc nhìn thứ nhất (FPS)
            if (distance <= 0.3f)
            {
                // Ở FPS, PlayerController trực tiếp xoay thân nhân vật theo Mouse X, 
                // nên Camera lấy luôn góc Y của nhân vật làm currentX.
                if (target != null)
                {
                    currentX = target.eulerAngles.y;
                }
            }
            else
            {
                // Ở TPS, Camera tự xoay tự do quanh nhân vật theo Mouse X
                currentX += Input.GetAxis("Mouse X") * mouseSensitivity;
            }

            // Xoay lên/xuống (Pitch) luôn do Camera đảm nhận
            currentY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            currentY = Mathf.Clamp(currentY, pitchMin, pitchMax);

            // Zoom con trỏ chuột
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            distance -= scroll * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Tính góc xoay từ chuột
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);

        // 1. Trọng tâm nhìn (tầm mắt/ngực): dùng offset.y (1.2m)
        Vector3 focusPoint = target.position + Vector3.up * offset.y;

        // 2. Tính vị trí lùi camera về sau theo góc xoay và khoảng cách
        Vector3 position = focusPoint - (rotation * Vector3.forward * distance);

        // 3. Cập nhật vị trí và góc xoay
        transform.position = position;
        transform.rotation = rotation;

        // 4. Tự động ẩn nhân vật khi Zoom sát mặt (distance < 0.3)
        if (characterRenderer != null)
        {
            characterRenderer.enabled = (distance > 0.3f);
        }
    }
}