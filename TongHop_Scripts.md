# TONG HOP SCRIPTS - BAINHOM

Tong 25 scripts (24 trong Assets/Script + 1 Editor). Trien lam tranh Dong Ho - 1 build chay PC / Mobile / VR.

## I. BANG CHUC NANG NHANH

| # | Script | Chuc nang |
|---|--------|-----------|
| 1 | AudioManager.cs | Singleton nhac nen BGM + SFX toan game, DontDestroyOnLoad. Ham: PlaySFX(), ChangeBGM(), SetMaster/BGM/SFXVolume(). |
| 2 | CrosshairReticle.cs | Tam ngam dau + bang TextMeshPro, raycast giua man hinh de highlight. Ham: SetCrosshairEnabled(), SetForceHidden(). An khi mo UI/VR. |
| 3 | DialogueUIManager.cs | Singleton UI hoi thoai NPC (panel + ten + noi dung + prompt Nhan E). Ham: StartDialogue(), AdvanceLine(), EndDialogue(), RequestPrompt(). |
| 4 | FPSDisplay.cs | Do va hien FPS len TextMeshPro. Doi mau xanh>=50 / vang>=30 / do. Khong co ham public. |
| 5 | HandTriggerInput.cs | Static doc nut trigger 2 tay VR, true dung 1 frame (cache frameCount). Ham: WasPressedThisFrame(). Thay phim E tren VR. |
| 6 | InfoPodiumTrigger.cs | Bat/tat infoDisplay khi player vao/ra trigger. Dung cho buc thong tin tinh. |
| 7 | InteractableOutline.cs | Vien vang LineRenderer + prompt khi toi gan (proximity) hoac tam ngam chi vao (aimed). Ham: SetProximity(), SetAimed(). |
| 8 | MainMenuController.cs | Menu don gian cu: PlayGame() load scene, QuitGame() thoat. Da bi MainMenuManager thay the. |
| 9 | MainMenuManager.cs | Menu chinh: load async + man hinh loading (slider + %), PlayGameVR() qua XRBoot. Ham: PlayGame(), PlayGameVR(), QuitGame(). |
| 10 | MobileControlsOverlay.cs | UI cam ung: joystick trai + vung vuot phai + nut TUONG TAC. Ham: FindOrCreate(), ConsumeLookDelta(), ConsumeInteractionPressed(). |
| 11 | NPCInteractable.cs | Luu npcName + dialogueLines, mo hoi thoai khi dung gan + bam E/trigger VR. Chong kich hoat kep voi popup tranh. |
| 12 | PaintingInfo.cs | Container du lieu tranh: paintingTitle, paintingDescription, paintingSprite. Khong co ham. |
| 13 | PaintingNamePlate.cs | Tu dien ten bang tranh tu PaintingInfo cha. Ham: UpdatePlateText(). Chay ca Edit mode. |
| 14 | PaintingTrigger.cs | Dung gan + E/click/trigger VR thi mo popup tranh. Tu dong khi di xa. Ham: ToggleInteract(). |
| 15 | PaintingUIManager.cs | Singleton popup tranh (panel + anh + tieu de + mo ta + click-blocker). Ham: ShowPaintingInfo(), ClosePopup(). Co IsPopupOpen. |
| 16 | PlatformHelper.cs | Nguon su that nen tang: IsTouchDevice(), IsQuestDevice(), IsXRDisplayRunning(), SetCursorLocked(). 1 build chay PC/mobile/VR. |
| 17 | PlayerController.cs | Di chuyen CharacterController + Animator (di/chay/nhay/trong luc + tieng buoc chan). Ho tro FPS + TPS + cam ung. |
| 18 | PlayerDetector.cs | Nhan dien player thong nhat: IsPlayer() qua Tag + rig dang ky + fallback CharacterController. Ham: RegisterRoot(). |
| 19 | PlayerInteraction.cs | Click/tap raycast mo tranh (uu tien 1) va NPC (uu tien 2). Ham: Interact(), TryInteract(). |
| 20 | SettingsManager.cs | Menu Settings: panel ESC, slider Master/BGM/SFX, dropdown BGM + FPS, toggle crosshair. Tu bo cap FPS khi VR. |
| 21 | ThirdPersonCamera.cs | Camera TPS/FPS: xoay chuot + zoom scroll (0=FPS), tu an mesh o FPS. Ham: AddLook() cho mobile. Co IsFirstPerson. |
| 22 | ViewModeController.cs | Chuyen rig Desktop <-> VR (poll XR 0.5s), tu tao MobileControls, gan Tag Player. Phim C doi Goc 1/3. |
| 23 | VRUIInputBridge.cs | Bien ray + trigger VR thanh PointerEvent cho UI Screen Space. Chieu ray len mat phang 2m truoc camera. |
| 24 | XRBoot.cs | Tu khoi XR luc chay: Quest tu vao VR, PC/mobile phang, nut Choi VR goi StartXRRoutine(). Bootstrap DontDestroyOnLoad. |
| 25 | FixVnFont.cs | Tool Editor: bake glyph tieng Viet vao LiberationSans SDF (Tools menu). Chuyen Static->Dynamic, TryAddCharacters(). |

---

## II. CODE CHI TIET TUNG FILE

### 1. AudioManager.cs

Duong dan: `Assets/Script/AudioManager.cs`

Chuc nang: Singleton nhac nen BGM + SFX toan game, DontDestroyOnLoad. Ham: PlaySFX(), ChangeBGM(), SetMaster/BGM/SFXVolume().

```csharp
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("--- Audio Sources ---")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;

    [Header("--- List BGM (Nhạc nền) ---")]
    public AudioClip[] bgmClips;

    [Header("--- SFX Clips (Cố định) ---")]
    public AudioClip footstepClip;
    public AudioClip jumpClip;
    public AudioClip inspectClip;

    private float masterVolume = 1.0f;
    private float bgmVolume = 1.0f;
    private float sfxVolume = 1.0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad chỉ áp dụng cho root GameObject. AudioManager trong
            // scene gallery đang nằm con (có parent) nên phải tách ra trước.
            if (transform.parent != null)
                transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            // Tự động tìm 2 AudioSource nếu chưa kéo
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length >= 1 && bgmSource == null) bgmSource = sources[0];
            if (sources.Length >= 2 && sfxSource == null) sfxSource = sources[1];
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Tự động phát bài BGM đầu tiên (phần tử số 0) khi game vừa chạy
        if (bgmClips != null && bgmClips.Length > 0)
        {
            ChangeBGM(0);
        }
    }

    // --- CÁC HÀM PHÁT ÂM THANH ---
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, sfxVolume * masterVolume);
        }
    }

    public void ChangeBGM(int index)
    {
        if (bgmClips != null && index >= 0 && index < bgmClips.Length)
        {
            if (bgmSource != null)
            {
                bgmSource.clip = bgmClips[index];
                bgmSource.loop = true;
                bgmSource.volume = bgmVolume * masterVolume;
                bgmSource.Play();
            }
        }
    }

    // --- CÁC HÀM ĐIỀU CHỈNH ÂM LƯỢNG ---
    public void SetMasterVolume(float value)
    {
        masterVolume = value;
        UpdateVolumes();
    }

    public void SetBGMVolume(float value)
    {
        bgmVolume = value;
        UpdateVolumes();
    }

    public void SetSFXVolume(float value)
    {
        sfxVolume = value;
        UpdateVolumes();
    }

    private void UpdateVolumes()
    {
        if (bgmSource != null)
        {
            bgmSource.volume = bgmVolume * masterVolume;
        }
    }
}
```

---

### 2. CrosshairReticle.cs

Duong dan: `Assets/Script/CrosshairReticle.cs`

Chuc nang: Tam ngam dau + bang TextMeshPro, raycast giua man hinh de highlight. Ham: SetCrosshairEnabled(), SetForceHidden(). An khi mo UI/VR.

```csharp
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
```

---

### 3. DialogueUIManager.cs

Duong dan: `Assets/Script/DialogueUIManager.cs`

Chuc nang: Singleton UI hoi thoai NPC (panel + ten + noi dung + prompt Nhan E). Ham: StartDialogue(), AdvanceLine(), EndDialogue(), RequestPrompt().

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueUIManager : MonoBehaviour
{
    public static DialogueUIManager Instance;

    public bool IsSpeaking { get; private set; }

    private GameObject panel;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI bodyText;
    private GameObject prompt;

    private NPCInteractable currentNPC;
    private int lineIndex;
    private int promptRequests;
    // Cache trạng thái hiện/ẩn prompt: chỉ gọi SetActive khi thật sự đổi -> không tốn GC/UI rebuild thừa
    private bool promptVisible;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildUI();
    }

    private void BuildUI()
    {
        // Canvas
        var canvasGo = new GameObject("DialogueUI");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Panel hội thoại (phía dưới màn hình)
        panel = new GameObject("DialoguePanel");
        panel.transform.SetParent(canvasGo.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0.35f);
        panelRect.offsetMin = new Vector2(20f, 20f);
        panelRect.offsetMax = new Vector2(-20f, -20f);
        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.75f);

        // Tên NPC
        var nameGo = new GameObject("NPCName");
        nameGo.transform.SetParent(panel.transform, false);
        nameText = nameGo.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 32;
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = Color.yellow;
        var nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = new Vector2(20f, -50f);
        nameRect.offsetMax = new Vector2(-20f, -10f);
        nameText.alignment = TextAlignmentOptions.Left;

        // Nội dung hội thoại
        var bodyGo = new GameObject("DialogueText");
        bodyGo.transform.SetParent(panel.transform, false);
        bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        bodyText.fontSize = 28;
        bodyText.color = Color.white;
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        var bodyRect = bodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 0.9f);
        bodyRect.offsetMin = new Vector2(20f, 20f);
        bodyRect.offsetMax = new Vector2(-20f, -55f);
        bodyText.alignment = TextAlignmentOptions.TopLeft;

        // Prompt "Nhấn E"
        prompt = new GameObject("InteractPrompt");
        prompt.transform.SetParent(canvasGo.transform, false);
        var promptText = prompt.AddComponent<TextMeshProUGUI>();
        // Prompt theo nền tảng: điện thoại tap, PC bấm E/click
        promptText.text = PlatformHelper.IsTouchDevice()
            ? "Chạm vào nhân vật/tranh để tương tác"
            : "Nhấn E / Click để tương tác";
        promptText.fontSize = 30;
        promptText.fontStyle = FontStyles.Bold;
        promptText.alignment = TextAlignmentOptions.Center;
        var promptRect = promptText.rectTransform;
        promptRect.anchorMin = new Vector2(0.5f, 0.5f);
        promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.sizeDelta = new Vector2(500f, 60f);
        promptText.color = Color.white;

        panel.SetActive(false);
        prompt.SetActive(false);
    }

    public void StartDialogue(NPCInteractable npc)
    {
        currentNPC = npc;
        lineIndex = 0;
        IsSpeaking = true;
        nameText.text = npc.npcName;
        bodyText.text = npc.dialogueLines.Length > 0 ? npc.dialogueLines[0] : "...";
        panel.SetActive(true);
        UpdatePrompt();
        PlayClickSFX();
    }

    public void AdvanceLine()
    {
        if (!IsSpeaking || currentNPC == null) return;

        lineIndex++;
        if (lineIndex < currentNPC.dialogueLines.Length)
        {
            bodyText.text = currentNPC.dialogueLines[lineIndex];
            PlayClickSFX();
        }
        else
        {
            EndDialogue();
        }
    }

    public void EndDialogue()
    {
        currentNPC = null;
        IsSpeaking = false;
        panel.SetActive(false);
        UpdatePrompt();
    }

    public void SetPromptActive(bool active)
    {
        UpdatePrompt(active ? 1 : 0);
    }

    // Các interactable đăng ký/hủy đăng ký khi player đến gần/rời đi
    public void RequestPrompt(bool request)
    {
        if (request)
        {
            promptRequests++;
        }
        else
        {
            promptRequests--;
            if (promptRequests < 0) promptRequests = 0;
        }
        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        UpdatePrompt(promptRequests);
    }

    // Điểm cập nhật prompt duy nhất: tự bỏ qua nếu trạng thái hiện/ẩn không đổi
    private void UpdatePrompt(int requested)
    {
        bool shouldShow = requested > 0 && !IsSpeaking;
        if (promptVisible == shouldShow) return;
        promptVisible = shouldShow;
        if (prompt != null)
        {
            prompt.SetActive(shouldShow);
        }
    }

    private void PlayClickSFX()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.inspectClip != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.inspectClip);
        }
    }
}
```

---

### 4. FPSDisplay.cs

Duong dan: `Assets/Script/FPSDisplay.cs`

Chuc nang: Do va hien FPS len TextMeshPro. Doi mau xanh>=50 / vang>=30 / do. Khong co ham public.

```csharp
using UnityEngine;
using TMPro;

public class FPSDisplay : MonoBehaviour
{
    [Header("--- UI Reference ---")]
    public TextMeshProUGUI fpsText; // Kéo Text UI vào đây

    [Header("--- Settings ---")]
    public float updateInterval = 0.5f; // Thời gian cập nhật FPS (giây)

    private float accum = 0; // Tổng FPS tích lũy
    private int frames = 0;   // Số khung hình đếm được
    private float timeleft;  // Thời gian đếm ngược

    private void Start()
    {
        if (fpsText == null)
            fpsText = GetComponent<TextMeshProUGUI>();

        timeleft = updateInterval;
    }

    private void Update()
    {
        timeleft -= Time.deltaTime;
        accum += Time.timeScale / Time.deltaTime;
        ++frames;

        // Cập nhật con số hiển thị sau mỗi khoảng updateInterval
        if (timeleft <= 0.0f)
        {
            float fps = accum / frames;

            if (fpsText != null)
            {
                fpsText.text = string.Format("{0:F0} FPS", fps);

                // Đổi màu chữ theo mức FPS để dễ quan sát
                if (fps >= 50)
                    fpsText.color = Color.green;      // Mượt
                else if (fps >= 30)
                    fpsText.color = Color.yellow;     // Tạm ổn
                else
                    fpsText.color = Color.red;        // Giật/Lag
            }

            timeleft = updateInterval;
            accum = 0.0f;
            frames = 0;
        }
    }
}
```

---

### 5. HandTriggerInput.cs

Duong dan: `Assets/Script/HandTriggerInput.cs`

Chuc nang: Static doc nut trigger 2 tay VR, true dung 1 frame (cache frameCount). Ham: WasPressedThisFrame(). Thay phim E tren VR.

```csharp
using UnityEngine;
using UnityEngine.XR;

public static class HandTriggerInput
{
    private static InputDevice left;
    private static InputDevice right;
    private static bool wasLeft;
    private static bool wasRight;

    private static int lastCachedFrame = -1;
    private static bool cachedResult;

    // Trả về true đúng 1 khung khi người dùng bấm trigger (tay trái hoặc tay phải)
    // Tối ưu hóa bằng cách cache kết quả theo Frame, tránh lỗi khi nhiều script gọi cùng 1 frame.
    public static bool WasPressedThisFrame()
    {
        if (Time.frameCount == lastCachedFrame)
        {
            return cachedResult;
        }

        lastCachedFrame = Time.frameCount;

        if (!left.isValid) left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!right.isValid) right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        bool leftNow = Read(left);
        bool rightNow = Read(right);
        bool pressedNow = leftNow || rightNow;
        bool wasPressed = wasLeft || wasRight;

        wasLeft = leftNow;
        wasRight = rightNow;

        cachedResult = pressedNow && !wasPressed;
        return cachedResult;
    }

    private static bool Read(InputDevice device)
    {
        if (!device.isValid) return false;

        return device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed;
    }
}
```

---

### 6. InfoPodiumTrigger.cs

Duong dan: `Assets/Script/InfoPodiumTrigger.cs`

Chuc nang: Bat/tat infoDisplay khi player vao/ra trigger. Dung cho buc thong tin tinh.

```csharp
using UnityEngine;

public class InfoPodiumTrigger : MonoBehaviour
{
    [Header("Keo Canvas hoac Text vao day")]
    public GameObject infoDisplay;

    void Start()
    {
        if (infoDisplay != null)
        {
            infoDisplay.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            if (infoDisplay != null)
            {
                infoDisplay.SetActive(true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            if (infoDisplay != null)
            {
                infoDisplay.SetActive(false);
            }
        }
    }
}
```

---

### 7. InteractableOutline.cs

Duong dan: `Assets/Script/InteractableOutline.cs`

Chuc nang: Vien vang LineRenderer + prompt khi toi gan (proximity) hoac tam ngam chi vao (aimed). Ham: SetProximity(), SetAimed().

```csharp
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractableOutline : MonoBehaviour
{
    [Header("Viền vàng cho vật có thể tương tác")]
    public Color outlineColor = Color.yellow;
    public float lineWidth = 0.03f;
    public float padding = 0.05f;
    [Tooltip("Nhấp nháy nhẹ để gây chú ý")]
    public bool pulse = true;
    public float pulseSpeed = 4f;
    [Tooltip("Bật/Tắt viền vàng (cả Game view lẫn Scene view)")]
    public bool drawOutline = true;

    private BoxCollider boxCol;
    private Collider col; // Cache: không GetComponent lại mỗi lần DrawOutline

    // Hai nguồn highlight độc lập:
    // proximity = người chơi đứng trong vùng trigger
    // aimed     = tâm ngắm (crosshair) đang chỉ vào vật
    private bool proximity;
    private bool aimed;
    private bool applied;
    private bool lineShown; // Trạng thái thực tế của LineRenderer ( tách riêng để xử lý drawOutline đổi lúc runtime )

    public bool IsHighlightActive => proximity || aimed;

    // Cho biết người chơi có đang đứng trong vùng trigger tương tác hay không
    public bool IsProximityActive => proximity;

    private LineRenderer line;

    private void Awake()
    {
        boxCol = GetComponent<BoxCollider>();
        col = GetComponent<Collider>();

        line = GetComponent<LineRenderer>();
        if (line == null) line = gameObject.AddComponent<LineRenderer>();

        // Ưu tiên shader hỗ trợ vertex color cho LineRenderer (URP project)
        Shader shader = Shader.Find("Universal Render Pipeline/Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Diffuse");
        line.material = new Material(shader);
        line.useWorldSpace = true;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.loop = true;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
    }

    // Tự phát hiện người chơi bước vào/ra khỏi vùng trigger
    private void OnTriggerEnter(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            SetProximity(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            SetProximity(false);
        }
    }

    private void Update()
    {
        if (!IsHighlightActive || line == null || !line.enabled) return;

        Color c = outlineColor;
        if (pulse)
        {
            c.a = 0.5f + 0.5f * Mathf.PingPong(Time.time * pulseSpeed, 1f);
        }
        line.startColor = c;
        line.endColor = c;
    }

    // Một trong hai nguồn highlight có thay đổi -> cập nhật lại viền + prompt.
    // Trạng thái prompt (dựa vào "có tương tác được không") tách riêng khỏi
    // trạng thái LineRenderer (tùy thêm drawOutline) để đồng bộ đúng với nhau.
    private void ApplyVisuals()
    {
        bool active = IsHighlightActive;
        bool shouldShow = active && drawOutline;

        if (active != applied)
        {
            applied = active;
            if (active && line != null)
            {
                DrawOutline();
            }

            if (DialogueUIManager.Instance != null)
            {
                DialogueUIManager.Instance.RequestPrompt(active);
            }
        }

        if (shouldShow != lineShown && line != null)
        {
            lineShown = shouldShow;
            line.enabled = shouldShow;
        }
    }

    public void SetProximity(bool on)
    {
        if (proximity == on) return;
        proximity = on;
        ApplyVisuals();
    }

    public void SetAimed(bool on)
    {
        if (aimed == on) return;
        aimed = on;
        ApplyVisuals();
    }

    private void DrawOutline()
    {
        if (col == null) col = GetComponent<Collider>();
        if (col == null) return;

        Bounds b = col.bounds;
        Vector3 center = b.center;
        Vector3 size = b.size;

        float halfX = size.x * 0.5f + padding;
        float halfY = size.y * 0.5f + padding;

        // Vẽ khung quanh mặt trước của vật (theo hướng nhìn của vật)
        Vector3 f = center + transform.forward * (size.z * 0.5f + padding);
        Vector3 right = transform.right;
        Vector3 up = transform.up;

        Vector3 tr = f + right * halfX + up * halfY;
        Vector3 tl = f - right * halfX + up * halfY;
        Vector3 bl = f - right * halfX - up * halfY;
        Vector3 br = f + right * halfX - up * halfY;

        line.positionCount = 4;
        line.SetPosition(0, tr);
        line.SetPosition(1, tl);
        line.SetPosition(2, bl);
        line.SetPosition(3, br);
    }

    // Vẽ khung vàng trong cửa sổ Scene khi highlight đang bật
    private void OnDrawGizmos()
    {
        if (!drawOutline || !IsHighlightActive) return;

        if (boxCol == null) boxCol = GetComponent<BoxCollider>();
        if (boxCol != null)
        {
            Gizmos.color = outlineColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCol.center, boxCol.size);
        }
    }
}
```

---

### 8. MainMenuController.cs

Duong dan: `Assets/Script/MainMenuController.cs`

Chuc nang: Menu don gian cu: PlayGame() load scene, QuitGame() thoat. Da bi MainMenuManager thay the.

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    // Hàm này gọi khi nhấn nút Play
    public void PlayGame()
    {
        // Nhập đúng tên Scene màn chơi chính
        SceneManager.LoadScene("Tranh Đông Hồ");
    }

    // Hàm này gọi khi nhấn nút Quit
    public void QuitGame()
    {
        Debug.Log("Đã thoát Game!"); // Hiển thị trong Editor để kiểm tra
        Application.Quit(); // Chỉ hoạt động khi đã Build thành file .exe / .apk
    }
}
```

---

### 9. MainMenuManager.cs

Duong dan: `Assets/Script/MainMenuManager.cs`

Chuc nang: Menu chinh: load async + man hinh loading (slider + %), PlayGameVR() qua XRBoot. Ham: PlayGame(), PlayGameVR(), QuitGame().

```csharp
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
```

---

### 10. MobileControlsOverlay.cs

Duong dan: `Assets/Script/MobileControlsOverlay.cs`

Chuc nang: UI cam ung: joystick trai + vung vuot phai + nut TUONG TAC. Ham: FindOrCreate(), ConsumeLookDelta(), ConsumeInteractionPressed().

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI cảm ứng được tạo một lần lúc chạy: joystick trái, vùng vuốt phải và nút
/// tương tác. Nó dùng EventSystem/Input System UI Module đang có trong scene;
/// PlayerController đọc các giá trị qua thuộc tính static bên dưới.
/// </summary>
public class MobileControlsOverlay : MonoBehaviour
{
    public static bool IsAvailable { get; private set; }
    public static Vector2 Move { get; private set; }
    private static Vector2 lookDelta;
    private static bool interactionPressed;
    private static bool gameplayInputEnabled = true;

    private int moveFingerId = -1;
    private int lookFingerId = -1;
    private int interactFingerId = -1;
    private Vector2 moveStart;
    private Vector2 interactStart;
    private float interactStartTime;

    public static Vector2 ConsumeLookDelta()
    {
        Vector2 result = lookDelta;
        lookDelta = Vector2.zero;
        return result;
    }

    public static bool ConsumeInteractionPressed()
    {
        bool result = interactionPressed;
        interactionPressed = false;
        return result;
    }

    public void SetGameplayInputEnabled(bool enabled)
    {
        gameplayInputEnabled = enabled;
        if (!enabled)
        {
            Move = Vector2.zero;
            lookDelta = Vector2.zero;
            interactionPressed = false;
            moveFingerId = -1;
            lookFingerId = -1;
            interactFingerId = -1;
        }
    }

    public static MobileControlsOverlay FindOrCreate()
    {
        MobileControlsOverlay existing = FindAnyObjectByType<MobileControlsOverlay>();
        if (existing != null) return existing;

        GameObject root = new GameObject("Mobile Controls Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MobileControlsOverlay));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        MobileControlsOverlay overlay = root.GetComponent<MobileControlsOverlay>();
        overlay.CreateControls();
        return overlay;
    }

    private void Awake()
    {
        IsAvailable = true;
    }

    private void OnDestroy()
    {
        IsAvailable = false;
        Move = Vector2.zero;
        lookDelta = Vector2.zero;
        interactionPressed = false;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        if (!visible) Move = Vector2.zero;
    }

    // Một số emulator Android không chuyển Touch thành PointerEvent của
    // InputSystemUIInputModule. Đọc Touch trực tiếp ở đây để joystick/nút vẫn
    // chạy; các callback UI bên dưới vẫn giữ cho thiết bị thật khi chúng hoạt động.
    private void Update()
    {
        if (!PlatformHelper.IsTouchDevice() || !gameplayInputEnabled) return;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            bool isInteractArea = touch.position.x >= Screen.width - 300f && touch.position.y <= 320f;

            if (touch.phase == TouchPhase.Began)
            {
                if (isInteractArea && interactFingerId < 0)
                {
                    interactFingerId = touch.fingerId;
                    interactStart = touch.position;
                    interactStartTime = Time.unscaledTime;
                }
                else if (touch.position.x < Screen.width * 0.5f && moveFingerId < 0)
                {
                    moveFingerId = touch.fingerId;
                    moveStart = touch.position;
                }
                else if (lookFingerId < 0)
                {
                    lookFingerId = touch.fingerId;
                }
            }

            if (touch.fingerId == moveFingerId)
            {
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    moveFingerId = -1;
                    Move = Vector2.zero;
                }
                else
                {
                    Vector2 offset = (touch.position - moveStart) / 90f;
                    Move = Vector2.ClampMagnitude(offset, 1f);
                }
            }
            else if (touch.fingerId == lookFingerId)
            {
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    lookFingerId = -1;
                else if (touch.phase == TouchPhase.Moved)
                    lookDelta += touch.deltaPosition;
            }
            else if (touch.fingerId == interactFingerId &&
                     (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))
            {
                if (touch.phase == TouchPhase.Ended &&
                    Time.unscaledTime - interactStartTime <= 0.5f &&
                    (touch.position - interactStart).sqrMagnitude <= 900f)
                    interactionPressed = true;
                interactFingerId = -1;
            }
        }
    }

    private void CreateControls()
    {
        VirtualStick stick = CreatePanel<VirtualStick>("Move Joystick", new Vector2(280, 280), new Vector2(0, 0), new Vector2(0, 0), new Vector2(170, 170));
        stick.background.color = new Color(1f, 1f, 1f, 0.18f);
        stick.CreateKnob();

        LookZone look = CreatePanel<LookZone>("Look Swipe Zone", Vector2.zero, new Vector2(0.5f, 0), new Vector2(1, 1), Vector2.zero);
        look.background.color = Color.clear;

        Button interact = CreatePanel<Button>("Interact Button", new Vector2(180, 180), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-150, 170));
        interact.GetComponent<Image>().color = new Color(0.12f, 0.5f, 0.95f, 0.78f);
        Text label = CreateLabel(interact.transform, "TƯƠNG TÁC");
        // Không dùng Button.onClick: emulator có thể không phát UI pointer event;
        // Update() phía trên nhận Touch trực tiếp cho cả joystick và nút này.
    }

    private T CreatePanel<T>(string name, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition) where T : Component
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(T));
        go.transform.SetParent(transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        // Điều khiển dùng Input.touch trực tiếp, vì vậy không được chặn raycast
        // của các popup, Slider và Button Settings nằm phía dưới.
        go.GetComponent<Image>().raycastTarget = false;
        return go.GetComponent<T>();
    }

    private static Text CreateLabel(Transform parent, string value)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        Text text = go.GetComponent<Text>();
        text.text = value;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.color = Color.white;
        return text;
    }

    private class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Image background;
        private RectTransform rect;
        private RectTransform knob;

        private void Awake()
        {
            background = GetComponent<Image>();
            rect = GetComponent<RectTransform>();
        }

        public void CreateKnob()
        {
            GameObject go = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            knob = go.GetComponent<RectTransform>();
            knob.sizeDelta = new Vector2(100, 100);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.45f);
            go.GetComponent<Image>().raycastTarget = false;
        }

        public void OnPointerDown(PointerEventData eventData) => UpdateStick(eventData);
        public void OnDrag(PointerEventData eventData) => UpdateStick(eventData);
        public void OnPointerUp(PointerEventData eventData)
        {
            Move = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
        }

        private void UpdateStick(PointerEventData eventData)
        {
            if (rect == null) rect = GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local);
            Vector2 radius = rect.rect.size * 0.5f;
            Move = new Vector2(local.x / radius.x, local.y / radius.y);
            Move = Vector2.ClampMagnitude(Move, 1f);
            if (knob != null) knob.anchoredPosition = Move * (radius - knob.sizeDelta * 0.5f);
        }
    }

    private class LookZone : MonoBehaviour
    {
        public Image background;
        private void Awake() => background = GetComponent<Image>();
    }
}
```

---

### 11. NPCInteractable.cs

Duong dan: `Assets/Script/NPCInteractable.cs`

Chuc nang: Luu npcName + dialogueLines, mo hoi thoai khi dung gan + bam E/trigger VR. Chong kich hoat kep voi popup tranh.

```csharp
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(InteractableOutline))]
public class NPCInteractable : MonoBehaviour
{
    [Header("Thông tin NPC")]
    public string npcName = "Người dân";

    [Tooltip("Các dòng hội thoại, hiện lần lượt mỗi lần bấm E")]
    public string[] dialogueLines;

    private bool isPlayerNearby = false;
    private InteractableOutline outline;

    private void Start()
    {
        outline = GetComponent<InteractableOutline>();
    }

    private void Update()
    {
        if (!isPlayerNearby || DialogueUIManager.Instance == null) return;

        bool pressed = Input.GetKeyDown(KeyCode.E) || HandTriggerInput.WasPressedThisFrame();
        if (!pressed) return;

        // FIX double-activation (bug trigger VR): 1 nút bấm không được kích hoạt
        // 2 hệ thống cùng lúc (tranh + NPC). Nếu một UI khác đang mở -> bỏ qua,
        // UI đó tự xử lý nút của chính nó.
        if (IsOtherUIOpen()) return;

        if (DialogueUIManager.Instance.IsSpeaking)
        {
            DialogueUIManager.Instance.AdvanceLine();
        }
        else
        {
            DialogueUIManager.Instance.StartDialogue(this);
        }
    }

    private static bool IsOtherUIOpen()
    {
        if (PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsPopupOpen) return true;
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            isPlayerNearby = true;
            if (outline != null) outline.SetProximity(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            isPlayerNearby = false;
            if (outline != null) outline.SetProximity(false);

            // Đi xa là đóng hội thoại đang mở
            if (DialogueUIManager.Instance != null)
            {
                DialogueUIManager.Instance.EndDialogue();
            }
        }
    }
}
```

---

### 12. PaintingInfo.cs

Duong dan: `Assets/Script/PaintingInfo.cs`

Chuc nang: Container du lieu tranh: paintingTitle, paintingDescription, paintingSprite. Khong co ham.

```csharp
using UnityEngine;

public class PaintingInfo : MonoBehaviour
{
    [Header("Thông tin bức tranh")]
    public string paintingTitle = "Bà Nguyệt";
    [TextArea(3, 10)]
    public string paintingDescription = "Tranh dân gian Đông Hồ...";
    public Sprite paintingSprite; // Ảnh phóng to hiển thị trên UI
}
```

---

### 13. PaintingNamePlate.cs

Duong dan: `Assets/Script/PaintingNamePlate.cs`

Chuc nang: Tu dien ten bang tranh tu PaintingInfo cha. Ham: UpdatePlateText(). Chay ca Edit mode.

```csharp
using UnityEngine;
using TMPro;

[ExecuteAlways]
public class PaintingNamePlate : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private void Start()
    {
        UpdatePlateText();
    }

    private void OnValidate()
    {
        UpdatePlateText();
    }

    public void UpdatePlateText()
    {
        var info = GetComponentInParent<PaintingInfo>();
        if (info != null && label != null)
        {
            label.text = info.paintingTitle;
        }
    }
}
```

---

### 14. PaintingTrigger.cs

Duong dan: `Assets/Script/PaintingTrigger.cs`

Chuc nang: Dung gan + E/click/trigger VR thi mo popup tranh. Tu dong khi di xa. Ham: ToggleInteract().

```csharp
using UnityEngine;

[RequireComponent(typeof(InteractableOutline))]
public class PaintingTrigger : MonoBehaviour
{
    private PaintingInfo paintingInfo; // Khai báo để lấy dữ liệu tranh
    private bool isPlayerNearby = false;
    private InteractableOutline outline;

    private void Start()
    {
        // Tự động lấy component PaintingInfo nằm trên cùng GameObject bức tranh này
        paintingInfo = GetComponent<PaintingInfo>();

        if (paintingInfo == null)
        {
            Debug.LogError("Chưa gắn script PaintingInfo trên bức tranh này: " + gameObject.name);
        }

        // Viền vàng nhấp nháy khi player tới gần
        outline = GetComponent<InteractableOutline>();
    }

    private void Update()
    {
        // Bấm phím E hoặc trigger tay VR:
        // - Nếu đang mở bảng thông tin -> đóng (bấm lần nữa để thoát)
        // - Nếu chưa mở -> mở bảng thông tin
        if (!isPlayerNearby) return;

        bool pressed = Input.GetKeyDown(KeyCode.E) || HandTriggerInput.WasPressedThisFrame();
        if (!pressed) return;

        // FIX double-activation (bug trigger VR): khi popup tranh ĐANG MỞ thì
        // nút này chỉ dùng để ĐÓNG popup, không mở cái mới; và không cạnh tranh
        // với các trigger khác (hội thoại NPC...) đang xử lý cùng frame.
        if (PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsPopupOpen)
        {
            PaintingUIManager.Instance.ClosePopup();
            return;
        }

        if (IsOtherUIOpen()) return;

        ToggleInteract();
    }

    private static bool IsOtherUIOpen()
    {
        if (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking) return true;
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            isPlayerNearby = true;
            if (outline != null) outline.SetProximity(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            isPlayerNearby = false;
            if (outline != null) outline.SetProximity(false);

            // Tự động đóng Popup qua Manager khi đi xa
            if (PaintingUIManager.Instance != null)
            {
                PaintingUIManager.Instance.ClosePopup();
            }
        }
    }

    private void OnMouseDown()
    {
        // Chỉ hoạt động khi người chơi ĐANG ĐỨNG GẦN và có đủ dữ liệu
        if (isPlayerNearby && paintingInfo != null)
        {
            ToggleInteract();
        }
    }

    private void ToggleInteract()
    {
        if (PaintingUIManager.Instance == null) return;

        // Popup đang mở -> bấm E / click / trigger lần nữa là thoát
        if (PaintingUIManager.Instance.IsPopupOpen)
        {
            PaintingUIManager.Instance.ClosePopup();
            return;
        }

        if (paintingInfo == null) return;

        // Bật Popup và TRUYỀN DỮ LIỆU tranh vào UIManager
        PaintingUIManager.Instance.ShowPaintingInfo(paintingInfo);

        // Phát âm thanh khi người chơi click xem tranh
        if (AudioManager.Instance != null && AudioManager.Instance.inspectClip != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.inspectClip);
        }
    }
}
```

---

### 15. PaintingUIManager.cs

Duong dan: `Assets/Script/PaintingUIManager.cs`

Chuc nang: Singleton popup tranh (panel + anh + tieu de + mo ta + click-blocker). Ham: ShowPaintingInfo(), ClosePopup(). Co IsPopupOpen.

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PaintingUIManager : MonoBehaviour
{
    // Tạo Singleton để các script khác truy cập dễ dàng
    public static PaintingUIManager Instance;

    [Header("UI Canvas Components")]
    public GameObject popupPanel;          // Bảng khung chính
    public Image displayImage;             // Ô hiển thị ảnh
    public TextMeshProUGUI titleText;      // Ô hiển thị tên tranh
    public TextMeshProUGUI descriptionText;// Ô hiển thị mô tả

    // Cho biết popup có đang mở hay không (để bấm E lần nữa đóng)
    public bool IsPopupOpen { get; private set; }

    private Button clickBlocker; // Vùng trong suốt phủ màn hình để click bên ngoài là thoát

    private void Awake()
    {
        // Khởi tạo Singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Mới vào game thì ẩn bảng thông tin đi
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }

        CreateClickBlocker();
    }

    // Hàm gọi khi bấm vào tranh
    public void ShowPaintingInfo(PaintingInfo info)
    {
        if (info == null) return;

        titleText.text = info.paintingTitle;
        descriptionText.text = info.paintingDescription;
        displayImage.sprite = info.paintingSprite;

        popupPanel.SetActive(true); // Hiện bảng UI
        IsPopupOpen = true;

        if (clickBlocker != null)
        {
            clickBlocker.gameObject.SetActive(true);
        }

        // Mở khóa chuột để click nút X hoặc thao tác UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Hàm gọi khi bấm nút X để đóng
    public void ClosePopup()
    {
        if (popupPanel == null) return;

        popupPanel.SetActive(false); // Ẩn bảng UI
        IsPopupOpen = false;

        if (clickBlocker != null)
        {
            clickBlocker.gameObject.SetActive(false);
        }

        // Khóa lại con trỏ chuột để điều khiển nhân vật tiếp (PC only)
        PlatformHelper.SetCursorLocked(true);
    }

    // Tự tạo vùng trong suốt phủ kín màn hình, nằm DƯỚI popupPanel.
    // Bấm chuột vào bất kỳ đâu ngoài bảng thông tin sẽ nhấn vùng này và đóng popup.
    private void CreateClickBlocker()
    {
        if (popupPanel == null || popupPanel.transform.parent == null) return;

        var blockerObj = new GameObject("PopupClickBlocker");
        blockerObj.transform.SetParent(popupPanel.transform.parent, false);

        var image = blockerObj.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        var rect = blockerObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        clickBlocker = blockerObj.AddComponent<Button>();
        clickBlocker.onClick.AddListener(ClosePopup);

        // Đẩy xuống dưới cùng để popup nằm trên, không bị che
        blockerObj.transform.SetAsFirstSibling();
        blockerObj.SetActive(false);
    }
}
```

---

### 16. PlatformHelper.cs

Duong dan: `Assets/Script/PlatformHelper.cs`

Chuc nang: Nguon su that nen tang: IsTouchDevice(), IsQuestDevice(), IsXRDisplayRunning(), SetCursorLocked(). 1 build chay PC/mobile/VR.

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

// Một nguồn sự thật duy nhất về nền tảng đang chạy:
// điện thoại (cảm ứng) / PC / kính VR. Dùng chung cho mọi system
// để cùng 1 bản build chạy được cả 3 nơi.
public static class PlatformHelper
{
    // True trên điện thoại/tablet (Android/iOS) hoặc bất kỳ thiết bị có cảm ứng.
    // Editor vẫn trả về false trừ khi đang giả lập touch — code desktop giữ nguyên.
    public static bool IsTouchDevice()
    {
        if (Application.isMobilePlatform) return true;
#if UNITY_EDITOR
        return false;
#else
        return Input.touchSupported && SystemInfo.deviceType == DeviceType.Handheld;
#endif
    }

    // True khi đang chạy trên kính Quest/Oculus (Android + tên máy chứa Quest/Oculus).
    public static bool IsQuestDevice()
    {
        if (Application.platform != RuntimePlatform.Android) return false;
        string model = SystemInfo.deviceModel ?? string.Empty;
        return model.IndexOf("quest", System.StringComparison.OrdinalIgnoreCase) >= 0
            || model.IndexOf("oculus", System.StringComparison.OrdinalIgnoreCase) >= 0
            || model.IndexOf("horizon", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // True khi XR display subsystem đang chạy (HMD đã kết nối + loader đã start).
    // Thay thế XRSettings.isDeviceActive (cũ) — vẫn đúng cả khi XR được
    // khởi động MUỘN lúc chạy (runtime init) thay vì lúc mở app.
    public static bool IsXRDisplayRunning()
    {
        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        for (int i = 0; i < displays.Count; i++)
        {
            if (displays[i] != null && displays[i].running) return true;
        }
#if UNITY_2019_1_OR_NEWER
#pragma warning disable 0618
        if (XRSettings.isDeviceActive) return true;
#pragma warning restore 0618
#endif
        return false;
    }

    // Khóa cursor CHỈ trên thiết bị có chuột (PC). Trên cảm ứng gọi hàm này
    // không làm gì — tránh khóa/ẩn con trỏ oan trên điện thoại.
    public static void SetCursorLocked(bool wantLocked)
    {
        if (IsTouchDevice() || IsXRDisplayRunning())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }
        Cursor.lockState = wantLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !wantLocked;
    }
}
```

---

### 17. PlayerController.cs

Duong dan: `Assets/Script/PlayerController.cs`

Chuc nang: Di chuyen CharacterController + Animator (di/chay/nhay/trong luc + tieng buoc chan). Ho tro FPS + TPS + cam ung.

```csharp
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
```

---

### 18. PlayerDetector.cs

Duong dan: `Assets/Script/PlayerDetector.cs`

Chuc nang: Nhan dien player thong nhat: IsPlayer() qua Tag + rig dang ky + fallback CharacterController. Ham: RegisterRoot().

```csharp
using System.Collections.Generic;
using UnityEngine;

// Một nguồn sự thật duy nhất để nhận diện người chơi, dùng chung cho mọi system
// (InteractableOutline, PaintingTrigger, NPCInteractable, InfoPodiumTrigger...)
// Tránh mỗi script tự viết logic riêng -> lệch pha, thiếu đồng bộ.
public static class PlayerDetector
{
    // Các "gốc" người chơi đã đăng ký (desktop rig, VR rig) bởi ViewModeController
    private static readonly List<Transform> roots = new List<Transform>(4);

    // Đăng ký trước mỗi scene load / khi biết rig. Gọi lại khi có rig mới.
    public static void RegisterRoot(Transform root)
    {
        if (root == null) return;
        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i] == root) return;
        }
        roots.Add(root);
    }

    public static void ClearRoots()
    {
        roots.Clear();
    }

    // Trả về true nếu collider này thuộc về người chơi
    public static bool IsPlayer(Collider other)
    {
        return other != null && IsPlayer(other.transform);
    }

    public static bool IsPlayer(Transform t)
    {
        if (t == null) return false;

        // 1. Ưu tiên Tag "Player" (chuẩn, gán trực tiếp trên player)
        if (t.CompareTag("Player")) return true;

        // 2. Đi lên cha: trùng với rig đã đăng ký (VR hands, desktop child...)
        Transform p = t;
        int depth = 0;
        while (p != null && depth < 10)
        {
            for (int i = 0; i < roots.Count; i++)
            {
                if (p == roots[i]) return true;
            }

            // 3. Fallback desktop: rig có CharacterController / PlayerController
            if (p.GetComponent(typeof(CharacterController)) != null
                || p.GetComponent(typeof(PlayerController)) != null)
            {
                return true;
            }

            p = p.parent;
            depth++;
        }

        return false;
    }
}
```

---

### 19. PlayerInteraction.cs

Duong dan: `Assets/Script/PlayerInteraction.cs`

Chuc nang: Click/tap raycast mo tranh (uu tien 1) va NPC (uu tien 2). Ham: Interact(), TryInteract().

```csharp
/*using UnityEngine;
public class PlayerInteraction : MonoBehaviour

{

    public float interactDistance = 3.5f; // Khoảng cách tối đa để tương tác

    public LayerMask paintingLayer; // Gán layer Tranh để tối ưu



    void Update()

    {

        // Khi bấm chuột trái (hoặc phím E) và chuột đang khóa

        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked)

        {

            TryInteract();

        }

    }



    void TryInteract()

    {

        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)); // Bắn tia từ tâm màn hình

        RaycastHit hit;



        if (Physics.Raycast(ray, out hit, interactDistance))

        {

            PaintingInfo painting = hit.collider.GetComponent<PaintingInfo>();

            if (painting != null)

            {

                PaintingUIManager.Instance.ShowPaintingInfo(painting);

            }

        }

    }

}*/

using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerInteraction : MonoBehaviour
{
    public float interactDistance = 3.5f;

    [Header("Tap cảm ứng (điện thoại)")]
    [Tooltip("Tap nhanh + ít di chuyển = tương tác (giống click chuột)")]
    public float tapMaxDuration = 0.35f;
    public float tapMaxMovePx = 25f;

    void Update()
    {
        if (MobileControlsOverlay.ConsumeInteractionPressed())
        {
            TryInteract();
        }
        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked)
        {
            TryInteract();
        }
        HandleTouchTap();
    }

    // Gán trực tiếp cho Button "Tương tác" trên MobileControlsOverlay, hoặc dùng
    // trong UnityEvent của một UI mobile tự thiết kế.
    public void Interact()
    {
        TryInteract();
    }

    private int tapFingerId = -1;
    private float tapStartTime;
    private Vector2 tapStartPos;

    // Tap 1 ngón nhanh trên điện thoại = click tương tác.
    // Vuốt dài (xoay/joystick) và chạm trên UI tự bị loại.
    private void HandleTouchTap()
    {
        if (Input.touchCount == 0)
        {
            tapFingerId = -1;
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);

            if (t.phase == TouchPhase.Began)
            {
                if (tapFingerId >= 0) continue;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId)) continue;
                tapFingerId = t.fingerId;
                tapStartTime = Time.unscaledTime;
                tapStartPos = t.position;
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                if (t.fingerId != tapFingerId) continue;
                tapFingerId = -1;
                if (t.phase != TouchPhase.Ended) continue;
                if (t.tapCount != 1) continue; // tap 2 ngón dành cho nhảy (PlayerController)
                if (Time.unscaledTime - tapStartTime > tapMaxDuration) continue;
                if ((t.position - tapStartPos).magnitude > tapMaxMovePx) continue;

                TryInteract();
                break;
            }
        }
    }

    public void TryInteract()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // Lấy tất cả các Object bị tia Raycast đâm xuyên qua (xếp theo thứ tự từ gần đến xa)
        RaycastHit[] hits = Physics.RaycastAll(ray, interactDistance);

        // Ưu tiên 1: tranh (giữ nguyên hành vi cũ)
        foreach (RaycastHit hit in hits)
        {
            // Bỏ qua nếu tia đâm trúng Nhân vật hoặc bất kỳ phần nào của Nhân vật
            if (PlayerDetector.IsPlayer(hit.collider.transform) || hit.collider.transform.IsChildOf(transform))
            {
                continue; // Chuyển sang Object tiếp theo đằng sau lưng nhân vật
            }

            // Tìm PaintingInfo trên Object bị đâm trúng (hoặc Cha/Con của nó)
            PaintingInfo painting = hit.collider.GetComponent<PaintingInfo>();
            if (painting == null) painting = hit.collider.GetComponentInParent<PaintingInfo>();
            if (painting == null) painting = hit.collider.GetComponentInChildren<PaintingInfo>();

            // Nếu tìm thấy bức tranh -> Mở UI và DỪNG VÒNG LẶP ngay
            if (painting != null)
            {
                if (PaintingUIManager.Instance != null)
                {
                    PaintingUIManager.Instance.ShowPaintingInfo(painting);
                }
                break; // Đã tìm thấy tranh thì không cần duyệt các vật thể đằng sau nữa
            }
        }

        // Ưu tiên 2: NPC — cho điện thoại (tap) dùng được hội thoại mà không cần phím E.
        // PC/VR vẫn đi đường phím E / trigger như cũ; tap chỉ phát sinh từ cảm ứng.
        if (DialogueUIManager.Instance != null)
        {
            foreach (RaycastHit hit in hits)
            {
                if (PlayerDetector.IsPlayer(hit.collider.transform) || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                NPCInteractable npc = hit.collider.GetComponent<NPCInteractable>();
                if (npc == null) npc = hit.collider.GetComponentInParent<NPCInteractable>();
                if (npc == null) continue;

                if (DialogueUIManager.Instance.IsSpeaking)
                {
                    DialogueUIManager.Instance.AdvanceLine();
                }
                else
                {
                    DialogueUIManager.Instance.StartDialogue(npc);
                }
                break;
            }
        }
    }
}
```

---

### 20. SettingsManager.cs

Duong dan: `Assets/Script/SettingsManager.cs`

Chuc nang: Menu Settings: panel ESC, slider Master/BGM/SFX, dropdown BGM + FPS, toggle crosshair. Tu bo cap FPS khi VR.

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class SettingsManager : MonoBehaviour
{
    [Header("--- UI Panel ---")]
    public GameObject settingsPanel;
    public Button openSettingButton;
    public Button closeSettingButton;

    [Header("--- Volume Sliders ---")]
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;

    [Header("--- Dropdowns & Audio ---")]
    public TMP_Dropdown bgmDropdown;
    public TMP_Dropdown fpsDropdown;
    public AudioSource bgmAudioSource; // Kéo AudioSource phát nhạc ở Scene này vào
    public List<AudioClip> localBGMList = new List<AudioClip>(); // Danh sách nhạc BGM riêng cho Scene này

    [Header("--- Tâm ngắm (Crosshair) ---")]
    public CrosshairReticle crosshair; // Nếu để trống sẽ tự tìm trong scene
    public bool crosshairEnabled = true;

    private void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (openSettingButton != null)
            openSettingButton.onClick.AddListener(OpenPanel);

        if (closeSettingButton != null)
            closeSettingButton.onClick.AddListener(ClosePanel);

        if (masterSlider != null)
            masterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        if (bgmSlider != null)
            bgmSlider.onValueChanged.AddListener(OnBGMVolumeChanged);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        // Cấu hình BGM Dropdown
        SetupBGMDropdown();

        // Cấu hình FPS Dropdown
        SetupFPSDropdown();

        // Đồng bộ trạng thái tâm ngắm với cài đặt
        if (crosshair == null)
        {
            crosshair = FindAnyObjectByType<CrosshairReticle>();
        }
        if (crosshair != null)
        {
            crosshair.SetCrosshairEnabled(crosshairEnabled);
        }
    }

    // Hàm gọi từ Toggle "Hiện tâm" trong Menu Settings
    public void SetCrosshairEnabled(bool value)
    {
        crosshairEnabled = value;
        if (crosshair != null)
        {
            crosshair.SetCrosshairEnabled(value);
        }
    }

    private void Update()
    {
        // Nhấn ESC để mở/đóng Settings (trong khi chơi)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePanel();
        }
    }

    public void TogglePanel()
    {
        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }

    public void OpenPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            MobileControlsOverlay controls = FindAnyObjectByType<MobileControlsOverlay>();
            if (controls != null) controls.SetGameplayInputEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Ẩn tâm ngắm khi menu Settings mở
            if (crosshair != null)
            {
                crosshair.SetForceHidden(true);
            }
        }
    }

    public void ClosePanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
            MobileControlsOverlay controls = FindAnyObjectByType<MobileControlsOverlay>();
            if (controls != null) controls.SetGameplayInputEnabled(true);

            // Hiện lại tâm ngắm khi đóng Settings
            if (crosshair != null)
            {
                crosshair.SetForceHidden(false);
            }

            if (SceneManager.GetActiveScene().name == "MainMenu")
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                // Về game: PC khóa lại chuột, điện thoại giữ nguyên cảm ứng
                PlatformHelper.SetCursorLocked(true);
            }
        }
    }

    private void OnMasterVolumeChanged(float value)
    {
        AudioListener.volume = value; // Chỉnh âm lượng tổng hệ thống
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMasterVolume(value);
    }

    private void OnBGMVolumeChanged(float value)
    {
        if (bgmAudioSource != null)
            bgmAudioSource.volume = value;

        if (AudioManager.Instance != null)
            AudioManager.Instance.SetBGMVolume(value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSFXVolume(value);
    }

    // --- CẤU HÌNH BGM DROPDOWN ---
    private void SetupBGMDropdown()
    {
        if (bgmDropdown == null) return;

        bgmDropdown.ClearOptions();
        List<string> options = new List<string>();

        // Ưu tiên 1: Lấy danh sách nhạc tự kéo trong Inspector của Scene này
        if (localBGMList != null && localBGMList.Count > 0)
        {
            for (int i = 0; i < localBGMList.Count; i++)
            {
                if (localBGMList[i] != null)
                    options.Add(localBGMList[i].name);
                else
                    options.Add("Bài nhạc " + (i + 1));
            }
        }
        // Ưu tiên 2: Nếu không kéo nhạc riêng thì lấy từ AudioManager (nếu có)
        else if (AudioManager.Instance != null && AudioManager.Instance.bgmClips != null)
        {
            for (int i = 0; i < AudioManager.Instance.bgmClips.Length; i++)
            {
                if (AudioManager.Instance.bgmClips[i] != null)
                    options.Add(AudioManager.Instance.bgmClips[i].name);
                else
                    options.Add("Nhạc " + (i + 1));
            }
        }
        else
        {
            options.Add("Không có nhạc");
        }

        bgmDropdown.AddOptions(options);
        bgmDropdown.onValueChanged.AddListener(OnBGMSelected);
    }

    private void OnBGMSelected(int index)
    {
        // Phát nhạc trực tiếp bằng localBGMList nếu có
        if (localBGMList != null && localBGMList.Count > index && bgmAudioSource != null)
        {
            bgmAudioSource.clip = localBGMList[index];
            bgmAudioSource.Play();
        }
        // Hoặc phát bằng AudioManager
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.ChangeBGM(index);
        }
    }

    private void SetupFPSDropdown()
    {
        if (fpsDropdown == null) return;

        fpsDropdown.ClearOptions();
        List<string> fpsOptions = new List<string> { "30 FPS", "60 FPS", "90 FPS", "Không giới hạn" };

        fpsDropdown.AddOptions(fpsOptions);
        fpsDropdown.onValueChanged.AddListener(OnFPSSelected);

        fpsDropdown.value = 1;
        // Đang cắm kính (Quest / Link / PCVR): bỏ cap FPS để compositor pacing
        // theo tần số kính (72/90/120Hz). Cap 60 trên kính 72Hz+ gây giật đều
        // dù đồng hồ FPS báo đủ.
        if (PlatformHelper.IsXRDisplayRunning())
            fpsDropdown.value = 3;
        OnFPSSelected(fpsDropdown.value);
    }

    private void OnFPSSelected(int index)
    {
        QualitySettings.vSyncCount = 0;

        // Trong VR luôn để không giới hạn, bất kể dropdown đang chọn gì.
        if (PlatformHelper.IsXRDisplayRunning())
            index = 3;

        switch (index)
        {
            case 0: Application.targetFrameRate = 30; break;
            case 1: Application.targetFrameRate = 60; break;
            case 2: Application.targetFrameRate = 90; break;
            case 3: Application.targetFrameRate = -1; break;
        }
    }
}
```

---

### 21. ThirdPersonCamera.cs

Duong dan: `Assets/Script/ThirdPersonCamera.cs`

Chuc nang: Camera TPS/FPS: xoay chuot + zoom scroll (0=FPS), tu an mesh o FPS. Ham: AddLook() cho mobile. Co IsFirstPerson.

```csharp
using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target & Offset")]
    public Transform target;                  // Nhân vật cần đi theo
    public Vector3 offset = new Vector3(0, 1.2f, 0);

    [Header("Zoom Settings")]
    public float distance = 2.0f;             // Khoảng cách ban đầu
    public float minDistance = 0.0f;           // 0 = Góc nhìn thứ nhất (First-Person)
    public float maxDistance = 6.0f;           // Tối đa góc nhìn thứ 3
    public float zoomSpeed = 2.0f;             // Tốc độ Zoom

    [Header("Character Renderer (Ẩn mesh khi First-Person)")]
    [Tooltip("Kéo các mesh của nhân vật vào (nếu nhiều mesh: tóc, mũ, thân...). Để TRỐNG = tự tìm toàn bộ Renderer con của Target.")]
    public Renderer[] characterRenderers;

    [Header("Sensitivity & Limits")]
    public float mouseSensitivity = 3f;       // Tốc độ xoay chuột
    public float pitchMin = -40f;             // Góc giới hạn nhìn xuống
    public float pitchMax = 60f;              // Góc giới hạn nhìn lên

    public float currentX = 0f;
    private float currentY = 0f;

    // Ngưỡng khoảng cách xem là "First-Person" (dùng chung cho Camera/PlayerController/Crosshair)
    public const float FirstPersonThreshold = 0.3f;

    public bool IsFirstPerson => distance <= FirstPersonThreshold;

    // Xoay camera bằng code (điều khiển cảm ứng điện thoại): cùng dấu với chuột
    // yaw > 0 = quay phải, pitch > 0 (kéo lên) = ngước lên
    public void AddLook(float yawDelta, float pitchDelta)
    {
        currentX += yawDelta;
        currentY = Mathf.Clamp(currentY - pitchDelta, pitchMin, pitchMax);
    }

    // Cache trạng thái hiển thị mesh: chỉ ghi enabled khi THẬT SỰ đổi góc nhìn
    private bool firstPersonState;
    private bool characterVisible = true;
    private Renderer[] resolvedRenderers;

    // Ưu tiên mảng do user kéo vào; nếu để trống thì tự gom toàn bộ Renderer con của Target
    // (bao cả SkinnedMeshRenderer tóc/mũ/thân...) -> Góc 1 ẩn sạch, không lòi mesh
    private void ResolveRenderers()
    {
        if (resolvedRenderers != null) return;
        if (characterRenderers != null && characterRenderers.Length > 0)
        {
            resolvedRenderers = characterRenderers;
        }
        else if (target != null)
        {
            resolvedRenderers = target.GetComponentsInChildren<Renderer>(true);
        }
        else
        {
            resolvedRenderers = System.Array.Empty<Renderer>();
        }
    }

    void Start()
    {
        // PC mới khóa chuột vào giữa màn hình; điện thoại/VR không khóa
        PlatformHelper.SetCursorLocked(true);

        if (target != null)
        {
            currentX = target.eulerAngles.y;
        }
    }

    void Update()
    {
        // 1. Bấm LeftAlt để Bật/Tắt trạng thái khóa chuột (PC only)
        if (!PlatformHelper.IsTouchDevice() && Input.GetKeyDown(KeyCode.LeftAlt))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // 2. CHỈ cho phép xoay camera và Zoom khi chuột đang bị khóa
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            // Kiểm tra nếu đang ở Góc nhìn thứ nhất (FPS)
            if (IsFirstPerson)
            {
                // Ở FPS, PlayerController trực tiếp xoay thân nhân vật theo Mouse X, 
                // nên Camera lấy luôn góc Y của nhân vật làm currentX.
                if (target != null)
                {
                    currentX = target.eulerAngles.y;
                }
            }
            else
            {
                // Ở TPS, Camera tự xoay tự do quanh nhân vật theo Mouse X
                currentX += Input.GetAxis("Mouse X") * mouseSensitivity;
            }

            // Xoay lên/xuống (Pitch) luôn do Camera đảm nhận
            currentY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            currentY = Mathf.Clamp(currentY, pitchMin, pitchMax);

            // Zoom con trỏ chuột
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            distance -= scroll * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Tính góc xoay từ chuột
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);

        // 1. Trọng tâm nhìn (tầm mắt/ngực): dùng offset.y (1.2m)
        Vector3 focusPoint = target.position + Vector3.up * offset.y;

        // 2. Tính vị trí lùi camera về sau theo góc xoay và khoảng cách
        Vector3 position = focusPoint - (rotation * Vector3.forward * distance);

        // 3. Cập nhật vị trí và góc xoay
        transform.position = position;
        transform.rotation = rotation;

        // 4. Tự động ẩn nhân vật khi Zoom sát mặt.
        // Tối ưu: chỉ ghi enabled đúng khi TRỞ NGANG TRẠNG THÁI (FPS <-> TPS),
        // thay vì đọc/so sánh .enabled mỗi frame như trước.
        bool firstPerson = IsFirstPerson;
        if (firstPerson != firstPersonState)
        {
            firstPersonState = firstPerson;
            ResolveRenderers();
            bool visible = !firstPerson;
            if (visible != characterVisible)
            {
                characterVisible = visible;
                for (int i = 0; i < resolvedRenderers.Length; i++)
                {
                    Renderer r = resolvedRenderers[i];
                    if (r != null && r.enabled != visible)
                    {
                        r.enabled = visible;
                    }
                }
            }
        }
    }
}
```

---

### 22. ViewModeController.cs

Duong dan: `Assets/Script/ViewModeController.cs`

Chuc nang: Chuyen rig Desktop <-> VR (poll XR 0.5s), tu tao MobileControls, gan Tag Player. Phim C doi Goc 1/3.

```csharp
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

        // Desktop/điện thoại: phím C chuyển nhanh Góc 1 <-> Góc 3
        if (Input.GetKeyDown(toggleViewKey) && thirdPersonCamera != null)
        {
            thirdPersonCamera.distance = thirdPersonCamera.IsFirstPerson ? 2.5f : 0f;
        }
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
```

---

### 23. VRUIInputBridge.cs

Duong dan: `Assets/Script/VRUIInputBridge.cs`

Chuc nang: Bien ray + trigger VR thanh PointerEvent cho UI Screen Space. Chieu ray len mat phang 2m truoc camera.

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;

/// <summary>
/// Cho phép controller VR bấm UI Screen Space hiện có (Play, Settings, slider...)
/// mà không cần nhân bản menu sang một Canvas khác. Ray phải được chiếu lên mặt
/// phẳng trước camera rồi chuyển thành tọa độ màn hình của EventSystem.
/// </summary>
public class VRUIInputBridge : MonoBehaviour
{
    private PointerEventData pointer;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private GameObject pressedObject;
    private bool wasPressed;
    private bool dragging;

    private IEnumerator Start()
    {
        // Menu phải khởi XR trước để controller có pose/trigger. Trên PC không
        // có kính, XRBoot tự thất bại an toàn và input chuột vẫn không đổi.
        if (!Application.isMobilePlatform && !PlatformHelper.IsXRDisplayRunning())
            yield return XRBoot.StartXRRoutine();

        if (EventSystem.current != null)
            pointer = new PointerEventData(EventSystem.current);
    }

    private void Update()
    {
        if (!PlatformHelper.IsXRDisplayRunning() || EventSystem.current == null) return;
        if (pointer == null) pointer = new PointerEventData(EventSystem.current);

        InputDevice controller = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!controller.isValid) controller = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!controller.isValid) return;

        if (!controller.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 origin) ||
            !controller.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation)) return;

        if (!TryGetScreenPosition(origin, rotation * Vector3.forward, out Vector2 screenPosition)) return;

        pointer.Reset();
        pointer.position = screenPosition;
        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointer, raycastResults);
        GameObject target = raycastResults.Count > 0 ? raycastResults[0].gameObject : null;
        pointer.pointerCurrentRaycast = raycastResults.Count > 0 ? raycastResults[0] : new RaycastResult();

        controller.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed);
        if (triggerPressed && !wasPressed)
        {
            pressedObject = ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerDownHandler);
            if (pressedObject == null) pressedObject = target;
            ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.initializePotentialDrag);
            dragging = false;
        }
        else if (triggerPressed && wasPressed && pressedObject != null)
        {
            GameObject dragTarget = ExecuteEvents.GetEventHandler<IDragHandler>(pressedObject);
            if (dragTarget != null)
            {
                if (!dragging) ExecuteEvents.Execute(dragTarget, pointer, ExecuteEvents.beginDragHandler);
                dragging = true;
                ExecuteEvents.Execute(dragTarget, pointer, ExecuteEvents.dragHandler);
            }
        }
        else if (!triggerPressed && wasPressed)
        {
            if (pressedObject != null)
            {
                ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.pointerUpHandler);
                if (dragging) ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.endDragHandler);
                else if (target != null) ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.pointerClickHandler);
            }
            pressedObject = null;
            dragging = false;
        }
        wasPressed = triggerPressed;
    }

    private static bool TryGetScreenPosition(Vector3 origin, Vector3 direction, out Vector2 screenPosition)
    {
        screenPosition = default;
        Camera cam = Camera.main;
        if (cam == null) return false;

        // Mặt phẳng UI giả định nằm 2m phía trước camera, đúng với Screen Space
        // Overlay trong headset. Chỉ nhận ray đang hướng vào trước mặt.
        Vector3 normal = cam.transform.forward;
        Vector3 planePoint = cam.transform.position + normal * 2f;
        float denominator = Vector3.Dot(normal, direction);
        if (denominator <= 0.001f) return false;
        float distance = Vector3.Dot(normal, planePoint - origin) / denominator;
        if (distance <= 0f) return false;

        Vector3 local = cam.transform.InverseTransformPoint(origin + direction * distance);
        float halfHeight = 2f * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
        float halfWidth = halfHeight * cam.aspect;
        float viewportX = 0.5f + local.x / halfWidth;
        float viewportY = 0.5f + local.y / halfHeight;
        if (viewportX < 0f || viewportX > 1f || viewportY < 0f || viewportY > 1f) return false;

        screenPosition = new Vector2(viewportX * Screen.width, viewportY * Screen.height);
        return true;
    }
}
```

---

### 24. XRBoot.cs

Duong dan: `Assets/Script/XRBoot.cs`

Chuc nang: Tu khoi XR luc chay: Quest tu vao VR, PC/mobile phang, nut Choi VR goi StartXRRoutine(). Bootstrap DontDestroyOnLoad.

```csharp
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
```

---

### 25. FixVnFont.cs

Duong dan: `Assets/Editor/FixVnFont.cs`

Chuc nang: Tool Editor: bake glyph tieng Viet vao LiberationSans SDF (Tools menu). Chuyen Static->Dynamic, TryAddCharacters().

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TMPro;

// One-shot utility: bake Vietnamese glyphs into LiberationSans SDF.
// Run headless: Unity.exe -batchmode -nographics -projectPath <proj>
//   -executeMethod FixVnFont.AddVietnameseGlyphs -quit -logFile <file>
public static class FixVnFont
{
    private const string FontAssetPath =
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    // Full Vietnamese alphabet with all tones + a few common punctuation marks.
    private const string VietnameseChars =
        "ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝàáâãèéêìíòóôõùúý" +
        "ĂăĐđĨĩŨũƠơƯư" +
        "ẠạẢảẤấẦầẨẩẪẫẬậẮắẰằẲẳẴẵẶặ" +
        "ẸẹẺẻẼẽẾếỀềỂểỄễỆệỈỉỊị" +
        "ỌọỎỏỐốỒồỔổỖỗỘộỚớỜờỞởỠỡỢợ" +
        "ỤụỦủỨứỪừỬửỮữỰựỲỳỴỵỶỷỸỹ" +
        "’“”…" + "–—";

    [MenuItem("Tools/Fix Vietnamese Font (LiberationSans)")]
    public static void AddVietnameseGlyphs()
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null)
        {
            Debug.LogError("[FixVnFont] Font asset not found at " + FontAssetPath);
            return;
        }

        // The asset is currently Static, and TryAddCharacters refuses Static
        // assets. Switching to Dynamic also restores the (currently null)
        // m_SourceFontFile reference from the stored editor reference.
        if (fontAsset.atlasPopulationMode == AtlasPopulationMode.Static)
        {
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            Debug.Log("[FixVnFont] Switched AtlasPopulationMode Static -> Dynamic.");
        }

        fontAsset.isMultiAtlasTexturesEnabled = true;

        var unicodes = new List<uint>();
        foreach (char c in VietnameseChars)
        {
            uint id = c;
            if (!unicodes.Contains(id))
                unicodes.Add(id);
        }

        Debug.Log($"[FixVnFont] Requesting {unicodes.Count} Vietnamese glyphs " +
                  $"(atlas count before: {fontAsset.atlasTextureCount}).");

        bool ok = false;
        uint[] missing = System.Array.Empty<uint>();
        try
        {
            ok = fontAsset.TryAddCharacters(unicodes.ToArray(), out uint[] m);
            if (m != null)
                missing = m;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[FixVnFont] Bake failed: " + e.Message);
            return;
        }

        Debug.Log($"[FixVnFont] TryAddCharacters ok={ok}, missing={missing.Length}, " +
                  $"atlas count after: {fontAsset.atlasTextureCount}.");
        foreach (uint m in missing)
            Debug.LogWarning($"[FixVnFont] Missing glyph U+{m:X4} not in source font.");

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        Debug.Log("[FixVnFont] Done.");
    }
}
```

---

