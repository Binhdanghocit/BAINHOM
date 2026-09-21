using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    public Transform cameraTransform;

    [Header("Touch Controls (điện thoại)")]
    [Tooltip("Nửa trái vuốt = joystick di chuyển, nửa phải vuốt = xoay góc nhìn")]
    public float touchLookSensitivity = 0.25f;
    [Tooltip("Bán kính joystick ảo (pixel)")]
    public float moveStickRadius = 90f;

    [Header("Movement Settings")]
    public float walkSpeed = 2.0f;
    public float runSpeed = 5.0f;
    public float jumpHeight = 1.2f;
    public float gravity = -9.81f;
    public float turnSmoothTime = 0.1f;
    public float mouseSensitivity = 3.0f; // Nhận xoay chuột ngang khi ở FPS

    [Header("Audio Settings")]
    public float stepIntervalWalk = 0.5f; // Khoảng thời gian giữa các bước đi bộ
    public float stepIntervalRun = 0.3f;  // Khoảng thời gian giữa các bước chạy
    private float stepTimer;

    private CharacterController controller;
    private Animator animator;
    private Vector3 velocity;
    private bool isGrounded;
    private float turnSmoothVelocity;

    private ThirdPersonCamera camScript;

    // State điều khiển cảm ứng (desktop không dùng tới)
    private Vector2 touchMove;
    private Vector2 touchLook;
    private bool touchJump;
    private int moveFingerId = -1;
    private int lookFingerId = -1;
    private Vector2 moveStartPos;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        if (cameraTransform != null)
        {
            camScript = cameraTransform.GetComponent<ThirdPersonCamera>();
        }

        // PC mới khóa chuột; điện thoại dùng cảm ứng nên không khóa
        PlatformHelper.SetCursorLocked(true);
    }

    void Update()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        UpdateTouchInput();

        float horizontal = Mathf.Clamp(Input.GetAxisRaw("Horizontal") + touchMove.x, -1f, 1f);
        float vertical = Mathf.Clamp(Input.GetAxisRaw("Vertical") + touchMove.y, -1f, 1f);
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        // Đẩy joystick hết cỡ trên điện thoại = chạy (tương đương giữ Shift)
        bool isRunning = Input.GetKey(KeyCode.LeftShift) || touchMove.sqrMagnitude > 0.8f;
        float targetSpeed = isRunning ? runSpeed : walkSpeed;

        // Kiểm tra xem chuột có đang bị khóa và đang ở góc nhìn thứ nhất (distance <= 0.3f)
        bool isCursorLocked = Cursor.lockState == CursorLockMode.Locked;
        bool isFirstPerson = (camScript != null && camScript.IsFirstPerson);

        if (isFirstPerson)
        {
            // --- GÓC NHÌN THỨ NHẤT (FPS) ---
            // 1. Di chuột ngang -> Xoay thân nhân vật trực tiếp
            if (isCursorLocked)
            {
                float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
                transform.Rotate(Vector3.up * mouseX);
            }

            // 1b. Cảm ứng: vuốt nửa phải để xoay (góc ngẩng do camera đảm nhận)
            if (touchLook.x != 0f)
            {
                transform.Rotate(Vector3.up * touchLook.x * touchLookSensitivity);
            }
            if (touchLook.y != 0f && camScript != null)
            {
                camScript.AddLook(0f, touchLook.y * touchLookSensitivity);
            }

            // 2. Di chuyển theo hướng nhân vật đang quay mặt
            if (direction.magnitude >= 0.1f)
            {
                Vector3 moveDir = transform.right * horizontal + transform.forward * vertical;
                controller.Move(moveDir.normalized * targetSpeed * Time.deltaTime);

                float animSpeed = isRunning ? 1.0f : 0.5f;
                animator.SetFloat("Speed", animSpeed, 0.1f, Time.deltaTime);

                // Phát tiếng bước chân
                HandleFootstepSounds(isRunning);
            }
            else
            {
                animator.SetFloat("Speed", 0f, 0.05f, Time.deltaTime);
                stepTimer = 0f; // Reset đếm giờ bước chân khi đứng yên
            }
        }
        else
        {
            // --- GÓC NHÌN THỨ BA (TPS) ---
            // Cảm ứng: vuốt nửa phải xoay camera quanh nhân vật
            if (touchLook != Vector2.zero && camScript != null)
            {
                camScript.AddLook(touchLook.x * touchLookSensitivity, touchLook.y * touchLookSensitivity);
            }

            if (direction.magnitude >= 0.1f)
            {
                float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                controller.Move(moveDir.normalized * targetSpeed * Time.deltaTime);

                float animSpeed = isRunning ? 1.0f : 0.5f;
                animator.SetFloat("Speed", animSpeed, 0.1f, Time.deltaTime);

                // Phát tiếng bước chân
                HandleFootstepSounds(isRunning);
            }
            else
            {
                animator.SetFloat("Speed", 0f, 0.05f, Time.deltaTime);
                stepTimer = 0f; // Reset đếm giờ bước chân khi đứng yên
            }
        }

        // Xử lý Nhảy và Trọng lực (tap 2 ngón trên điện thoại = nhảy)
        if ((Input.GetButtonDown("Jump") || touchJump) && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetBool("IsGrounded", false);

            // Phát tiếng nhảy qua AudioManager
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.jumpClip);
            }
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        if (controller.isGrounded)
        {
            animator.SetBool("IsGrounded", true);
        }
    }

    // Quét cảm ứng mỗi frame: nửa trái = joystick di chuyển, nửa phải = xoay nhìn,
    // tap 2 ngón = nhảy. Chạm bắt đầu/kết thúc trên UI thì bỏ qua (để bấm nút).
    // Desktop (không có touch) tự bỏ qua, không ảnh hưởng chuột/phím.
    private void UpdateTouchInput()
    {
        touchMove = Vector2.zero;
        touchLook = Vector2.zero;
        touchJump = false;

        // MobileControlsOverlay nhận pointer từ Input System UI và chuyển nó thành
        // trục di chuyển / delta vuốt. Đường touch cũ phía dưới vẫn là dự phòng
        // cho scene chưa có overlay hoặc các UI tùy biến.
        if (MobileControlsOverlay.IsAvailable)
        {
            touchMove = MobileControlsOverlay.Move;
            touchLook = MobileControlsOverlay.ConsumeLookDelta();
            return;
        }

        if (Input.touchCount == 0)
        {
            moveFingerId = -1;
            lookFingerId = -1;
            return;
        }

        float halfW = Screen.width * 0.5f;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId))
            {
                continue;
            }

            if (t.phase == TouchPhase.Ended && t.tapCount >= 2)
            {
                touchJump = true;
                continue;
            }

            if (t.phase == TouchPhase.Began)
            {
                if (t.position.x < halfW && moveFingerId < 0)
                {
                    moveFingerId = t.fingerId;
                    moveStartPos = t.position;
                }
                else if (t.position.x >= halfW && lookFingerId < 0)
                {
                    lookFingerId = t.fingerId;
                }
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                if (t.fingerId == moveFingerId) moveFingerId = -1;
                if (t.fingerId == lookFingerId) lookFingerId = -1;
            }
            else // Moved || Stationary
            {
                if (t.fingerId == moveFingerId)
                {
                    Vector2 offset = (t.position - moveStartPos) / moveStickRadius;
                    if (offset.sqrMagnitude > 1f) offset.Normalize();
                    touchMove = offset; // y màn hình hướng lên = tiến (khớp trục Vertical)
                }
                else if (t.fingerId == lookFingerId)
                {
                    touchLook += t.deltaPosition;
                }
            }
        }
    }

    // Hàm bổ sung: Quản lý tần suất phát tiếng bước chân khi chạm đất
    private void HandleFootstepSounds(bool running)
    {
        if (!isGrounded) return;

        stepTimer += Time.deltaTime;
        float currentInterval = running ? stepIntervalRun : stepIntervalWalk;

        if (stepTimer >= currentInterval)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.footstepClip);
            }
            stepTimer = 0f;
        }
    }
}
