using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("--- Scene Configuration ---")]
    [Tooltip("Nhập chính xác tên Scene Triển lãm Tranh Đông Hồ của bạn")]
    public string gallerySceneName = "ExhibitionScene";

    [Header("--- Loading Screen ---")]
    [Tooltip("Màu nền màn hình chờ")]
    public Color loadingBackground = new Color(0.08f, 0.08f, 0.12f, 1f);
    [Tooltip("Màn chờ hiện tối thiểu bao lâu (giây) để người chơi kịp nhìn thấy")]
    public float minLoadingTime = 2f;

    private GameObject loadingRoot;
    private Slider progressBar;
    private TextMeshProUGUI progressText;
    private bool isLoading;

    private void Awake()
    {
        BuildLoadingUI();
        // Main Menu dùng Canvas Screen Space. Cầu nối này biến ray/trigger từ
        // controller VR thành PointerEvent cho chính các Button/Slider hiện có.
        gameObject.AddComponent<VRUIInputBridge>();
    }

    // Gọi khi nhấn nút Play
    public void PlayGame()
    {
        if (isLoading) return;
        if (!Application.CanStreamedLevelBeLoaded(gallerySceneName))
        {
            Debug.LogError("[MainMenu] Scene không có trong Build Settings: " + gallerySceneName);
            return;
        }
        StartCoroutine(LoadGalleryAsync());
    }

    // Gọi khi nhấn nút "Chơi VR" (PC có kính / Quest muốn vào VR thủ công):
    // khởi XR trước rồi mới load gallery để ViewModeController bật rig VR.
    // Trên máy không có kính, XRBoot tự bỏ qua và vào chế độ phẳng như PlayGame.
    public void PlayGameVR()
    {
        if (isLoading) return;
        StartCoroutine(PlayVRRoutine());
    }

    private System.Collections.IEnumerator PlayVRRoutine()
    {
        yield return XRBoot.StartXRRoutine();
        PlayGame();
    }

    // Gọi khi nhấn nút Quit
    public void QuitGame()
    {
        Debug.Log("Đã thoát triển lãm!");
        Application.Quit();
    }

    private IEnumerator LoadGalleryAsync()
    {
        isLoading = true;
        loadingRoot.SetActive(true);
        float startTime = Time.realtimeSinceStartup;
        Debug.Log("[MainMenu] Bắt đầu tải scene: " + gallerySceneName);

        // Load nền, chưa cho chuyển scene vội để kịp vẽ thanh tiến trình
        AsyncOperation op = SceneManager.LoadSceneAsync(gallerySceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            SetProgress(op.progress / 0.9f);
            yield return null;
        }

        SetProgress(1f);

        // Ép màn chờ hiện đủ lâu kể cả khi scene load nhanh (Editor/ổ SSD)
        float elapsed = Time.realtimeSinceStartup - startTime;
        if (elapsed < minLoadingTime)
        {
            yield return new WaitForSecondsRealtime(minLoadingTime - elapsed);
        }

        op.allowSceneActivation = true;
    }

    private void SetProgress(float value)
    {
        value = Mathf.Clamp01(value);
        if (progressBar != null) progressBar.value = value;
        if (progressText != null) progressText.text = "Đang tải... " + Mathf.RoundToInt(value * 100f) + "%";
    }

    // Dựng overlay loading bằng code (khỏi sửa file scene): nền full màn hình +
    // thanh tiến trình + chữ %, ẩn sẵn cho tới khi bấm Play
    private void BuildLoadingUI()
    {
        loadingRoot = new GameObject("LoadingScreen");
        loadingRoot.transform.SetParent(transform, false);

        var canvas = loadingRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        loadingRoot.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        loadingRoot.AddComponent<GraphicRaycaster>();

        var bg = loadingRoot.AddComponent<Image>();
        bg.color = loadingBackground;

        var bgRect = loadingRoot.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Thanh tiến trình (giữa màn hình, hơi lệch xuống)
        var barGo = new GameObject("LoadingBar");
        barGo.transform.SetParent(loadingRoot.transform, false);
        progressBar = barGo.AddComponent<Slider>();
        progressBar.minValue = 0f;
        progressBar.maxValue = 1f;
        progressBar.value = 0f;
        // Track nền tối để thấy rõ phần đã load
        var trackImage = barGo.AddComponent<Image>();
        trackImage.color = new Color(1f, 1f, 1f, 0.15f);
        var barRect = barGo.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0.4f);
        barRect.anchorMax = new Vector2(0.5f, 0.4f);
        barRect.sizeDelta = new Vector2(600f, 30f);

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(barGo.transform, false);
        var fillRect = fillArea.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(1f, 0.8f, 0.2f, 1f); // vàng gallery
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        // Nối Fill vào Slider thì thanh mới co giãn theo value (không cần Handle)
        progressBar.fillRect = fillRt;
        progressBar.handleRect = null;

        // Chữ phần trăm
        var textGo = new GameObject("LoadingText");
        textGo.transform.SetParent(loadingRoot.transform, false);
        progressText = textGo.AddComponent<TextMeshProUGUI>();
        progressText.fontSize = 32;
        progressText.fontStyle = FontStyles.Bold;
        progressText.alignment = TextAlignmentOptions.Center;
        progressText.color = Color.white;
        progressText.text = "Đang tải... 0%";
        var textRect = progressText.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 0.4f);
        textRect.anchorMax = new Vector2(0.5f, 0.4f);
        textRect.sizeDelta = new Vector2(600f, 60f);
        textRect.anchoredPosition = new Vector2(0f, 70f);

        loadingRoot.SetActive(false);
    }
}
