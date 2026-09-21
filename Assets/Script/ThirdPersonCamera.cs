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
    [Tooltip("Kéo các mesh của nhân vật vào (nếu nhiều mesh: tóc, mũ, thân...). Để TRỐNG = tự tìm toàn bộ Renderer con của Target.")]
    public Renderer[] characterRenderers;

    [Header("Sensitivity & Limits")]
    public float mouseSensitivity = 3f;       // Tốc độ xoay chuột
    public float pitchMin = -40f;             // Góc giới hạn nhìn xuống
    public float pitchMax = 60f;              // Góc giới hạn nhìn lên

    public float currentX = 0f;
    private float currentY = 0f;

    // Ngưỡng khoảng cách xem là "First-Person" (dùng chung cho Camera/PlayerController/Crosshair)
    public const float FirstPersonThreshold = 0.3f;

    public bool IsFirstPerson => distance <= FirstPersonThreshold;

    // Xoay camera bằng code (điều khiển cảm ứng điện thoại): cùng dấu với chuột
    // yaw > 0 = quay phải, pitch > 0 (kéo lên) = ngước lên
    public void AddLook(float yawDelta, float pitchDelta)
    {
        currentX += yawDelta;
        currentY = Mathf.Clamp(currentY - pitchDelta, pitchMin, pitchMax);
    }

    // Cache trạng thái hiển thị mesh: chỉ ghi enabled khi THẬT SỰ đổi góc nhìn
    private bool firstPersonState;
    private bool characterVisible = true;
    private Renderer[] resolvedRenderers;

    // Ưu tiên mảng do user kéo vào; nếu để trống thì tự gom toàn bộ Renderer con của Target
    // (bao cả SkinnedMeshRenderer tóc/mũ/thân...) -> Góc 1 ẩn sạch, không lòi mesh
    private void ResolveRenderers()
    {
        if (resolvedRenderers != null) return;
        if (characterRenderers != null && characterRenderers.Length > 0)
        {
            resolvedRenderers = characterRenderers;
        }
        else if (target != null)
        {
            resolvedRenderers = target.GetComponentsInChildren<Renderer>(true);
        }
        else
        {
            resolvedRenderers = System.Array.Empty<Renderer>();
        }
    }

    void Start()
    {
        // PC mới khóa chuột vào giữa màn hình; điện thoại/VR không khóa
        PlatformHelper.SetCursorLocked(true);

        if (target != null)
        {
            currentX = target.eulerAngles.y;
        }
    }

    void Update()
    {
        // 1. Bấm LeftAlt để Bật/Tắt trạng thái khóa chuột (PC only)
        if (!PlatformHelper.IsTouchDevice() && Input.GetKeyDown(KeyCode.LeftAlt))
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
            if (IsFirstPerson)
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

        // 4. Tự động ẩn nhân vật khi Zoom sát mặt.
        // Tối ưu: chỉ ghi enabled đúng khi TRỞ NGANG TRẠNG THÁI (FPS <-> TPS),
        // thay vì đọc/so sánh .enabled mỗi frame như trước.
        bool firstPerson = IsFirstPerson;
        if (firstPerson != firstPersonState)
        {
            firstPersonState = firstPerson;
            ResolveRenderers();
            bool visible = !firstPerson;
            if (visible != characterVisible)
            {
                characterVisible = visible;
                for (int i = 0; i < resolvedRenderers.Length; i++)
                {
                    Renderer r = resolvedRenderers[i];
                    if (r != null && r.enabled != visible)
                    {
                        r.enabled = visible;
                    }
                }
            }
        }
    }
}