using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class DialogueUIManager : MonoBehaviour
{
    public static DialogueUIManager Instance;
    public bool IsSpeaking { get; private set; }
    private enum DialogueState { Closed, LegacyLines, LegacyChoices, Questions, Answer }
    private DialogueState state;
    private GameObject panel, choicesRoot, questionRoot;
    private TextMeshProUGUI nameText, bodyText;
    private Button skipButton, workshopButton, continueButton, nextLineButton;
    private readonly List<Button> questionButtons = new List<Button>();
    private NPCInteractable currentNPC;
    private NPCDialogueData activeData;
    private DialogueOption selectedOption;
    private int lineIndex, promptRequests;
    private bool modalInputCaptured;
    private float nextActionAt;
    private CanvasScaler scaler;
    private RectTransform canvasRect, contentRect;
    private ScrollRect scroll;
    private Vector2 lastScreenSize;
    private Rect lastSafeArea;

    public static DialogueUIManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        return new GameObject("Dialogue UI Manager").AddComponent<DialogueUIManager>();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        if (Instance == this && panel != null) return;
        Instance = this;
        VRUIInputBridge.EnsureInstance();
        BuildUI();
    }
    private void OnEnable() => SceneManager.activeSceneChanged += OnSceneChanged;
    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
        EndDialogue();
    }
    private void OnSceneChanged(Scene previous, Scene next) => EndDialogue();
    private void OnDestroy()
    {
        EndDialogue();
        if (Instance == this) Instance = null;
    }
    private void Update()
    {
        if (IsSpeaking && (currentNPC == null || !currentNPC.isActiveAndEnabled)) EndDialogue();
        if (lastScreenSize != new Vector2(Screen.width, Screen.height) || lastSafeArea != Screen.safeArea)
            RefreshLayout();
    }
    private static RectTransform RectObject(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = LayerMask.NameToLayer("UI");
        rect.SetParent(parent, false);
        return rect;
    }
    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = max;
    }
    private void BuildUI()
    {
        canvasRect = RectObject("DialogueUI", transform);
        var canvas = canvasRect.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasRect.gameObject.AddComponent<GraphicRaycaster>();
        var panelRect = RectObject("DialoguePanel", canvasRect);
        panel = panelRect.gameObject;
        panel.AddComponent<Image>().color = UiTheme.PanelBg;
        nameText = CreateText(panelRect, "NPCName", 30, UiTheme.TitleGold);
        nameText.fontStyle = FontStyles.Bold;
        var nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0, 1);
        nameRect.anchorMax = Vector2.one;
        nameRect.offsetMin = new Vector2(24, -76);
        nameRect.offsetMax = new Vector2(-24, -16);
        var viewport = RectObject("DialogueViewport", panelRect);
        Stretch(viewport, new Vector2(24, 98), new Vector2(-24, -84));
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35;
        contentRect = RectObject("DialogueContent", viewport);
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = Vector2.zero;
        ConfigureStack(contentRect.gameObject);
        contentRect.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRect;
        bodyText = CreateText(contentRect, "DialogueText", 26, UiTheme.TextWhite);
        bodyText.gameObject.AddComponent<LayoutElement>().minHeight = 64;
        questionRoot = RectObject("PlayerQuestions", contentRect).gameObject;
        ConfigureStack(questionRoot);
        nextLineButton = CreateButton(contentRect, "NextDialogueLine", "Tiếp tục", UiTheme.BtnConfirm);
        nextLineButton.onClick.AddListener(AdvanceLine);
        skipButton = CreateButton(contentRect, "SkipDialogue", "Tua nhanh", UiTheme.BtnConfirm);
        skipButton.onClick.AddListener(SkipToChoices);
        choicesRoot = RectObject("GuideChoices", contentRect).gameObject;
        ConfigureStack(choicesRoot);
        workshopButton = CreateButton(choicesRoot.transform, "WorkshopChoice", "Vào workshop", UiTheme.BtnAccent);
        workshopButton.onClick.AddListener(ChooseWorkshop);
        // Outside the scroll viewport: always reachable with malformed/long content.
        continueButton = CreateButton(panelRect, "ContinueTourChoice", "Tiếp tục tham quan", UiTheme.BtnConfirm);
        var exitRect = continueButton.GetComponent<RectTransform>();
        exitRect.anchorMin = Vector2.zero;
        exitRect.anchorMax = new Vector2(1, 0);
        exitRect.pivot = new Vector2(0.5f, 0);
        exitRect.offsetMin = new Vector2(24, 20);
        exitRect.offsetMax = new Vector2(-24, 84);
        continueButton.onClick.AddListener(ContinueTour);
        RefreshLayout();
        panel.SetActive(false);
    }
    private static void ConfigureStack(GameObject root)
    {
        var layout = root.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }
    private static TextMeshProUGUI CreateText(Transform parent, string name, float size, Color color)
    {
        var text = RectObject(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        return text;
    }
    private Button CreateButton(Transform parent, string name, string label, Color color)
    {
        var rect = RectObject(name, parent);
        rect.gameObject.AddComponent<Image>();
        var button = rect.gameObject.AddComponent<Button>();
        UiTheme.ApplyButton(button, color);
        // TMP measures width before the stack allocates height, allowing wrapped labels.
        rect.gameObject.AddComponent<LayoutElement>().minHeight = 64;
        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 12, 12);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var text = CreateText(rect, "Label", 24, UiTheme.TextWhite);
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }
    private static void SetLabel(Button button, string label)
        => button.GetComponentInChildren<TextMeshProUGUI>().text = label;
    private void RefreshLayout()
    {
        if (panel == null) return;
        lastScreenSize = new Vector2(Screen.width, Screen.height);
        lastSafeArea = Screen.safeArea;
        ApplyDialogueLayout(Screen.safeArea, lastScreenSize);
    }
    private void ApplyDialogueLayout(Rect safe, Vector2 screenSize)
    {
        bool portrait = screenSize.y > screenSize.x;
        scaler.referenceResolution = portrait ? new Vector2(720, 1280) : new Vector2(1280, 720);
        scaler.matchWidthOrHeight = portrait ? 0 : 1;
        Canvas.ForceUpdateCanvases();
        float width = Mathf.Max(1, screenSize.x), height = Mathf.Max(1, screenSize.y);
        if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, width, height);
        var rect = panel.GetComponent<RectTransform>();
        float canvasWidth = canvasRect.rect.width, canvasHeight = canvasRect.rect.height;
        rect.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
        rect.anchorMax = new Vector2(safe.xMax / width, safe.yMin / height);
        float inset = Mathf.Max(16, (safe.width / width * canvasWidth - 1000) / 2);
        float panelHeight = Mathf.Min(portrait ? 640 : 480, safe.height / height * canvasHeight - 32);
        rect.offsetMin = new Vector2(inset, 16);
        rect.offsetMax = new Vector2(-inset, 16 + Mathf.Max(200, panelHeight));
        RebuildContent();
    }
    private void RebuildContent()
    {
        if (contentRect == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        scroll.verticalNormalizedPosition = 1;
    }
    public void StartDialogue(NPCInteractable npc)
    {
        if (!isActiveAndEnabled || npc == null || !npc.isActiveAndEnabled || IsSpeaking) return;
        currentNPC = npc;
        activeData = npc.dialogueData;
        selectedOption = null;
        lineIndex = 0;
        IsSpeaking = true;
        nameText.text = npc.npcName;
        nameText.gameObject.SetActive(true);
        bodyText.gameObject.SetActive(true);
        panel.SetActive(true);
        choicesRoot.SetActive(false);
        SetLabel(continueButton, activeData != null ? activeData.ExitLabel : "Tiếp tục tham quan");
        CaptureModalInput();
        nextActionAt = Application.isPlaying ? Time.unscaledTime + 0.15f : 0;
        if (activeData != null) ShowQuestions();
        else
        {
            state = DialogueState.LegacyLines;
            questionRoot.SetActive(false);
            bodyText.text = npc.dialogueLines != null && npc.dialogueLines.Length > 0 ? npc.dialogueLines[0] : "...";
            skipButton.gameObject.SetActive(npc.workshopTrigger != null);
            nextLineButton.gameObject.SetActive(true);
            SetLabel(nextLineButton, "Tiếp tục");
        }
        RefreshLayout();
        PlayClickSFX();
    }
    private bool AcceptAction()
    {
        if (!IsSpeaking || currentNPC == null || !currentNPC.isActiveAndEnabled) return false;
        if (Application.isPlaying && Time.unscaledTime < nextActionAt) return false;
        if (Application.isPlaying) nextActionAt = Time.unscaledTime + 0.15f;
        return true;
    }
    private void ShowQuestions()
    {
        state = DialogueState.Questions;
        selectedOption = null;
        bodyText.text = string.IsNullOrWhiteSpace(activeData.opening) ? "Bạn muốn hỏi điều gì?" : activeData.opening;
        skipButton.gameObject.SetActive(false);
        nextLineButton.gameObject.SetActive(false);
        choicesRoot.SetActive(false);
        questionRoot.SetActive(true);
        foreach (Button button in questionButtons)
        {
            button.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(button.gameObject);
            else DestroyImmediate(button.gameObject);
        }
        questionButtons.Clear();
        if (activeData.options != null)
            foreach (DialogueOption option in activeData.options)
            {
                if (option == null || !option.IsValid) continue;
                var captured = option;
                var button = CreateButton(questionRoot.transform, "PlayerQuestion", option.question,
                    option.action == DialogueAction.EnterWorkshop ? UiTheme.BtnAccent : UiTheme.BtnConfirm);
                button.onClick.AddListener(() => SelectQuestion(captured));
                questionButtons.Add(button);
            }
        RebuildContent();
    }
    private void SelectQuestion(DialogueOption option)
    {
        if (state != DialogueState.Questions || !AcceptAction()) return;
        selectedOption = option;
        state = DialogueState.Answer;
        bodyText.text = option.answer;
        questionRoot.SetActive(false);
        nextLineButton.gameObject.SetActive(true);
        SetLabel(nextLineButton, option.action == DialogueAction.EnterWorkshop ? "Vào workshop"
            : option.action == DialogueAction.EndConversation ? "Tiếp tục tham quan" : "Hỏi câu khác");
        RebuildContent();
        PlayClickSFX();
    }
    public void AdvanceLine()
    {
        // E cannot select a question or advance twice when UI and gameplay share a press.
        if (state != DialogueState.Answer && state != DialogueState.LegacyLines) return;
        if (!AcceptAction()) return;
        if (state == DialogueState.Answer)
        {
            switch (selectedOption.action)
            {
                case DialogueAction.AskAnother: ShowQuestions(); break;
                case DialogueAction.EndConversation: EndDialogue(); break;
                case DialogueAction.EnterWorkshop: OpenWorkshop(); break;
            }
        }
        else if (currentNPC.dialogueLines != null && ++lineIndex < currentNPC.dialogueLines.Length)
            bodyText.text = currentNPC.dialogueLines[lineIndex];
        else ShowPostDialogueChoices();
        RebuildContent();
        PlayClickSFX();
    }
    public bool IsConversationWith(NPCInteractable npc) => IsSpeaking && currentNPC == npc;
    private void SkipToChoices()
    {
        if (state == DialogueState.LegacyLines && currentNPC != null && currentNPC.workshopTrigger != null && AcceptAction())
            ShowPostDialogueChoices();
    }
    private void ShowPostDialogueChoices()
    {
        if (currentNPC.workshopTrigger == null) { EndDialogue(); return; }
        state = DialogueState.LegacyChoices;
        bodyText.text = "Bạn muốn vào workshop hay tiếp tục tham quan?";
        skipButton.gameObject.SetActive(false);
        nextLineButton.gameObject.SetActive(false);
        choicesRoot.SetActive(true);
        RebuildContent();
    }
    private void ChooseWorkshop()
    {
        if (state == DialogueState.LegacyChoices && AcceptAction()) OpenWorkshop();
    }
    private void OpenWorkshop()
    {
        MinigameTrigger target = currentNPC.workshopTrigger;
        if (target == null || !target.isActiveAndEnabled || target.minigameUI == null)
        {
            bodyText.text = "Workshop hiện chưa sẵn sàng. Bạn có thể tiếp tục tham quan.";
            RebuildContent();
            return;
        }
        EndDialogue();
        target.OpenMinigame();
    }
    private void ContinueTour() { if (AcceptAction()) EndDialogue(); }
    public void EndDialogue()
    {
        currentNPC = null;
        activeData = null;
        selectedOption = null;
        state = DialogueState.Closed;
        IsSpeaking = false;
        if (panel != null) panel.SetActive(false);
        if (choicesRoot != null) choicesRoot.SetActive(false);
        RestoreModalInput();
    }
    public void CancelDialogue(NPCInteractable npc) { if (IsConversationWith(npc)) EndDialogue(); }
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
    public void SetPromptActive(bool active) { } // Compatibility: interaction hints are hidden.
    public void RequestPrompt(bool request) => promptRequests = Mathf.Max(0, promptRequests + (request ? 1 : -1));
    private void PlayClickSFX()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.inspectClip != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.inspectClip);
    }
}
