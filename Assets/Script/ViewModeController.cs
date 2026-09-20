using UnityEngine;
using UnityEngine.XR;

public class ViewModeController : MonoBehaviour
{
    [Header("Player không cắm kính (desktop)")]
    public GameObject desktopPlayerRig;   // Object chứa CharacterController + PlayerController + ThirdPersonCamera
    public ThirdPersonCamera thirdPersonCamera;

    [Header("Player cắm kính (VR)")]
    public GameObject vrRig;              // XR Origin (rig) chứa camera HMD

    [Header("Tùy chọn")]
    [Tooltip("Ép góc nhìn thứ nhất khi phát hiện cắm kính (XR active)")]
    public bool forceFirstPersonOnVR = true;
    [Tooltip("Phím chuyển nhanh Góc 1 / Góc 3 khi KHÔNG cắm kính")]
    public KeyCode toggleViewKey = KeyCode.C;

    private bool isVR;

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
        // Tự gắn Tag "Player" cho 2 rig (đỡ phải set tay trong Editor;
        // set trùng tag cũ cũng không sao). Mọi system nhận diện qua PlayerDetector.
        EnsurePlayerTag(desktopPlayerRig);
        EnsurePlayerTag(vrRig);

        // Đăng ký 2 "gốc" người chơi để mọi system nhận diện chung
        // (dự phòng khi tag bị ai đó gỡ mất)
        PlayerDetector.RegisterRoot(desktopPlayerRig != null ? desktopPlayerRig.transform : null);
        PlayerDetector.RegisterRoot(vrRig != null ? vrRig.transform : null);

        isVR = XRSettings.isDeviceActive;

        if (isVR)
        {
            // Cắm kính: bật rig VR, tắt player desktop -> camera HMD = Góc nhìn thứ 1 đúng tự nhiên
            if (vrRig != null) vrRig.SetActive(true);
            if (desktopPlayerRig != null) desktopPlayerRig.SetActive(false);
        }
        else
        {
            // Không cắm kính: dùng player desktop với camera 1/3 như bình thường
            if (vrRig != null) vrRig.SetActive(false);
            if (desktopPlayerRig != null) desktopPlayerRig.SetActive(true);

            if (forceFirstPersonOnVR && thirdPersonCamera != null)
            {
                // Mặc định vào game ở Góc thứ 3, người chơi tự bấm C để sang Góc 1
                thirdPersonCamera.distance = Mathf.Max(thirdPersonCamera.distance, 2f);
            }
        }
    }

    private void Update()
    {
        // Trên VR luôn là Góc thứ 1 (HMD), không cho phép chuyển góc
        if (isVR) return;

        // Desktop: phím C chuyển nhanh Góc 1 <-> Góc 3
        if (Input.GetKeyDown(toggleViewKey) && thirdPersonCamera != null)
        {
            thirdPersonCamera.distance = thirdPersonCamera.IsFirstPerson ? 2.5f : 0f;
        }
    }
}