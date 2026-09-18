using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    public Transform cameraTransform;

    [Header("Movement Settings")]
    public float walkSpeed = 2.0f;
    public float runSpeed = 5.0f;
    public float jumpHeight = 1.2f;
    public float gravity = -9.81f;
    public float turnSmoothTime = 0.1f;
    public float mouseSensitivity = 3.0f; // Nhận xoay chuột ngang khi ở FPS

    private CharacterController controller;
    private Animator animator;
    private Vector3 velocity;
    private bool isGrounded;
    private float turnSmoothVelocity;

    private ThirdPersonCamera camScript;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        if (cameraTransform != null)
        {
            camScript = cameraTransform.GetComponent<ThirdPersonCamera>();
        }

        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float targetSpeed = isRunning ? runSpeed : walkSpeed;

        // Kiểm tra xem chuột có đang bị khóa và đang ở góc nhìn thứ nhất (distance <= 0.3f)
        bool isCursorLocked = Cursor.lockState == CursorLockMode.Locked;
        bool isFirstPerson = (camScript != null && camScript.distance <= 0.3f);

        if (isFirstPerson)
        {
            // --- GÓC NHÌN THỨ NHẤT (FPS) ---
            // 1. Di chuột ngang -> Xoay thân nhân vật trực tiếp
            if (isCursorLocked)
            {
                float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
                transform.Rotate(Vector3.up * mouseX);
            }

            // 2. Di chuyển theo hướng nhân vật đang quay mặt
            if (direction.magnitude >= 0.1f)
            {
                Vector3 moveDir = transform.right * horizontal + transform.forward * vertical;
                controller.Move(moveDir.normalized * targetSpeed * Time.deltaTime);

                float animSpeed = isRunning ? 1.0f : 0.5f;
                animator.SetFloat("Speed", animSpeed, 0.1f, Time.deltaTime);
            }
            else
            {
                animator.SetFloat("Speed", 0f, 0.05f, Time.deltaTime);
            }
        }
        else
        {
            // --- GÓC NHÌN THỨ BA (TPS) ---
            if (direction.magnitude >= 0.1f)
            {
                float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                controller.Move(moveDir.normalized * targetSpeed * Time.deltaTime);

                float animSpeed = isRunning ? 1.0f : 0.5f;
                animator.SetFloat("Speed", animSpeed, 0.1f, Time.deltaTime);
            }
            else
            {
                animator.SetFloat("Speed", 0f, 0.05f, Time.deltaTime);
            }
        }

        // Xử lý Nhảy và Trọng lực
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            animator.SetBool("IsGrounded", false);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        if (controller.isGrounded)
        {
            animator.SetBool("IsGrounded", true);
        }
    }
}