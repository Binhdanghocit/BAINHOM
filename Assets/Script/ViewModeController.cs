using UnityEngine;

public class ViewModeController : MonoBehaviour
{
    [Header("Player không cắm kính (desktop / điện thoại)")]
    public GameObject desktopPlayerRig;   // Object chứa CharacterController + PlayerController + ThirdPersonCamera
    public ThirdPersonCamera thirdPersonCamera;

    [Header("Player cắm kính (VR)")]
    public GameObject vrRig;              // XR Origin (rig) chứa camera HMD

    [Header("Điều khiển điện thoại")]
    [Tooltip("Canvas joystick/vuốt/nút tương tác. Để trống để tự tạo khi chạy.")]
    public MobileControlsOverlay mobileControls;

    [Header("Tùy chọn")]
    [Tooltip("Ép góc nhìn thứ nhất khi phát hiện cắm kính (XR active)")]
    public bool forceFirstPersonOnVR = true;
    [Tooltip("Phím chuyển nhanh Góc 1 / Góc 3 khi KHÔNG cắm kính")]
    public KeyCode toggleViewKey = KeyCode.C;

    private bool isVR;
    private float nextPollTime;

    // Gắn Tag "Player" nếu object chưa có (chạy 1 lần lúc vào scene)
    private static void EnsurePlayerTag(GameObject go)
    {
        if (go == null) return;
        if (!go.CompareTag("Player"))
        {
            go.tag = "Player";
        }
    }

    private void Start()
    {
        // Canvas này chỉ hiện trên điện thoại. Tạo lúc chạy giúp các scene cũ cũng
        // có điều khiển mobile mà không phải sao chép thủ công UI vào từng scene.
        if (mobileControls == null)
            mobileControls = MobileControlsOverlay.FindOrCreate();

        // Tự gắn Tag "Player" cho 2 rig (đỡ phải set tay trong Editor;
        // set trùng tag cũ cũng không sao). Mọi system nhận diện qua PlayerDetector.
        EnsurePlayerTag(desktopPlayerRig);
        EnsurePlayerTag(vrRig);

        // Đăng ký 2 "gốc" người chơi để mọi system nhận diện chung
        // (dự phòng khi tag bị ai đó gỡ mất)
        PlayerDetector.RegisterRoot(desktopPlayerRig != null ? desktopPlayerRig.transform : null);
        PlayerDetector.RegisterRoot(vrRig != null ? vrRig.transform : null);

        // Áp chế độ ban đầu, rồi poll tiếp vì XR có thể khởi MUỘN
        // (XRBoot khởi runtime trên Quest / người dùng bấm "Chơi VR").
        isVR = PlatformHelper.IsXRDisplayRunning();
        ApplyMode(true);
        nextPollTime = Time.unscaledTime + 0.5f;
    }

    private void Update()
    {
        // XR có thể bật/tắt bất cứ lúc nào (runtime init) -> kiểm tra định kỳ,
        // chỉ đổi rig khi trạng thái THẬT SỰ đổi (không tốn gì mỗi frame).
        if (Time.unscaledTime >= nextPollTime)
        {
            nextPollTime = Time.unscaledTime + 0.5f;
            bool nowVR = PlatformHelper.IsXRDisplayRunning();
            if (nowVR != isVR)
            {
                isVR = nowVR;
                ApplyMode(false);
            }
        }

        // Trên VR luôn là Góc thứ 1 (HMD), không cho phép chuyển góc
        if (isVR) return;

        // Desktop/điện thoại: phím C chuyển Góc 1 <-> Góc 3.
        // Mobile đổi góc bằng nhúm 2 ngón zoom (qua ThirdPersonCamera).
        if (Input.GetKeyDown(toggleViewKey))
        {
            ToggleView();
        }
    }

    // Đổi góc nhìn bằng phím C trên PC
    public void ToggleView()
    {
        if (thirdPersonCamera == null)
        {
            Debug.LogWarning("[ViewMode] Chưa gán thirdPersonCamera -> không đổi được góc nhìn.");
            return;
        }
        thirdPersonCamera.distance = thirdPersonCamera.IsFirstPerson ? 2.5f : 0f;
    }

    private void ApplyMode(bool firstTime)
    {
        if (isVR)
        {
            if (vrRig == null)
            {
                Debug.LogWarning("[ViewMode] Phát hiện kính VR nhưng chưa gán vrRig -> giữ player phẳng.");
                isVR = false;
                return;
            }
            // Cắm kính: bật rig VR, tắt player desktop -> camera HMD = Góc nhìn thứ 1 đúng tự nhiên
            if (vrRig != null) vrRig.SetActive(true);
            if (desktopPlayerRig != null) desktopPlayerRig.SetActive(false);
            if (mobileControls != null) mobileControls.SetVisible(false);
        }
        else
        {
            // Không cắm kính (PC / điện thoại / giả lập): dùng player phẳng
            if (vrRig != null) vrRig.SetActive(false);
            if (desktopPlayerRig != null) desktopPlayerRig.SetActive(true);
            if (mobileControls != null) mobileControls.SetVisible(PlatformHelper.IsTouchDevice());

            if (forceFirstPersonOnVR && thirdPersonCamera != null)
            {
                // Mặc định ở Góc thứ 3, người chơi tự bấm C để sang Góc 1
                thirdPersonCamera.distance = Mathf.Max(thirdPersonCamera.distance, 2f);
            }
        }

        PlatformHelper.SetCursorLocked(!isVR && !PlatformHelper.IsTouchDevice());
    }
}
