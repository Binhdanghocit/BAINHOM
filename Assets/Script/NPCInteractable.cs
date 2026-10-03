using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(InteractableOutline))]
public class NPCInteractable : MonoBehaviour
{
    [Header("Thông tin NPC")]
    public string npcName = "Người dân";

    [Tooltip("Các dòng hội thoại, hiện lần lượt mỗi lần bấm E")]
    public string[] dialogueLines;
    [Tooltip("Collider thân nhân vật dùng để chọn bằng ray. Để trống giữ hành vi cũ; không chọn volume phát hiện khoảng cách.")]
    public Collider interactionCollider;

    public bool IsInteractionCollider(Collider collider) => interactionCollider == null || interactionCollider == collider;

    [Header("Tùy chọn NPC hướng dẫn")]
    [Tooltip("Chỉ gán cho hướng dẫn viên. Sau hội thoại, người chơi được chọn vào workshop hoặc tiếp tục tham quan.")]
    public MinigameTrigger workshopTrigger;

    private bool isPlayerNearby = false;
    private InteractableOutline outline;
    private int lastInteractFrame = -1;
    private readonly HashSet<Collider> playerCollidersInRange = new HashSet<Collider>();

    private void Awake()
    {
        outline = GetComponent<InteractableOutline>();
        DialogueUIManager.EnsureInstance();
    }

    private void Start()
    {
        outline = GetComponent<InteractableOutline>();
        UpdateNearbyState();
    }



    public void TriggerDialogue()
    {
        if (!isActiveAndEnabled) return;
        if (lastInteractFrame == Time.frameCount) return;
        lastInteractFrame = Time.frameCount;

        if (DialogueUIManager.Instance == null || IsOtherUIOpen()) return;

        if (DialogueUIManager.Instance.IsSpeaking)
        {
            if (DialogueUIManager.Instance.IsConversationWith(this))
                DialogueUIManager.Instance.AdvanceLine();
        }
        else
        {
            DialogueUIManager.Instance.StartDialogue(this);
        }
    }

    private static bool IsOtherUIOpen()
    {
        var settings = Object.FindAnyObjectByType<SettingsManager>();
        if (settings != null && settings.IsSettingsOpen()) return true;
        if (PaintingUIManager.Instance != null && PaintingUIManager.Instance.IsPopupOpen) return true;
        if (MinigameTrigger.IsAnyOpen) return true;
        if (DoorMenuTrigger.IsAnyOpen) return true;
        if (ExitToExteriorUI.IsAnyOpen) return true;
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            if (playerCollidersInRange.Add(other)) UpdateNearbyState();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerDetector.IsPlayer(other))
        {
            if (playerCollidersInRange.Remove(other)) UpdateNearbyState();
        }
    }

    private void OnDisable()
    {
        playerCollidersInRange.Clear();
        UpdateNearbyState();
        if (DialogueUIManager.Instance != null) DialogueUIManager.Instance.CancelDialogue(this);
    }

    private void LateUpdate()
    {
        if (playerCollidersInRange.RemoveWhere(collider => collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) > 0)
            UpdateNearbyState();
    }

    private void UpdateNearbyState()
    {
        playerCollidersInRange.RemoveWhere(collider => collider == null);
        bool wasNearby = isPlayerNearby;
        isPlayerNearby = playerCollidersInRange.Count > 0;
        if (outline != null) outline.SetProximity(isPlayerNearby);

        DialogueUIManager manager = DialogueUIManager.Instance;
        if (wasNearby == isPlayerNearby || manager == null) return;
        // InteractableOutline owns the prompt registration for this target.
        if (!isPlayerNearby) manager.CancelDialogue(this);
    }
}
