using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Batch integration test of the saved scene. Pointer callbacks are simulated;
// this does not claim physical touchscreen/controller or headset rendering coverage.
public static class TestGalleryNPCPlayMode
{
    private const string Flag = "BAINHOM.NPCReview.PlayMode";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> results = new List<string>();
    private static int stage;
    private static double startedAt, nextStepAt;
    private static NPCInteractable visitor, guide;
    private static PlayerController player;
    private static PlayerInteraction interaction;
    private static ThirdPersonCamera cameraController;
    private static SettingsManager settings;
    private static DialogueUIManager dialogue;
    private static Collider extraCollider;
    private static ColoringPageMinigame coloring;

    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Flag, true);
        ResumeIfRequested();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void ResumeIfRequested()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }

    private static void Click(Button button)
    {
        if (button == null || !button.IsActive() || !button.IsInteractable())
            throw new InvalidOperationException("Requested dialogue/workshop button is not usable.");
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left
        }, ExecuteEvents.pointerClickHandler);
    }

    private static void MoveNear(NPCInteractable npc)
    {
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        Vector3 destination = npc.transform.position + npc.transform.forward * 1.4f;
        player.transform.SetPositionAndRotation(destination, Quaternion.LookRotation(npc.transform.position - destination));
        controller.enabled = true;
        cameraController.distance = 0;
        cameraController.currentX = player.transform.eulerAngles.y;
        Physics.SyncTransforms();
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (startedAt == 0)
        {
            startedAt = EditorApplication.timeSinceStartup;
            nextStepAt = startedAt + 2;
        }
        if (EditorApplication.timeSinceStartup - startedAt > 120)
        {
            Finish(new TimeoutException("NPC Play Mode integration test timed out."));
            return;
        }
        if (EditorApplication.timeSinceStartup < nextStepAt) return;
        nextStepAt = EditorApplication.timeSinceStartup + 0.35;
        try
        {
            switch (stage)
            {
                case 0:
                    guide = GameObject.Find("GalleryGuide").GetComponent<NPCInteractable>();
                    visitor = GameObject.Find("GalleryVisitor").GetComponent<NPCInteractable>();
                    player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                    interaction = player.GetComponent<PlayerInteraction>();
                    cameraController = Camera.main.GetComponent<ThirdPersonCamera>();
                    settings = UnityEngine.Object.FindAnyObjectByType<SettingsManager>();
                    dialogue = DialogueUIManager.Instance;
                    Check(visitor.dialogueLines.Length == 3 && guide.dialogueLines.Length == 2 && guide.workshopTrigger != null,
                        "Saved NPC instances and workshop reference load in Play Mode");
                    Check(interaction != null && cameraController != null && dialogue != null, "Scene interaction and dialogue initialize");
                    var hand = new GameObject("NPC test extra player collider", typeof(BoxCollider));
                    hand.transform.SetParent(player.transform, false);
                    hand.transform.localPosition = new Vector3(0.15f, 0.8f, 0);
                    hand.GetComponent<BoxCollider>().size = Vector3.one * 0.15f;
                    extraCollider = hand.GetComponent<Collider>();
                    MoveNear(visitor);
                    break;
                case 1:
                    interaction.TryInteract();
                    break;
                case 2:
                    Check(dialogue.IsConversationWith(visitor) && !player.enabled, "Ray opens the regular NPC and locks WASD");
                    Check(Get<HashSet<Collider>>(visitor, "playerCollidersInRange").Count >= 2, "NPC tracks multiple real player colliders");
                    Click(Get<Button>(dialogue, "nextLineButton"));
                    Check(Get<int>(dialogue, "lineIndex") == 1, "Pointer click advances to line two");
                    extraCollider.enabled = false;
                    break;
                case 3:
                    Check(dialogue.IsConversationWith(visitor) && visitor.GetComponent<InteractableOutline>().IsProximityActive,
                        "Disabling one collider preserves dialogue and outline for the remaining collider");
                    Click(Get<Button>(dialogue, "nextLineButton"));
                    Check(Get<int>(dialogue, "lineIndex") == 2, "Pointer click advances to line three");
                    break;
                case 4:
                    Click(Get<Button>(dialogue, "nextLineButton"));
                    Check(!dialogue.IsSpeaking && player.enabled, "Final line closes dialogue and restores movement");
                    break;
                case 5:
                    interaction.TryInteract();
                    settings.OpenPanel();
                    break;
                case 6:
                    visitor.gameObject.SetActive(false);
                    Check(!dialogue.IsSpeaking && !player.enabled, "Disabling the speaking NPC cancels dialogue while Settings keeps movement locked");
                    Check(!Get<bool>(visitor.GetComponent<InteractableOutline>(), "promptRegistered"), "Disabled NPC releases its prompt registration");
                    settings.ClosePanel();
                    visitor.gameObject.SetActive(true);
                    MoveNear(guide);
                    break;
                case 7:
                    interaction.TryInteract();
                    break;
                case 8:
                    Check(dialogue.IsConversationWith(guide), "Ray selects the guide in the saved scene");
                    Click(Get<Button>(dialogue, "nextLineButton"));
                    break;
                case 9:
                    Click(Get<Button>(dialogue, "nextLineButton"));
                    Check(Get<GameObject>(dialogue, "choicesRoot").activeSelf, "Guide shows both choices after its two lines");
                    Click(Get<Button>(dialogue, "continueButton"));
                    Check(!dialogue.IsSpeaking && !MinigameTrigger.IsAnyOpen && player.enabled, "Continue tour returns to gameplay");
                    break;
                case 10:
                    interaction.TryInteract();
                    break;
                case 11:
                    Click(Get<Button>(dialogue, "nextLineButton"));
                    break;
                case 12:
                    Click(Get<Button>(dialogue, "nextLineButton"));
                    break;
                case 13:
                    Click(Get<Button>(dialogue, "workshopButton"));
                    break;
                case 14:
                    Check(guide.workshopTrigger.IsMinigameOpen && guide.workshopTrigger.minigameUI.activeSelf && !player.enabled,
                        "Guide workshop choice opens its assigned UI and locks gameplay");
                    coloring = guide.workshopTrigger.minigameUI.GetComponentInChildren<ColoringPageMinigame>(true);
                    Check(coloring != null && coloring.coloringImage.texture != null, "Saved workshop initializes an actual painting texture");
                    Click(coloring.optionsButton);
                    break;
                case 15:
                    Click(coloring.returnToGalleryButton);
                    break;
                case 16:
                    Check(!MinigameTrigger.IsAnyOpen && !guide.workshopTrigger.minigameUI.activeSelf && player.enabled,
                        "Workshop return button closes the owning table and restores gameplay");
                    Finish(null);
                    return;
            }
            stage++;
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Flag, false);
        results.Add(error == null ? "RESULT: PASS, all 17 saved-scene Play Mode stages completed. Pointer callbacks simulated; physical mobile/VR input and rendering unverified."
            : "RESULT: FAIL at stage " + stage + "\n" + error);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/NPCPlayModeResults.txt", results);
        if (error != null) Debug.LogException(error);
        else Debug.Log("[NPC Play Mode] All saved-scene stages passed.");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
