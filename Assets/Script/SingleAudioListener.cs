using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Đảm bảo luôn chỉ có đúng 1 AudioListener đang bật trong scene.
/// Fix spam: "There are 2 audio listeners in the scene..."
/// Nguyên nhân: XR Origin prefab + Main Camera prefab cùng bật AudioListener.
/// Không cần gắn tay: tự bootstrap trước khi scene load (giống XRBoot).
/// </summary>
[DefaultExecutionOrder(-1000)]
public class SingleAudioListener : MonoBehaviour
{
    private static SingleAudioListener instance;
    private float nextCheckTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("SingleAudioListener");
        go.hideFlags = HideFlags.HideAndDontSave;
        instance = go.AddComponent<SingleAudioListener>();
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
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnforceSingleListener();
    }

    private void OnDestroy()
    {
        if (instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnforceSingleListener();
    }

    private void OnEnable()
    {
        EnforceSingleListener();
    }

    private void Update()
    {
        // Check throttled 0.5s để bắt prefab spawn muộn (XR Origin, Main Camera)
        // mà không tốn hiệu năng mỗi frame.
        if (Time.unscaledTime >= nextCheckTime)
        {
            nextCheckTime = Time.unscaledTime + 0.5f;
            EnforceSingleListener();
        }
    }

    public static void EnforceSingleListener(Camera preferredCamera = null)
    {
        var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
        if (listeners.Length == 0) return;
        Camera camera = preferredCamera != null ? preferredCamera : Camera.main;
        AudioListener keep = camera != null && camera.isActiveAndEnabled ? camera.GetComponent<AudioListener>() : null;
        if (keep == null)
        {
            foreach (AudioListener listener in listeners)
            {
                Camera candidate = listener.GetComponent<Camera>();
                if (candidate != null && candidate.isActiveAndEnabled) { keep = listener; break; }
            }
        }
        if (keep == null) keep = listeners[0];
        // Disabled listeners must also participate: the newly active rig may have
        // been disabled as the spare listener during the previous mode.
        foreach (AudioListener listener in listeners) listener.enabled = listener == keep;
    }
}