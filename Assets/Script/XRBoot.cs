using System.Collections;
using UnityEngine;
using UnityEngine.XR.Management;

// Tự khởi động XR lúc chạy (runtime init) thay vì "Initialize XR on Startup".
// Nhờ vậy CÙNG 1 bản build:
//  - Điện thoại thường / giả lập / PC phẳng: XR không khởi động -> chạy flat, không đen màn hình.
//  - Kính Quest: tự phát hiện (tên máy) và khởi XR -> chạy VR.
//  - PC có kính / người dùng bấm nút "Chơi VR": gọi XRBoot.TryStartXR để vào VR.
//
// Không cần gắn vào scene: tự bootstrap trước khi scene đầu tiên load.
public class XRBoot : MonoBehaviour
{
    private static XRBoot instance;
    private static bool autoTried;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("XRBoot");
        go.hideFlags = HideFlags.HideAndDontSave;
        instance = go.AddComponent<XRBoot>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        // Chỉ tự thử 1 lần mỗi lần mở app.
        if (!autoTried)
        {
            autoTried = true;
            if (ShouldAutoStartXR())
            {
                StartCoroutine(StartXRRoutine());
            }
        }
    }

    // Quest là thiết bị VR-only: luôn tự vào VR.
    // Các nền tảng khác mặc định chạy flat (PC muốn VR thì bấm nút gọi TryStartXR).
    private static bool ShouldAutoStartXR()
    {
        return PlatformHelper.IsQuestDevice();
    }

    // Khởi XR loader + subsystems. Gọi được từ bất kỳ đâu (nút menu, MainMenuManager...).
    // An toàn khi gọi nhiều lần hoặc khi máy không có kính (chỉ log warning rồi thôi).
    public static IEnumerator StartXRRoutine()
    {
        var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (manager == null) yield break;

        // XR đã chạy rồi -> đảm bảo subsystems bật rồi xong.
        if (manager.activeLoader != null)
        {
            manager.StartSubsystems();
            yield break;
        }
        if (manager.activeLoaders.Count == 0)
        {
            Debug.Log("[XRBoot] Không có XR loader nào được cấu hình -> chạy chế độ phẳng.");
            yield break;
        }

        Debug.Log("[XRBoot] Đang khởi động XR...");
        yield return manager.InitializeLoader();

        if (manager.activeLoader == null)
        {
            Debug.LogWarning("[XRBoot] Không khởi động được XR (máy không có kính?) -> chạy chế độ phẳng.");
            yield break;
        }

        manager.StartSubsystems();
        Debug.Log("[XRBoot] Đã vào chế độ VR.");
    }

    public static void TryStartXR(MonoBehaviour host)
    {
        if (host != null) host.StartCoroutine(StartXRRoutine());
    }

    public static void StopXR()
    {
        var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (manager == null || manager.activeLoader == null) return;
        manager.StopSubsystems();
        manager.DeinitializeLoader();
        Debug.Log("[XRBoot] Đã thoát chế độ VR, về chế độ phẳng.");
    }
}
