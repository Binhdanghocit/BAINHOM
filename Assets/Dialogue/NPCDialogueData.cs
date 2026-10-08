using System;
using UnityEngine;

public enum DialogueAction { AskAnother, EndConversation, EnterWorkshop }

[Serializable]
public class DialogueOption
{
    public string question;
    [TextArea(2, 5)] public string answer;
    [Tooltip("Thực hiện sau khi người chơi đọc câu trả lời và bấm nút xác nhận.")]
    public DialogueAction action;

    public bool IsValid => !string.IsNullOrWhiteSpace(question) && !string.IsNullOrWhiteSpace(answer)
        && Enum.IsDefined(typeof(DialogueAction), action);
}

[CreateAssetMenu(fileName = "NPCDialogue", menuName = "Gallery/NPC Dialogue")]
public class NPCDialogueData : ScriptableObject
{
    [TextArea(2, 5)] public string opening;
    public DialogueOption[] options;
    [Tooltip("Nút thoát luôn có, kể cả khi dữ liệu thiếu hoặc không hợp lệ.")]
    public string exitLabel = "Tiếp tục tham quan";
    public string ExitLabel => string.IsNullOrWhiteSpace(exitLabel) ? "Tiếp tục tham quan" : exitLabel;
}
