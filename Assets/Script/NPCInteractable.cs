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
    private int lastInteractFrame = -1;

    private void Start()
    {
        outline = GetComponent<InteractableOutline>();
    }

    private void Update()
    {
        if (!isPlayerNearby || DialogueUIManager.Instance == null) return;

        bool pressed = Input.GetKeyDown(KeyCode.E) || HandTriggerInput.WasPressedThisFrame();
        if (!pressed) return;

        TriggerDialogue();
    }

    public void TriggerDialogue()
    {
        if (lastInteractFrame == Time.frameCount) return;
        lastInteractFrame = Time.frameCount;

        if (DialogueUIManager.Instance == null || IsOtherUIOpen()) return;

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
        if (MinigameTrigger.IsAnyOpen) return true;
        if (DoorMenuTrigger.IsAnyOpen) return true;
        if (ExitToExteriorUI.IsAnyOpen) return true;
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
            if (DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsSpeaking)
            {
                DialogueUIManager.Instance.EndDialogue();
            }
        }
    }
}