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
    private Button skipButton;
    private GameObject choicesRoot;
    private Button workshopButton;
    private Button continueButton;
    private Button nextLineButton;

    private NPCInteractable currentNPC;
    private int lineIndex;
    private int promptRequests;
    private bool modalInputCaptured;

    public static DialogueUIManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        GameObject managerObject = new GameObject("Dialogue UI Manager");
        return managerObject.AddComponent<DialogueUIManager>();
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        VRUIInputBridge.EnsureInstance();
        BuildUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        IsSpeaking = false;
        RestoreModalInput();
    }

    private void BuildUI()
    {
        // Canvas
        var canvasGo = new GameObject("DialogueUI");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
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
        panelImage.color = UiTheme.PanelBg;

        // Tên NPC
        var nameGo = new GameObject("NPCName");
        nameGo.transform.SetParent(panel.transform, false);
        nameText = nameGo.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 32;
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = UiTheme.TitleGold;
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
        bodyRect.offsetMin = new Vector2(20f, 90f);
        bodyRect.offsetMax = new Vector2(-20f, -55f);
        bodyText.alignment = TextAlignmentOptions.TopLeft;

        panel.SetActive(false);

        // Tua nhanh is shown for the guide flow; normal NPC conversations
        // keep their original interaction pattern.
        skipButton = CreateButton(panel.transform, "SkipDialogue", "Tua nhanh", Vector2.zero, new Vector2(220f, 56f), new Color(0.25f, 0.38f, 0.48f, 1f));
        RectTransform skipRect = skipButton.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(1f, 0f);
        skipRect.anchorMax = new Vector2(1f, 0f);
        skipRect.pivot = new Vector2(1f, 0f);
        skipRect.anchoredPosition = new Vector2(-25f, 25f);
        skipButton.onClick.AddListener(SkipToChoices);
        skipButton.gameObject.SetActive(false);

        nextLineButton = CreateButton(panel.transform, "NextDialogueLine", "Tiếp tục", new Vector2(25f, 25f), new Vector2(220f, 56f), UiTheme.BtnConfirm);
        RectTransform nextRect = nextLineButton.GetComponent<RectTransform>();
        nextRect.anchorMin = nextRect.anchorMax = Vector2.zero;
        nextRect.pivot = Vector2.zero;
        nextLineButton.onClick.AddListener(AdvanceLine);

        choicesRoot = new GameObject("GuideChoices", typeof(RectTransform));
        choicesRoot.transform.SetParent(panel.transform, false);
        RectTransform choicesRect = choicesRoot.GetComponent<RectTransform>();
        choicesRect.anchorMin = new Vector2(0.5f, 0f);
        choicesRect.anchorMax = new Vector2(0.5f, 0f);
        choicesRect.pivot = new Vector2(0.5f, 0f);
        choicesRect.anchoredPosition = new Vector2(0f, 28f);
        choicesRect.sizeDelta = new Vector2(620f, 145f);

        workshopButton = CreateButton(choicesRoot.transform, "WorkshopChoice", "Qua workshop làm tranh", new Vector2(0f, 78f), new Vector2(600f, 62f), UiTheme.BtnAccent);
        workshopButton.onClick.AddListener(ChooseWorkshop);
        continueButton = CreateButton(choicesRoot.transform, "ContinueTourChoice", "Tiếp tục tham quan triển lãm", new Vector2(0f, 8f), new Vector2(600f, 62f), UiTheme.BtnConfirm);
        continueButton.onClick.AddListener(ContinueTour);
        choicesRoot.SetActive(false);
    }

    private Button CreateButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        UiTheme.ApplyButton(button, color);

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 4f);
        textRect.offsetMax = new Vector2(-12f, -4f);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 22f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return button;
    }

    public void StartDialogue(NPCInteractable npc)
    {
        if (npc == null) return;
        currentNPC = npc;
        lineIndex = 0;
        IsSpeaking = true;
        nameText.text = npc.npcName;
        bodyText.text = npc.dialogueLines != null && npc.dialogueLines.Length > 0 ? npc.dialogueLines[0] : "...";
        bodyText.rectTransform.offsetMin = new Vector2(20f, 90f);
        nameText.gameObject.SetActive(true);
        bodyText.gameObject.SetActive(true);
        skipButton.gameObject.SetActive(npc.workshopTrigger != null);
        nextLineButton.gameObject.SetActive(true);
        choicesRoot.SetActive(false);
        panel.SetActive(true);
        CaptureModalInput();
        PlayClickSFX();
    }

    public void AdvanceLine()
    {
        if (!IsSpeaking || currentNPC == null) return;
        if (choicesRoot != null && choicesRoot.activeSelf) return;

        lineIndex++;
        if (currentNPC.dialogueLines != null && lineIndex < currentNPC.dialogueLines.Length)
        {
            bodyText.text = currentNPC.dialogueLines[lineIndex];
            PlayClickSFX();
        }
        else
        {
            ShowPostDialogueChoices();
        }
    }

    public bool IsConversationWith(NPCInteractable npc)
    {
        return IsSpeaking && currentNPC == npc;
    }

    private void SkipToChoices()
    {
        if (!IsSpeaking || currentNPC == null || currentNPC.workshopTrigger == null) return;
        ShowPostDialogueChoices();
    }

    private void ShowPostDialogueChoices()
    {
        if (!IsSpeaking || currentNPC == null) return;
        if (currentNPC.workshopTrigger == null)
        {
            EndDialogue();
            return;
        }

        // Keep the modal active so unrelated NPCs and interactables cannot
        // start another interaction while the two choices are on screen.
        nameText.gameObject.SetActive(false);
        bodyText.gameObject.SetActive(false);
        skipButton.gameObject.SetActive(false);
        nextLineButton.gameObject.SetActive(false);
        choicesRoot.SetActive(true);
        panel.SetActive(true);
        PlayClickSFX();
    }

    private void ChooseWorkshop()
    {
        if (!IsSpeaking || currentNPC == null) return;
        MinigameTrigger target = currentNPC.workshopTrigger;
        if (target == null || target.minigameUI == null)
        {
            Debug.LogError("[DialogueUIManager] Chưa gán MinigameTrigger hoặc workshop UI cho NPC hướng dẫn viên.");
            return;
        }
        EndDialogue();
        target.OpenMinigame();
    }

    private void ContinueTour()
    {
        if (IsSpeaking) EndDialogue();
    }

    public void EndDialogue()
    {
        currentNPC = null;
        IsSpeaking = false;
        if (panel != null) panel.SetActive(false);
        if (choicesRoot != null) choicesRoot.SetActive(false);
        RestoreModalInput();
    }

    public void CancelDialogue(NPCInteractable npc)
    {
        if (IsSpeaking && currentNPC == npc) EndDialogue();
    }

    private void CaptureModalInput()
    {
        if (modalInputCaptured) return;
        modalInputCaptured = true;
        ViewModeController.PauseGameplayForModal();
    }

    private void RestoreModalInput()
    {
        if (!modalInputCaptured) return;
        modalInputCaptured = false;
        ViewModeController.TryResumeGameplayIfClear();
    }

    public void SetPromptActive(bool active)
    {
        // Compatibility API: interaction hints are intentionally hidden on all platforms.
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
    }

    private void PlayClickSFX()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.inspectClip != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.inspectClip);
        }
    }
}
