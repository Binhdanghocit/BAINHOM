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
    [Tooltip("Tốc độ zoom khi nhúm 2 ngón trên điện thoại (đơn vị khoảng cách / pixel dãn)")]
    public float touchZoomSensitivity = 0.01f;

    [Header("Camera Collision")]
    [Min(0.01f)] public float collisionRadius = 0.2f;
    [Min(0f)] public float collisionPadding = 0.04f;
    [Min(0.01f)] public float collisionRecoveryTime = 0.2f;
    public LayerMask collisionLayers = ~0;

    // distance remains the user's zoom; collision never changes the view mode.
    private float collisionDistance;
    private float collisionVelocity;
    private Vector3 collisionOffset;
    private Vector3 collisionOffsetVelocity;
    private Transform collisionTarget;
    private Camera collisionCamera;
    private readonly Vector3[] nearPlaneCorners = new Vector3[4];

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
        if (!PlatformHelper.IsTouchDevice() && GameplayInput.GetKeyDown(KeyCode.LeftAlt))
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
                currentX += GameplayInput.GetAxis("Mouse X") * mouseSensitivity;
            }

            // Xoay lên/xuống (Pitch) luôn do Camera đảm nhận
            currentY -= GameplayInput.GetAxis("Mouse Y") * mouseSensitivity;
            currentY = Mathf.Clamp(currentY, pitchMin, pitchMax);

            // Zoom con trỏ chuột
            float scroll = GameplayInput.GetAxis("Mouse ScrollWheel");
            distance -= scroll * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        // Mobile: nhúm 2 ngón trên nửa phải để zoom (dãn = lại gần, chụm = ra xa).
        // Zoom sát qua ngưỡng FirstPersonThreshold sẽ tự sang góc nhìn thứ 1 như PC.
        float pinch = MobileControlsOverlay.ConsumePinchDelta();
        if (pinch != 0f)
        {
            distance = Mathf.Clamp(distance - pinch * touchZoomSensitivity, minDistance, maxDistance);
        }
    }

    void LateUpdate()
    {
        UpdateCameraPosition(Time.deltaTime);
    }

    private void UpdateCameraPosition(float deltaTime)
    {
        if (target == null) return;

        // FPS: camera luôn bám yaw của nhân vật (fix mobile chỉ kéo lên/xuống được).
        // Trên PC dòng này đã có trong Update khi khóa chuột, nhưng mobile không khóa
        // chuột nên currentX bị đứng yên -> vuốt ngang xoay thân mà camera không xoay theo.
        if (IsFirstPerson)
        {
            currentX = target.eulerAngles.y;
        }

        // Tính góc xoay từ chuột
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);

        // 1. Trọng tâm nhìn (tầm mắt/ngực): dùng offset.y (1.2m)
        Vector3 focusPoint = target.position + Vector3.up * offset.y;

        // 2. Tính vị trí lùi camera về sau theo góc xoay và khoảng cách
        Vector3 direction = -(rotation * Vector3.forward);
        float requestedDistance = Mathf.Max(0f, distance);
        if (collisionTarget != target)
        {
            collisionTarget = target;
            collisionDistance = requestedDistance;
            collisionVelocity = 0f;
            collisionOffset = Vector3.zero;
            collisionOffsetVelocity = Vector3.zero;
        }
        float radius = GetCollisionRadius();
        collisionOffset = Vector3.SmoothDamp(collisionOffset, Vector3.zero, ref collisionOffsetVelocity,
            Mathf.Max(0.01f, collisionRecoveryTime), Mathf.Infinity, Mathf.Max(0f, deltaTime));
        // An outside pivot can still overlap a wall with the camera sphere. Move
        // the cast origin to the free side using the actual collider geometry.
        Vector3 safePivot = ResolveCameraOverlap(focusPoint + collisionOffset, radius, true);
        Vector3 resolvedOffset = safePivot - focusPoint;
        collisionOffset = resolvedOffset;
        float allowedDistance = FindCollisionDistance(safePivot, direction, requestedDistance, radius);
        if (allowedDistance <= collisionDistance)
        {
            // New obstacles (or zooming in) must constrain this very frame.
            collisionDistance = allowedDistance;
            collisionVelocity = 0f;
        }
        else
        {
            collisionDistance = Mathf.SmoothDamp(collisionDistance, allowedDistance,
                ref collisionVelocity, Mathf.Max(0.01f, collisionRecoveryTime),
                Mathf.Infinity, Mathf.Max(0f, deltaTime));
        }
        Vector3 position = ResolveCameraOverlap(safePivot + direction * collisionDistance, radius);

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

    private bool IsIgnoredCollider(Collider collider)
    {
        return PlayerDetector.IsPlayer(collider) || collider.transform.IsChildOf(target)
            || collider.transform.IsChildOf(transform);
    }

    private float GetCollisionRadius()
    {
        float radius = Mathf.Max(0.01f, collisionRadius);
        if (collisionCamera == null) collisionCamera = GetComponent<Camera>();
        if (collisionCamera != null)
        {
            // Protect the near-plane corners too, including wide aspect ratios.
            collisionCamera.CalculateFrustumCorners(new Rect(0, 0, 1, 1),
                collisionCamera.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, nearPlaneCorners);
            foreach (Vector3 corner in nearPlaneCorners) radius = Mathf.Max(radius, corner.magnitude);
        }
        return radius;
    }

    private Vector3 ResolveCameraOverlap(Vector3 position, float radius, bool constrainRecovery = false)
    {
        float clearanceRadius = radius + Mathf.Max(0.001f, collisionPadding);
        // Re-query after each pass: a correction against one wall may reach another.
        for (int pass = 0; pass < 12; pass++)
        {
            bool corrected = false;
            foreach (Collider collider in Physics.OverlapSphere(position, clearanceRadius,
                collisionLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsIgnoredCollider(collider)) continue;
                Vector3 separation = position - collider.ClosestPoint(position);
                float gap = separation.magnitude;
                // For an outside center, the closest surface supplies the free-side
                // normal, including rotated walls and corners. An inside player/pivot
                // has no such normal; resolving player penetration is a separate task.
                if (gap > 0.00001f && gap < clearanceRadius)
                {
                    Vector3 normal = separation / gap;
                    position += normal * (clearanceRadius - gap + 0.0001f);
                    // Stop only recovery into this wall; preserve recovery along it.
                    float inwardVelocity = Vector3.Dot(collisionOffsetVelocity, normal);
                    if (constrainRecovery && inwardVelocity < 0f)
                        collisionOffsetVelocity -= normal * inwardVelocity;
                    corrected = true;
                }
            }
            if (!corrected) break;
        }
        return position;
    }

    private float FindCollisionDistance(Vector3 pivot, Vector3 direction, float requestedDistance, float radius)
    {
        if (requestedDistance <= 0f) return 0f;
        float allowed = requestedDistance;
        foreach (RaycastHit hit in Physics.SphereCastAll(pivot, radius, direction,
            requestedDistance, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            if (IsIgnoredCollider(hit.collider)) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - Mathf.Max(0f, collisionPadding)));
        }
        return allowed;
    }

}
