using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    public Transform cameraTransform;

    [Header("Touch Controls (điện thoại)")]
    [Tooltip("Nửa trái vuốt = joystick di chuyển, nửa phải vuốt = xoay góc nhìn")]
    public float touchLookSensitivity = 0.25f;

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

        // --- XOAY NHÌN (khác nhau giữa FPS/TPS, giữ tách riêng) ---
        if (isFirstPerson)
        {
            // Ở FPS, xoay thân nhân vật theo Mouse X / vuốt ngang
            if (isCursorLocked)
            {
                float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
                transform.Rotate(Vector3.up * mouseX);
            }
            if (touchLook.x != 0f)
            {
                transform.Rotate(Vector3.up * touchLook.x * touchLookSensitivity);
            }
            if (touchLook.y != 0f && camScript != null)
            {
                camScript.AddLook(0f, touchLook.y * touchLookSensitivity);
            }
        }
        else if (touchLook != Vector2.zero && camScript != null)
        {
            // Ở TPS, vuốt xoay camera quanh nhân vật
            camScript.AddLook(touchLook.x * touchLookSensitivity, touchLook.y * touchLookSensitivity);
        }

        // --- DI CHUYỂN (logic chung, chỉ khác cách tính moveDir) ---
        if (direction.magnitude >= 0.1f)
        {
            Vector3 moveDir;
            if (isFirstPerson)
            {
                moveDir = transform.right * horizontal + transform.forward * vertical;
            }
            else
            {
                float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
                moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            }

            controller.Move(moveDir.normalized * targetSpeed * Time.deltaTime);
            animator.SetFloat("Speed", isRunning ? 1.0f : 0.5f, 0.1f, Time.deltaTime);

            // Phát tiếng bước chân
            HandleFootstepSounds(isRunning);
        }
        else
        {
            animator.SetFloat("Speed", 0f, 0.05f, Time.deltaTime);
            stepTimer = 0f; // Reset đếm giờ bước chân khi đứng yên
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

        // Dùng lại isGrounded đã cache đầu frame, không gọi controller.isGrounded lần 2
        if (isGrounded)
        {
            animator.SetBool("IsGrounded", true);
        }
    }

    // Overlay luôn được ViewModeController tự tạo lúc Start nên là nguồn duy nhất.
    // Nhảy tap-2-ngón do overlay phát hiện (ConsumeJumpPressed).
    private void UpdateTouchInput()
    {
        if (!MobileControlsOverlay.IsAvailable)
        {
            touchMove = Vector2.zero;
            touchLook = Vector2.zero;
            touchJump = false;
            return;
        }

        touchMove = MobileControlsOverlay.Move;
        touchLook = MobileControlsOverlay.ConsumeLookDelta();
        touchJump = MobileControlsOverlay.ConsumeJumpPressed();
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
