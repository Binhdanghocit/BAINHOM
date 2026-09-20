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
        promptText.text = "Nhấn E / Click để tương tác";
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