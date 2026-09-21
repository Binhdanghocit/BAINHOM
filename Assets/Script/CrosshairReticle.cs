using UnityEngine;
using TMPro;

public class CrosshairReticle : MonoBehaviour
{
    [Header("Camera để bắn tia & hiệu ứng")]
    public Camera aimCamera;
    [Tooltip("Chỉ hiện tâm ở Góc nhìn thứ 1 (tích)/ hiện cả Góc 1 lẫn Góc 3 (bỏ tích)")]
    public bool onlyInFirstPerson = false;

    public float rayDistance = 10f;
    public Color normalColor = Color.white;
    public Color highlightColor = Color.yellow;

    private TextMeshProUGUI reticleText;
    private GameObject canvasGo;

    // Cache tham chiếu: không tìm lại mỗi frame
    private ThirdPersonCamera thirdPersonCam;
    private bool canvasVisible;
    private bool reticleColorDirty = true;
    private Color lastReticleColor;

    // Bật/tắt tâm (điều khiển từ SettingsManager)
    private bool enabledFlag = true;
    // Tạm ẩn khi một UI khác (Settings...) đang mở
    private bool forceHidden;
    private InteractableOutline aimedOutline;

    private void Awake()
    {
        ResolveAimCamera();
        BuildReticle();
    }

    // Camera.main có thể chưa sẵn sàng ở Awake -> resolve lazy, chỉ tìm khi null
    private void ResolveAimCamera()
    {
        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }
        if (aimCamera != null && thirdPersonCam == null)
        {
            thirdPersonCam = aimCamera.GetComponent<ThirdPersonCamera>();
        }
    }

    private void BuildReticle()
    {
        canvasGo = new GameObject("CrosshairReticleCanvas");

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var go = new GameObject("Reticle");
        go.transform.SetParent(canvasGo.transform, false);

        reticleText = go.AddComponent<TextMeshProUGUI>();
        reticleText.text = "+";
        reticleText.fontSize = 46;
        reticleText.fontStyle = FontStyles.Bold;
        reticleText.alignment = TextAlignmentOptions.Center;
        reticleText.color = normalColor;

        var rect = reticleText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(80f, 80f);
        rect.anchoredPosition = Vector2.zero;
    }

    private void Update()
    {
        ResolveAimCamera();

        // VR: XRI ray interactor sẽ hiển thị dấu ngắm riêng -> ẩn tâm màn hình
        if (PlatformHelper.IsXRDisplayRunning())
        {
            SetCanvasVisible(false);
            ClearAimed();
            return;
        }

        // Có UI đang mở (Popup tranh / Hội thoại NPC / Settings...) -> ẩn tâm
        if (IsAnyUIOpen() || forceHidden || !enabledFlag)
        {
            SetCanvasVisible(false);
            ClearAimed();
            return;
        }

        bool show = !onlyInFirstPerson;
        if (!show && aimCamera != null)
        {
            // onlyInFirstPerson: hiện thêm khi đang Góc 1 (hoặc camera không có ThirdPersonCamera)
            show = thirdPersonCam == null || thirdPersonCam.IsFirstPerson;
        }

        // Tắt hẳn canvas khi không show (Góc 3 + onlyInFirstPerson): không chỉ alpha 0,
        // để không còn đường nào vẽ được dấu tâm lên màn hình
        SetCanvasVisible(show);

        if (show)
        {
            UpdateAimed(FindAimedInteractable());
        }
        else
        {
            ClearAimed();
        }

        Color desired = show ? (aimedOutline != null ? highlightColor : normalColor) : Color.clear;
        if (reticleColorDirty || lastReticleColor != desired)
        {
            reticleColorDirty = false;
            lastReticleColor = desired;
            reticleText.color = desired;
        }
    }

    // Chỉ gọi SetActive khi TRẠNG THÁI thật sự đổi (tránh set ~2 lần/frame)
    private void SetCanvasVisible(bool visible)
    {
        if (canvasVisible == visible) return;
        canvasVisible = visible;
        canvasGo.SetActive(visible);
    }

    private static bool IsAnyUIOpen()
    {
        if (PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsPopupOpen) return true;
        if (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking) return true;
        return false;
    }

    // Bật/tắt tâm ngắm (hàm gọi từ SettingsManager hoặc Toggle trong menu Settings)
    public void SetCrosshairEnabled(bool enabled)
    {
        enabledFlag = enabled;
        if (!enabled)
        {
            ClearAimed();
        }
    }

    // Tạm ẩn khi có UI khác đang che màn hình (Settings...)
    public void SetForceHidden(bool hidden)
    {
        forceHidden = hidden;
        if (hidden)
        {
            ClearAimed();
        }
    }

    private void UpdateAimed(InteractableOutline target)
    {
        if (aimedOutline == target) return;

        if (aimedOutline != null) aimedOutline.SetAimed(false);
        aimedOutline = target;
        if (target != null) target.SetAimed(true);
    }

    private void ClearAimed()
    {
        UpdateAimed(null);
    }

    // Tia từ MÀN HÌNH tìm vật có thể tương tác đang bị nhắm.
    // Chỉ coi là "đang nhắm" khi người chơi ĐỨNG TRONG VÙNG trigger của vật đó.
    private InteractableOutline FindAimedInteractable()
    {
        if (aimCamera == null) return null;

        Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // Trường hợp nhanh (99% frame): 1 raycast duy nhất, không allocation
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, ~0, QueryTriggerInteraction.Collide))
        {
            return null;
        }

        // FIX BUG: Ở Góc 1 camera nằm TRONG lưới nhân vật -> tia đâm trúng collider của
        // chính mình trước, khiến viền vàng không bao giờ hiện (hoặc hiện sai vật).
        // Chỉ khi bị chặn bởi player mới phải quét bổ sung phần phía sau.
        if (PlayerDetector.IsPlayer(hit.collider.transform))
        {
            return FindAimedBehindPlayer(ray, hit.collider);
        }

        var outline = hit.collider.GetComponentInParent<InteractableOutline>();
        if (outline != null && outline.IsProximityActive)
        {
            return outline;
        }
        return null;
    }

    // Quét bổ sung khi tia bị chặn bởi collider của chính người chơi (hiếm gặp, chỉ khi FPS)
    private InteractableOutline FindAimedBehindPlayer(Ray ray, Collider blocker)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, rayDistance);
        for (int i = 0; i < hits.Length; i++)
        {
            // Bỏ qua collider đã chặn + mọi collider của player
            if (hits[i].collider == blocker) continue;
            if (PlayerDetector.IsPlayer(hits[i].collider.transform)) continue;

            var outline = hits[i].collider.GetComponentInParent<InteractableOutline>();
            if (outline != null && outline.IsProximityActive) return outline;
            // Hit đầu tiên không phải player mà không phải interactable -> phần đằng sau bị che khuất
            break;
        }
        return null;
    }
}
