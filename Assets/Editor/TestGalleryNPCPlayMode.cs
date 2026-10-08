using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Real saved-scene lifecycle/physics and pointer callbacks. No physical HMD/touch claim.
public static class TestGalleryNPCPlayMode
{
    private const string Flag = "BAINHOM.NPCReview.PlayMode";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> results = new List<string>();
    private static readonly List<Action> steps = new List<Action>();
    private static int stage;
    private static double startedAt, nextStepAt;
    private static PlayerController player;
    private static PlayerInteraction interaction;
    private static ThirdPersonCamera cameraController;
    private static DialogueUIManager dialogue;
    private static NPCInteractable guide, visitor;
    private static Collider extraCollider;
    private static ColoringPageMinigame coloring;
    private static readonly string[] Names = { "GalleryGuide", "GalleryVisitor", "GalleryVisitor_Floor1", "GalleryVisitor_Floor2" };
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }

    public static void Begin()
    {
        try
        {
            Directory.CreateDirectory("Logs/DialogueReview");
            TestGameplayRegressionSuite.RunAllTests(); // Legacy dialogue/guide remain covered.
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SetupGalleryNPCTool.ValidateSavedGallery();
            CheckSetupPreservesEdits();
            SessionState.SetBool(Flag, true);
            ResumeIfRequested();
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Finish(error); }
    }
    public static void BeginLayout()
    {
        Directory.CreateDirectory("Logs/DialogueReview");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool("BAINHOM.NPCReview.LayoutOnly", true);
        SessionState.SetBool(Flag, true);
        ResumeIfRequested();
        EditorApplication.EnterPlaymode();
    }

    private static void CheckSetupPreservesEdits()
    {
        byte[] scene = File.ReadAllBytes("Assets/Scenes/SampleScene.unity");
        var snapshots = Names.ToDictionary(n => n, n => File.ReadAllBytes("Assets/Prefabs/" + n + ".prefab"));
        var npcs = Names.Select(n => GameObject.Find(n).GetComponent<NPCInteractable>()).ToArray();
        var data = npcs.Select(n => n.dialogueData).ToArray();
        Check(data.Distinct().Count() == Names.Length, "Four NPCs own distinct dialogue assets");
        string opening = data[0].opening, answer = data[0].options[0].answer;
        var body = npcs[0].interactionCollider;
        var visual = npcs[0].transform.Find("Character Visual");
        var position = npcs[0].transform.position;
        var workshop = npcs[0].workshopTrigger;
        var custom = ScriptableObject.CreateInstance<NPCDialogueData>();
        data[0].opening = "Opening edited in Inspector";
        data[0].options[0].answer = "Answer edited in Inspector";
        npcs[1].dialogueData = custom; // A custom assignment is also preserved.
        npcs[0].transform.position += Vector3.right;
        npcs[2].gameObject.SetActive(false);
        try
        {
            for (int run = 0; run < 2; run++)
            {
                SetupGalleryNPCTool.ConfigureNPCs();
                Check(data[0].opening == "Opening edited in Inspector" && data[0].options[0].answer == "Answer edited in Inspector",
                    "Repeated setup preserves edited content: " + run);
                Check(npcs[1].dialogueData == custom && npcs[0].interactionCollider == body
                    && npcs[0].transform.Find("Character Visual") == visual && npcs[0].workshopTrigger == workshop
                    && npcs[0].transform.position == position + Vector3.right, "Setup preserves assignment/model/collider/position/workshop: " + run);
                Check(Names.All(n => File.ReadAllBytes("Assets/Prefabs/" + n + ".prefab").SequenceEqual(snapshots[n])),
                    "Setup never rewrites configured prefabs: " + run);
                Check(UnityEngine.Object.FindObjectsByType<NPCInteractable>(FindObjectsInactive.Include).Count(n => n.name == Names[2]) == 1
                    && !npcs[2].gameObject.activeSelf, "Setup does not duplicate inactive NPC: " + run);
            }
        }
        finally
        {
            data[0].opening = opening;
            data[0].options[0].answer = answer;
            UnityEngine.Object.DestroyImmediate(custom);
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        }
        Check(File.ReadAllBytes("Assets/Scenes/SampleScene.unity").SequenceEqual(scene), "Tests preserve saved scene and bake");
        File.WriteAllLines("Logs/DialogueReview/AuthoringResults.txt", results);
    }

    [InitializeOnLoadMethod]
    private static void ResumeIfRequested()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }
    private static void Click(Button button)
    {
        Check(button != null && button.IsActive() && button.IsInteractable(), "Pointer target is usable");
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }
    private static Button Question(int index) => Get<List<Button>>(dialogue, "questionButtons")[index];
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
    private static void BuildSteps()
    {
        guide = GameObject.Find(Names[0]).GetComponent<NPCInteractable>();
        visitor = GameObject.Find(Names[1]).GetComponent<NPCInteractable>();
        player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        interaction = player.GetComponent<PlayerInteraction>();
        cameraController = Camera.main.GetComponent<ThirdPersonCamera>();
        dialogue = DialogueUIManager.Instance;
        Check(dialogue != null && interaction != null && guide.workshopTrigger != null, "Scene interaction and workshop initialize");
        if (SessionState.GetBool("BAINHOM.NPCReview.LayoutOnly", false)) { AddLayoutSteps(); return; }
        foreach (string name in Names)
        {
            var npc = GameObject.Find(name).GetComponent<NPCInteractable>();
            steps.Add(() => MoveNear(npc));
            steps.Add(() => dialogue.StartDialogue(npc));
            steps.Add(() =>
            {
                Check(dialogue.IsConversationWith(npc) && !player.enabled, name + ": opening locks movement");
                Check(Get<List<Button>>(dialogue, "questionButtons").Count == npc.dialogueData.options.Length,
                    name + ": every authored question is visible");
                Check(Get<Button>(dialogue, "continueButton").IsActive(), name + ": exit is always available");
                dialogue.AdvanceLine();
                Check(Get<GameObject>(dialogue, "questionRoot").activeSelf, "E does not select a question");
            });
            for (int i = 0; i < npc.dialogueData.options.Length; i++)
            {
                int branch = i;
                var option = npc.dialogueData.options[i];
                steps.Add(() =>
                {
                    Click(Question(branch));
                    // Deliberately deliver a second input in the same frame.
                    dialogue.AdvanceLine();
                    Check(Get<TextMeshProUGUI>(dialogue, "bodyText").text == option.answer
                        && !Get<GameObject>(dialogue, "questionRoot").activeSelf, name + ": answer survives repeated press, branch " + branch);
                });
                steps.Add(() => Click(Get<Button>(dialogue, "nextLineButton")));
                if (option.action == DialogueAction.AskAnother)
                    steps.Add(() => Check(Get<GameObject>(dialogue, "questionRoot").activeSelf && dialogue.IsSpeaking,
                        name + ": answer returns to questions"));
                else if (option.action == DialogueAction.EnterWorkshop)
                {
                    steps.Add(() =>
                    {
                        Check(!dialogue.IsSpeaking && npc.workshopTrigger.IsMinigameOpen && !player.enabled, "Workshop handoff owns modal lock");
                        coloring = npc.workshopTrigger.minigameUI.GetComponentInChildren<ColoringPageMinigame>(true);
                        Check(coloring != null && coloring.coloringImage.texture != null, "Workshop loads actual artwork");
                        Click(coloring.optionsButton);
                    });
                    steps.Add(() => Click(coloring.returnToGalleryButton));
                    steps.Add(() =>
                    {
                        Check(!MinigameTrigger.IsAnyOpen && player.enabled, "Workshop return restores gameplay");
                        npc.TriggerDialogue();
                        Check(dialogue.IsConversationWith(npc) && Get<GameObject>(dialogue, "questionRoot").activeSelf,
                            "Guide can be spoken to again after workshop");
                    });
                }
                else steps.Add(() => Check(!dialogue.IsSpeaking && player.enabled, name + ": farewell restores gameplay"));
            }
        }
        steps.Add(() => MoveNear(visitor));
        steps.Add(() =>
        {
            interaction.TryInteract();
            Check(dialogue.IsConversationWith(visitor), "Ray interaction opens converted visitor");
            var hand = new GameObject("Extra player collider", typeof(BoxCollider));
            hand.transform.SetParent(player.transform, false);
            hand.transform.localPosition = new Vector3(0.15f, 0.8f, 0);
            extraCollider = hand.GetComponent<Collider>();
        });
        steps.Add(() =>
        {
            Check(Get<HashSet<Collider>>(visitor, "playerCollidersInRange").Count >= 2, "NPC tracks multiple player colliders");
            extraCollider.enabled = false;
        });
        steps.Add(() =>
        {
            Check(dialogue.IsConversationWith(visitor), "Disabling one collider preserves conversation");
            var settings = UnityEngine.Object.FindAnyObjectByType<SettingsManager>();
            settings.OpenPanel();
            visitor.enabled = false;
            Check(!dialogue.IsSpeaking && !player.enabled, "NPC disable cancels dialogue while Settings keeps lock");
            settings.ClosePanel();
            Check(player.enabled, "Final modal close resumes gameplay");
            visitor.enabled = true;
        });
        steps.Add(() => { dialogue.StartDialogue(visitor); visitor.gameObject.SetActive(false); Check(!dialogue.IsSpeaking && player.enabled, "Inactive NPC closes UI"); visitor.gameObject.SetActive(true); });
        steps.Add(() => { dialogue.StartDialogue(visitor); UnityEngine.Object.Destroy(visitor.gameObject); });
        steps.Add(() => Check(!dialogue.IsSpeaking && player.enabled, "Destroyed NPC closes UI and resumes gameplay"));
        steps.Add(() => { dialogue.StartDialogue(guide); dialogue.enabled = false; Check(!dialogue.IsSpeaking && player.enabled, "Disabled manager releases modal"); dialogue.enabled = true; });
        steps.Add(() =>
        {
            dialogue.StartDialogue(guide);
            var original = SceneManager.GetActiveScene();
            var other = SceneManager.CreateScene("NPC transition fixture");
            SceneManager.SetActiveScene(other);
            Check(!dialogue.IsSpeaking && player.enabled, "Scene transition cancels active dialogue");
            SceneManager.SetActiveScene(original);
            SceneManager.UnloadSceneAsync(other);
        });
        AddLayoutSteps();
    }
    private static void AddLayoutSteps()
    {
        foreach (string npcName in Names)
        {
            // The lifecycle test destroys one visitor; use a temporary prefab for rendering it.
            steps.Add(() =>
            {
                var npc = GameObject.Find(npcName)?.GetComponent<NPCInteractable>();
                if (npc == null)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + npcName + ".prefab");
                    npc = UnityEngine.Object.Instantiate(prefab).GetComponent<NPCInteractable>();
                }
                dialogue.EndDialogue();
                dialogue.StartDialogue(npc);
            });
            steps.Add(() => CheckLayoutAndCapture(npcName + "-PC", new Vector2Int(1280, 720)));
            steps.Add(() => CheckLayoutAndCapture(npcName + "-Mobile", new Vector2Int(720, 1280)));
        }
        steps.Add(() => dialogue.EndDialogue());
        steps.Add(() => { MoveNear(guide); dialogue.StartDialogue(guide); });
        steps.Add(() => CheckLayoutAndCapture("PC-questions", new Vector2Int(1280, 720)));
        steps.Add(() => Click(Question(0)));
        steps.Add(() => CheckLayoutAndCapture("PC-answer", new Vector2Int(1280, 720)));
        steps.Add(() => Click(Get<Button>(dialogue, "nextLineButton")));
        steps.Add(() => CheckLayoutAndCapture("Mobile-questions", new Vector2Int(720, 1280)));
        steps.Add(() => Click(Question(0)));
        steps.Add(() => CheckLayoutAndCapture("Mobile-answer", new Vector2Int(720, 1280)));
        steps.Add(() => Click(Get<Button>(dialogue, "continueButton")));
    }
    private static void CheckLayoutAndCapture(string name, Vector2Int size)
    {
        var canvas = dialogue.GetComponentInChildren<Canvas>();
        var scaler = canvas.GetComponent<CanvasScaler>();
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        var oldScaleMode = scaler.uiScaleMode;
        float oldScale = scaler.scaleFactor;
        var camera = new GameObject("Dialogue render camera", typeof(Camera)).GetComponent<Camera>();
        camera.enabled = false;
        camera.cullingMask = 1 << canvas.gameObject.layer;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
        var target = new RenderTexture(size.x, size.y, 24);
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = size.y > size.x ? size.x / 720f : size.y / 720f;
        typeof(CanvasScaler).GetMethod("Handle", Private).Invoke(scaler, null);
        var safe = new Rect(0, 32, size.x, size.y - 64);
        try
        {
        Canvas.ForceUpdateCanvases();
        typeof(DialogueUIManager).GetMethod("ApplyDialogueLayout", Private).Invoke(dialogue, new object[] { safe, (Vector2)size });
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
        image.Apply();
        File.WriteAllBytes("Logs/DialogueReview/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = previous;
        UnityEngine.Object.Destroy(image);
        results.Add("IMAGE: " + name + " at " + size);
        var panel = Get<GameObject>(dialogue, "panel").GetComponent<RectTransform>();
        var rect = panel.rect;
        foreach (var text in panel.GetComponentsInChildren<TextMeshProUGUI>())
        {
            text.ForceMeshUpdate();
            Check(text.preferredHeight <= text.rectTransform.rect.height + 2, name + ": text fits " + text.gameObject.name);
        }
        var close = Get<Button>(dialogue, "continueButton");
        Vector3[] corners = new Vector3[4];
        close.GetComponent<RectTransform>().GetWorldCorners(corners);
        Check(corners.All(p => rect.Contains(panel.InverseTransformPoint(p))), name + ": exit lies inside panel");
        panel.GetWorldCorners(corners);
        Check(corners.All(p => safe.Contains(RectTransformUtility.WorldToScreenPoint(camera, p))), name + ": panel lies inside safe area");
        foreach (var button in Get<List<Button>>(dialogue, "questionButtons").Where(b => b.IsActive()))
        {
            button.GetComponent<RectTransform>().GetWorldCorners(corners);
            var viewport = Get<ScrollRect>(dialogue, "scroll").viewport;
            var bounds = viewport.rect;
            var points = corners.Select(p => (Vector2)viewport.InverseTransformPoint(p)).ToArray();
            Check(points.All(p => p.x >= bounds.xMin - 1 && p.x <= bounds.xMax + 1 && p.y >= bounds.yMin - 1 && p.y <= bounds.yMax + 1),
                name + ": question fits viewport " + bounds + ", corners " + string.Join(",", points));
        }
        var position = RectTransformUtility.WorldToScreenPoint(camera, close.transform.TransformPoint(close.GetComponent<RectTransform>().rect.center));
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        Check(hits.Any(h => h.gameObject == close.gameObject), name + ": GraphicRaycaster exposes exit to mouse/touch/XR bridge");
        }
        finally
        {
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            scaler.uiScaleMode = oldScaleMode;
            scaler.scaleFactor = oldScale;
            typeof(CanvasScaler).GetMethod("Handle", Private).Invoke(scaler, null);
            typeof(DialogueUIManager).GetMethod("RefreshLayout", Private).Invoke(dialogue, null);
            camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(camera.gameObject);
        }
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (startedAt == 0) { startedAt = EditorApplication.timeSinceStartup; nextStepAt = startedAt + 2; }
        if (EditorApplication.timeSinceStartup - startedAt > 160) { Finish(new TimeoutException("NPC test timed out")); return; }
        if (EditorApplication.timeSinceStartup < nextStepAt) return;
        nextStepAt = EditorApplication.timeSinceStartup + 0.4;
        try
        {
            if (steps.Count == 0) BuildSteps();
            if (stage >= steps.Count) { Finish(null); return; }
            steps[stage++]();
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        bool layoutOnly = SessionState.GetBool("BAINHOM.NPCReview.LayoutOnly", false);
        SessionState.SetBool(Flag, false);
        SessionState.SetBool("BAINHOM.NPCReview.LayoutOnly", false);
        Directory.CreateDirectory("Logs/DialogueReview");
        results.Add(error == null ? "RESULT: PASS; " + (layoutOnly ? "layout/raycast render" : "all branches, workshop return, modal lifecycle, layout/raycast") + ". Physical mobile/HMD unverified."
            : "RESULT: FAIL at stage " + stage + "\n" + error);
        File.WriteAllLines("Logs/DialogueReview/NPCPlayModeResults.txt", results);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
