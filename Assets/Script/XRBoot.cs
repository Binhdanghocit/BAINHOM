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
    // Chặn 2 coroutine cùng init loader một lúc (Awake + nút "Chơi VR" + bridge).
    private static bool initializing;

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

        // XRBoot tự quản lý toàn bộ vòng đời XR (init/start/stop/deinit thủ công),
        // nên phải TẮT chế độ tự động của XRManager ngay từ đầu. Nếu không, lúc thoát
        // Play / quit app, XRManagerSettings.OnDisable sẽ tự gọi StopSubsystems() trên
        // manager chưa init xong và bắn warning "Call to StopSubsystems...".
        // Ép runtime (thay vì trông chờ file settings) để chắc chắn đúng cả khi ai đó
        // tick lại "Initialize XR on Startup" trong Project Settings hay Editor chưa
        // refresh xong file settings.
        EnforceManualLifecycle();

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

    // Tắt lifecycle tự động của XRManager (API public, sinh ra đúng cho trường hợp
    // tự init thủ công như XRBoot). Gọi mỗi lần mở app, trước mọi quyết định init.
    private static void EnforceManualLifecycle()
    {
        var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (manager == null) return;
        manager.automaticLoading = false;
        manager.automaticRunning = false;
    }

    // Khởi XR loader + subsystems. Gọi được từ bất kỳ đâu (nút menu, MainMenuManager...).
    // An toàn khi gọi nhiều lần hoặc khi máy không có kính (chỉ log warning rồi thôi).
    // Không bao giờ gọi API của XRManager khi init chưa xong (nếu không sẽ bắn warning
    // "Call to ... without an initialized manager" giống như XRManagerSettings.OnDisable).
    public static IEnumerator StartXRRoutine()
    {
        var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (manager == null) yield break;

        // XR đã chạy rồi -> đảm bảo subsystems bật rồi xong.
        if (manager.isInitializationComplete && manager.activeLoader != null)
        {
            manager.StartSubsystems();
            UncapFrameRateForVR();
            yield break;
        }
        if (manager.activeLoaders.Count == 0)
        {
            Debug.Log("[XRBoot] Không có XR loader nào được cấu hình -> chạy chế độ phẳng.");
            yield break;
        }

        // Đang có một coroutine khác init dở -> đợi nó xong thay vì init chồng.
        if (initializing)
        {
            yield return new WaitUntil(() => !initializing);
            if (manager.isInitializationComplete && manager.activeLoader != null)
            {
                manager.StartSubsystems();
                UncapFrameRateForVR();
            }
            yield break;
        }
        initializing = true;

        Debug.Log("[XRBoot] Đang khởi động XR...");
        yield return manager.InitializeLoader();
        initializing = false;

        if (!manager.isInitializationComplete || manager.activeLoader == null)
        {
            Debug.LogWarning("[XRBoot] Không khởi động được XR (máy không có kính?) -> chạy chế độ phẳng.");
            yield break;
        }

        manager.StartSubsystems();
        UncapFrameRateForVR();
        Debug.Log("[XRBoot] Đã vào chế độ VR.");
    }

    // VR (Quest / Link / PCVR): bỏ cap FPS và vsync của chế độ phẳng để compositor
    // pacing theo tần số kính (72/90/120Hz). Giữ cap 60 của menu phẳng khi lên kính
    // là nguyên nhân giật đều dù FPS báo đủ.
    private static void UncapFrameRateForVR()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
    }

    public static void TryStartXR(MonoBehaviour host)
    {
        if (host != null) host.StartCoroutine(StartXRRoutine());
    }

    public static void StopXR()
    {
        var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        // Chỉ stop/deinit khi init đã hoàn tất. Gọi lúc chưa init xong (hoặc chưa init)
        // sẽ khiến XRManager bắn warning "Call to ... without an initialized manager",
        // và DeinitializeLoader còn reset cờ khiến OnDisable ở lần thoát sau cũng warning theo.
        if (manager == null || !manager.isInitializationComplete || manager.activeLoader == null) return;
        manager.StopSubsystems();
        manager.DeinitializeLoader();
        Debug.Log("[XRBoot] Đã thoát chế độ VR, về chế độ phẳng.");
    }
}
