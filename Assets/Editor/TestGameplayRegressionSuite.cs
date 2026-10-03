using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Exercises runtime methods in an isolated scene without requiring an XR device.
public static class TestGameplayRegressionSuite
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly List<UnityEngine.Object> resources = new List<UnityEngine.Object>();

    private static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static T Get<T>(object target, string name)
        => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value)
        => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static GameObject Object(string name, params Type[] types) => new GameObject(name, types);

    [MenuItem("Tools/Tests/Run All Review Checks")]
    public static void RunReviewChecks()
    {
        TestColoringWorkshopSuite.RunAllTests(isAutomated: true);
        RunAllTests();
    }

    [MenuItem("Tools/Tests/Run Gameplay Regression Suite")]
    public static void RunAllTests()
    {
        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            Application.isBatchMode && string.IsNullOrEmpty(previousScene.path) ? NewSceneMode.Single : NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        float previousVolume = AudioListener.volume;
        float previousTimeScale = Time.timeScale;
        var logs = new List<string>();
        int failed = 0;
        var tests = new (string name, Action run)[]
        {
            ("Interaction available after desktop/VR rig switch", TestRigInteraction),
            ("VR singleton pointer and release target", TestVRPointer),
            ("Tracking pose and viewport projection", TestVRProjection),
            ("NPC next-line button and nested modal movement lock", TestDialogueAndModal),
            ("Guide choices and workshop close ownership", TestGuideChoices),
            ("NPC prompt ownership, disable and multiple colliders", TestNPCPromptOwnership),
            ("NPC ray ignores another NPC proximity volume", TestNPCBodyRay),
            ("Workshop modal guards and door-to-workshop transition", TestWorkshopGuards),
            ("Popup ownership and multiple player colliders", TestPaintingOwnership),
            ("Crosshair wall occlusion and trigger volumes", TestCrosshair),
            ("Jump hit region follows scaled RectTransform", TestJumpRegion),
            ("Listener reactivation and single master gain", TestAudio),
            ("Largest enclosed coloring region and invalid span fallback", TestColoringFallback)
        };
        try
        {
            foreach (var test in tests)
            {
                try
                {
                    test.run();
                    logs.Add("PASS: " + test.name);
                }
                catch (Exception exception)
                {
                    failed++;
                    logs.Add("FAIL: " + test.name + "\n" + exception);
                }
                finally
                {
                    foreach (GameObject root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
                    foreach (UnityEngine.Object resource in resources) if (resource != null) UnityEngine.Object.DestroyImmediate(resource);
                    resources.Clear();
                    PlayerDetector.ClearRoots();
                }
            }
        }
        finally
        {
            if (previousScene.IsValid()) SceneManager.SetActiveScene(previousScene);
            if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            AudioListener.volume = previousVolume;
            Time.timeScale = previousTimeScale;
        }
        logs.Add($"RESULT: {tests.Length - failed}/{tests.Length} passed. Editor CPU/geometry tests; device rendering and input not verified.");
        Directory.CreateDirectory("Temp");
        File.WriteAllLines("Temp/ReviewGameplayResults.txt", logs);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/ReviewGameplayResults.txt", logs);
        foreach (string log in logs) Debug.Log(log);
        if (failed > 0) throw new Exception($"Gameplay regression suite: {failed} failed.");
    }

    private static Camera Camera(string name)
    {
        var camera = Object(name, typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        return camera;
    }

    private static PaintingUIManager PaintingUI()
    {
        var ui = Object("Painting UI").AddComponent<PaintingUIManager>();
        PaintingUIManager.Instance = ui;
        ui.popupPanel = Object("Popup");
        ui.popupPanel.SetActive(false);
        return ui;
    }

    private static PaintingTrigger Painting(string name, Vector3 position)
    {
        var painting = Object(name, typeof(BoxCollider), typeof(PaintingInfo));
        painting.transform.position = position;
        painting.GetComponent<BoxCollider>().isTrigger = true;
        var trigger = painting.AddComponent<PaintingTrigger>();
        Call(trigger, "Awake");
        return trigger;
    }

    private static void TestRigInteraction()
    {
        var mode = Object("View mode").AddComponent<ViewModeController>();
        mode.desktopPlayerRig = Object("Desktop");
        mode.vrRig = Object("VR");
        MethodInfo ensure = typeof(ViewModeController).GetMethod("EnsureRigInteraction", Private);
        ensure.Invoke(null, new object[] { mode.desktopPlayerRig });
        ensure.Invoke(null, new object[] { mode.vrRig });
        mode.desktopPlayerRig.SetActive(false);
        mode.vrRig.SetActive(true);
        var interaction = mode.vrRig.GetComponent<PlayerInteraction>();
        Check(interaction != null && interaction.isActiveAndEnabled, "VR has no active interaction component.");
        var camera = Camera("VR camera");
        camera.transform.SetParent(mode.vrRig.transform, false);
        var ui = PaintingUI();
        var painting = Painting("Target", Vector3.forward * 2f);
        Physics.SyncTransforms();
        typeof(PlayerInteraction).GetField("lastInteractionFrame", Private).SetValue(null, -1);
        Call(interaction, "HandlePrimaryInteraction");
        Check(ui.IsShowingPainting(painting.GetComponent<PaintingInfo>()), "VR interaction did not open the ray target.");
        Call(interaction, "HandlePrimaryInteraction");
        Check(ui.IsPopupOpen, "The same frame toggled the popup twice.");
        mode.desktopPlayerRig.SetActive(true);
        mode.vrRig.SetActive(false);
        Check(mode.desktopPlayerRig.GetComponent<PlayerInteraction>().isActiveAndEnabled, "Desktop interaction was lost.");
    }

    private static void TestVRPointer()
    {
        var bridge = Object("Pointer").AddComponent<VRUIInputBridge>();
        Call(bridge, "Awake");
        Check(VRUIInputBridge.EnsureInstance() == bridge, "EnsureInstance created a second pointer.");
        var duplicate = Object("Duplicate pointer").AddComponent<VRUIInputBridge>();
        Call(duplicate, "Awake");
        Check(!duplicate.enabled, "Duplicate bridge can emit events.");
        var eventSystem = Object("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        Set(bridge, "pointerSystem", eventSystem);
        Set(bridge, "pointer", new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left });
        Button a = Object("Button A", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
        Button b = Object("Button B", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
        int aClicks = 0, bClicks = 0;
        a.onClick.AddListener(() => aClicks++);
        b.onClick.AddListener(() => bClicks++);
        void Send(GameObject target, bool down) => Call(bridge, "ProcessPointer", Vector2.zero, new RaycastResult { gameObject = target }, down);
        Send(a.gameObject, true);
        Send(a.gameObject, false);
        Check(aClicks == 1 && bClicks == 0, "One press did not produce exactly one click.");
        Send(a.gameObject, true);
        Send(b.gameObject, false);
        Send(a.gameObject, true);
        Send(null, false);
        Check(aClicks == 1 && bClicks == 0, "Release outside the pressed button fired a callback.");
    }

    private static void TestVRProjection()
    {
        var tracking = Object("Tracking space").transform;
        tracking.SetPositionAndRotation(new Vector3(20, 3, -8), Quaternion.Euler(0, 90, 0));
        tracking.localScale = Vector3.one * 2f;
        Vector3 local = new Vector3(0.3f, 1.5f, 0.1f);
        Ray ray = VRUIInputBridge.TrackingPoseToRay(tracking, local, Quaternion.Euler(0, 20, 0));
        Check(Vector3.Distance(ray.origin, tracking.TransformPoint(local)) < 0.0001f, "Tracking position ignored XR Origin.");
        Check(Vector3.Angle(ray.direction, tracking.TransformDirection(Quaternion.Euler(0, 20, 0) * Vector3.forward)) < 0.01f, "Tracking direction ignored XR Origin rotation.");
        var camera = Camera("Projection camera");
        camera.transform.SetPositionAndRotation(tracking.position, tracking.rotation);
        camera.pixelRect = new Rect(0, 0, 800, 400);
        camera.fieldOfView = 60;
        camera.aspect = 2;
        Vector3 world = camera.ViewportToWorldPoint(new Vector3(0.75f, 0.75f, 2));
        Check(VRUIInputBridge.TryProjectRay(camera, new Ray(camera.transform.position, world - camera.transform.position), out Vector2 screen), "Valid ray was rejected.");
        Vector2 expected = camera.ViewportToScreenPoint(new Vector3(0.75f, 0.75f, 2));
        Check(Vector2.Distance(screen, expected) < 0.1f, $"Viewport displacement is scaled incorrectly: {screen}, expected {expected}.");
        Check(!VRUIInputBridge.TryProjectRay(camera, new Ray(camera.transform.position, -camera.transform.forward), out _), "Backward ray reached UI.");
    }

    private static void TestDialogueAndModal()
    {
        var player = Object("Player").AddComponent<PlayerController>();
        var settings = Object("Settings").AddComponent<SettingsManager>();
        settings.settingsPanel = Object("Settings panel");
        settings.settingsPanel.SetActive(false);
        var dialogue = Object("Dialogue").AddComponent<DialogueUIManager>();
        Call(dialogue, "Awake");
        var npc = Object("NPC", typeof(BoxCollider)).AddComponent<NPCInteractable>();
        npc.dialogueLines = new[] { "First line", "Second line", "Third line" };
        dialogue.StartDialogue(npc);
        Check(!player.enabled, "Player can move while reading dialogue.");
        Button next = Get<Button>(dialogue, "nextLineButton");
        next.onClick.Invoke();
        Check(Get<int>(dialogue, "lineIndex") == 1 && dialogue.IsSpeaking, "Next button skipped or failed to advance a line.");
        next.onClick.Invoke();
        Check(Get<int>(dialogue, "lineIndex") == 2 && dialogue.IsSpeaking, "The third NPC line was skipped.");
        settings.OpenPanel();
        next.onClick.Invoke();
        Check(!dialogue.IsSpeaking && !player.enabled, "Closing dialogue unlocked movement under Settings.");
        settings.ClosePanel();
        Check(player.enabled, "Player movement was not restored after the final modal closed.");
    }

    private static void TestPaintingOwnership()
    {
        var ui = PaintingUI();
        var a = Painting("Painting A", Vector3.zero);
        var b = Painting("Painting B", Vector3.right);
        var player = Object("Player", typeof(BoxCollider));
        player.tag = "Player";
        var child = Object("Player hand", typeof(BoxCollider));
        child.transform.SetParent(player.transform);
        PlayerDetector.RegisterRoot(player.transform);
        Collider first = player.GetComponent<Collider>(), second = child.GetComponent<Collider>();
        Call(a, "OnTriggerEnter", first);
        Call(a, "OnTriggerEnter", second);
        Call(b, "OnTriggerEnter", first);
        ui.ShowPaintingInfo(a.GetComponent<PaintingInfo>());
        Call(b, "OnTriggerExit", first);
        Check(ui.IsPopupOpen, "Leaving B closed A's popup.");
        Call(a, "OnTriggerExit", first);
        Check(ui.IsPopupOpen, "Leaving one collider closed the popup while another remains.");
        Call(a, "OnTriggerExit", second);
        Check(!ui.IsPopupOpen && ui.CurrentPainting == null, "Leaving the final collider did not close the owned popup.");
    }

    private static void TestGuideChoices()
    {
        var dialogue = Object("Dialogue").AddComponent<DialogueUIManager>();
        Call(dialogue, "Awake");
        var table = Object("Workshop", typeof(BoxCollider)).AddComponent<MinigameTrigger>();
        table.minigameUI = Object("Workshop panel");
        Call(table, "Start");
        var game = table.minigameUI.AddComponent<ColoringPageMinigame>();
        game.workshopPanel = table.minigameUI;
        var guide = Object("Guide", typeof(BoxCollider)).AddComponent<NPCInteractable>();
        guide.dialogueLines = new[] { "Welcome", "Workshop or gallery?" };
        guide.workshopTrigger = table;
        void ReadGuide()
        {
            dialogue.StartDialogue(guide);
            Get<Button>(dialogue, "nextLineButton").onClick.Invoke();
            Get<Button>(dialogue, "nextLineButton").onClick.Invoke();
            Check(Get<GameObject>(dialogue, "choicesRoot").activeSelf, "Guide choices were not shown.");
            Check(Get<Button>(dialogue, "workshopButton").gameObject.activeInHierarchy
                && Get<Button>(dialogue, "continueButton").gameObject.activeInHierarchy, "One guide choice is missing.");
        }
        ReadGuide();
        Get<Button>(dialogue, "continueButton").onClick.Invoke();
        Check(!dialogue.IsSpeaking && !table.IsMinigameOpen, "Continue tour opened the workshop.");
        ReadGuide();
        Get<Button>(dialogue, "workshopButton").onClick.Invoke();
        Check(!dialogue.IsSpeaking && table.IsMinigameOpen, "Guide workshop choice did not open the assigned table.");
        game.CloseWorkshop();
        Check(!table.IsMinigameOpen && !MinigameTrigger.IsAnyOpen && !table.minigameUI.activeSelf,
            "Closing the workshop left its table's modal registration active.");
    }

    private static void TestNPCPromptOwnership()
    {
        var dialogue = Object("Dialogue").AddComponent<DialogueUIManager>();
        Call(dialogue, "Awake");
        var player = Object("Player", typeof(BoxCollider)).AddComponent<PlayerController>();
        PlayerDetector.RegisterRoot(player.transform);
        Collider first = player.GetComponent<Collider>();
        var hand = Object("Player hand", typeof(BoxCollider));
        hand.transform.SetParent(player.transform, false);
        Collider second = hand.GetComponent<Collider>();
        var a = Object("NPC A", typeof(BoxCollider)).AddComponent<NPCInteractable>();
        var b = Object("NPC B", typeof(BoxCollider)).AddComponent<NPCInteractable>();
        a.dialogueLines = b.dialogueLines = new[] { "Hello" };
        Call(a, "Start");
        Call(b, "Start");
        void Enter(NPCInteractable npc, Collider collider)
        {
            Call(npc, "OnTriggerEnter", collider);
            Call(npc.GetComponent<InteractableOutline>(), "OnTriggerEnter", collider);
        }
        Enter(a, first);
        Enter(a, second);
        Enter(b, first);
        Enter(b, second);
        Check(Get<int>(dialogue, "promptRequests") == 2, "NPC and outline registered the same prompt twice.");
        Call(a, "OnTriggerExit", first);
        Call(a.GetComponent<InteractableOutline>(), "OnTriggerExit", first);
        Check(a.GetComponent<InteractableOutline>().IsProximityActive && Get<int>(dialogue, "promptRequests") == 2,
            "Leaving one collider lost the NPC's outline/prompt.");
        dialogue.StartDialogue(b);
        a.gameObject.SetActive(false);
        Call(a, "OnDisable");
        Call(a.GetComponent<InteractableOutline>(), "OnDisable");
        Check(dialogue.IsConversationWith(b) && Get<int>(dialogue, "promptRequests") == 1,
            "Disabling A canceled B's dialogue or leaked A's prompt.");
        var settings = Object("Settings").AddComponent<SettingsManager>();
        settings.settingsPanel = Object("Settings panel");
        settings.OpenPanel();
        b.gameObject.SetActive(false);
        Call(b, "OnDisable");
        Call(b.GetComponent<InteractableOutline>(), "OnDisable");
        Check(!dialogue.IsSpeaking && !player.enabled, "Canceling the disabled NPC unlocked movement under Settings.");
        Check(Get<int>(dialogue, "promptRequests") == 0 &&
            !Array.Exists(dialogue.GetComponentsInChildren<Transform>(true), child => child.name == "InteractPrompt"),
            "Disabled NPCs left a prompt visible.");
        settings.ClosePanel();
    }

    private static void TestNPCBodyRay()
    {
        var dialogue = Object("Dialogue").AddComponent<DialogueUIManager>();
        Call(dialogue, "Awake");
        var camera = Camera("NPC aim");
        var interaction = Object("Interaction").AddComponent<PlayerInteraction>();
        var a = Object("NPC body A", typeof(BoxCollider)).AddComponent<NPCInteractable>();
        a.transform.position = Vector3.forward * 2;
        a.interactionCollider = a.GetComponent<Collider>();
        a.dialogueLines = new[] { "A" };
        var b = Object("NPC body B", typeof(BoxCollider)).AddComponent<NPCInteractable>();
        b.transform.position = new Vector3(2, 0, 3);
        b.interactionCollider = b.GetComponent<Collider>();
        var range = b.gameObject.AddComponent<SphereCollider>();
        range.radius = 2.9f;
        range.isTrigger = true;
        b.dialogueLines = new[] { "B" };
        a.GetComponent<InteractableOutline>().SetProximity(true);
        Physics.SyncTransforms();
        typeof(PlayerInteraction).GetField("lastInteractionFrame", Private).SetValue(null, -1);
        interaction.TryInteract();
        Check(dialogue.IsConversationWith(a), "The proximity volume selected B while aiming at A's body.");
        var reticle = Object("Reticle").AddComponent<CrosshairReticle>();
        reticle.aimCamera = camera;
        Check((InteractableOutline)Call(reticle, "FindAimedInteractable") == a.GetComponent<InteractableOutline>(),
            "Crosshair and interaction selected different NPCs.");
        dialogue.EndDialogue();
    }

    private static void TestWorkshopGuards()
    {
        var player = Object("Player").AddComponent<PlayerController>();
        var settings = Object("Settings").AddComponent<SettingsManager>();
        settings.settingsPanel = Object("Settings panel");
        settings.settingsPanel.SetActive(false);
        var workshop = Object("Workshop table", typeof(BoxCollider)).AddComponent<MinigameTrigger>();
        workshop.minigameUI = Object("Workshop panel");
        workshop.playerController = player;
        Call(workshop, "Start");
        settings.OpenPanel();
        workshop.OpenMinigame();
        Check(!workshop.IsMinigameOpen, "Workshop opened under Settings.");
        settings.ClosePanel();
        workshop.OpenMinigame();
        Check(workshop.IsMinigameOpen && !player.enabled, "Unblocked workshop did not open/lock movement.");
        settings.OpenPanel();
        workshop.CloseMinigame();
        Check(!workshop.IsMinigameOpen && !player.enabled, "Workshop close failed or unlocked movement under Settings.");
        settings.ClosePanel();
        var door = Object("Door", typeof(BoxCollider)).AddComponent<DoorMenuTrigger>();
        door.doorMenuUI = Object("Door menu");
        door.minigameUI = workshop.minigameUI;
        Call(door, "SyncOpenState");
        workshop.OpenMinigame();
        Check(!workshop.IsMinigameOpen, "Workshop opened behind a door menu.");
        door.OpenMinigame();
        Check(workshop.IsMinigameOpen && !door.doorMenuUI.activeSelf, "Door-to-workshop handoff was blocked.");
        door.CloseMinigame();
        var exit = Object("Exit").AddComponent<ExitToExteriorUI>();
        var dialog = Object("Exit dialog");
        dialog.SetActive(false);
        Set(exit, "dialogRoot", dialog);
        exit.ToggleDialog();
        workshop.OpenMinigame();
        Check(!workshop.IsMinigameOpen, "Workshop opened behind the exit dialog.");
        exit.StayHere();
        Check(player.enabled, "Movement did not resume after the final modal.");
    }

    private static void TestCrosshair()
    {
        var camera = Camera("Aim camera");
        var reticle = Object("Reticle").AddComponent<CrosshairReticle>();
        reticle.aimCamera = camera;
        var player = Object("Player collider", typeof(BoxCollider));
        player.tag = "Player";
        player.transform.position = Vector3.forward;
        var volume = Object("Noninteractive volume", typeof(BoxCollider));
        volume.transform.position = Vector3.forward * 1.4f;
        volume.GetComponent<BoxCollider>().isTrigger = true;
        volume.transform.localScale = Vector3.one * 0.1f;
        var wall = Object("Wall", typeof(BoxCollider));
        wall.transform.position = Vector3.forward * 2f;
        wall.transform.localScale = new Vector3(2, 2, 0.1f);
        var target = Painting("Behind wall", Vector3.forward * 3f);
        var outline = target.GetComponent<InteractableOutline>();
        outline.SetProximity(true);
        Physics.SyncTransforms();
        Check(Call(reticle, "FindAimedInteractable") == null, "Crosshair selected a target through a wall.");
        UnityEngine.Object.DestroyImmediate(wall);
        Physics.SyncTransforms();
        Check((InteractableOutline)Call(reticle, "FindAimedInteractable") == outline, "Player/volume incorrectly blocked the target.");
    }

    private static void TestJumpRegion()
    {
        var camera = Camera("Touch projection");
        var canvas = Object("Mobile canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvas.transform.position = Vector3.forward * 3f;
        var overlay = canvas.gameObject.AddComponent<MobileControlsOverlay>();
        Call(overlay, "OnEnable");
        var button = Object("Jump", typeof(RectTransform)).GetComponent<RectTransform>();
        button.SetParent(canvas.transform, false);
        button.sizeDelta = new Vector2(100, 100);
        Set(overlay, "jumpButton", button);
        var sizes = new[] { new Vector2(800, 600), new Vector2(1920, 1080), new Vector2(3840, 2160) };
        for (int i = 0; i < sizes.Length; i++)
        {
            camera.pixelRect = new Rect(Vector2.zero, sizes[i]);
            canvas.transform.localScale = Vector3.one * (0.005f * (i + 1));
            Vector2 center = camera.WorldToScreenPoint(button.position);
            Vector2 outside = camera.WorldToScreenPoint(button.TransformPoint(new Vector3(60, 0, 0)));
            Check(MobileControlsOverlay.IsInJumpZone(center), "Visible button center is outside the input zone.");
            Check(!MobileControlsOverlay.IsInJumpZone(outside), "Input zone extends beyond the scaled button.");
        }
    }

    private static void TestAudio()
    {
        var a = Camera("Desktop camera");
        var b = Camera("VR camera");
        AudioListener aListener = a.gameObject.AddComponent<AudioListener>();
        AudioListener bListener = b.gameObject.AddComponent<AudioListener>();
        bListener.enabled = false;
        SingleAudioListener.EnforceSingleListener(b);
        Check(bListener.enabled && !aListener.enabled, "VR listener was not re-enabled.");
        b.gameObject.SetActive(false);
        SingleAudioListener.EnforceSingleListener(a);
        Check(aListener.enabled, "Desktop listener was not restored.");
        var audio = Object("Audio manager").AddComponent<AudioManager>();
        AudioManager.Instance = audio;
        audio.bgmSource = audio.gameObject.AddComponent<AudioSource>();
        audio.voiceSource = audio.gameObject.AddComponent<AudioSource>();
        audio.sfxSource = audio.gameObject.AddComponent<AudioSource>();
        audio.SetBGMVolume(1);
        audio.SetMasterVolume(0.5f);
        Check(Mathf.Approximately(audio.bgmSource.volume * AudioListener.volume, 0.5f), "Master gain was applied twice to BGM.");
        Check(Mathf.Approximately(audio.voiceSource.volume * AudioListener.volume, 0.5f), "Master gain was applied twice to voice.");
        audio.SetSFXVolume(0.4f);
        Check(Mathf.Approximately(audio.sfxSource.volume * AudioListener.volume, 0.2f), "SFX gain is incorrect.");
    }

    private static void TestColoringFallback()
    {
        var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        resources.Add(texture);
        Color[] pixels = new Color[64];
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            pixels[y * 8 + x] = x > 0 && x < 7 && y > 0 && y < 7 ? Color.white : Color.black;
        pixels[0] = Color.white; // A tiny border background; the enclosed region is largest.
        texture.SetPixels(pixels);
        texture.Apply();
        var game = Object("Coloring test").AddComponent<ColoringPageMinigame>();
        var image = Object("Coloring image", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(game.transform, false);
        image.rectTransform.sizeDelta = new Vector2(100, 100);
        game.coloringImage = image;
        var definition = new ColoringArtworkDefinition { title = "Largest enclosed region", lineArt = texture, minimumRegionPixels = 1 };
        game.paintings = new[] { definition };
        Call(game, "PreparePaintingList");
        var invalid = new List<string> { null, "{ invalid json" };
        foreach (var span in new[]
        {
            new ColoringPixelSpan { start = -1, length = 1 },
            new ColoringPixelSpan { start = 60, length = 8 },
            new ColoringPixelSpan { start = 0, length = int.MaxValue },
            new ColoringPixelSpan { start = 0, length = 0 }
        })
        {
            invalid.Add(JsonUtility.ToJson(new ColoringRegionDataAsset
            {
                width = 8, height = 8, regionCount = 1,
                regions = new[] { new ColoringRegionSpanItem { id = 1, pixelCount = span.length, spans = new[] { span } } }
            }));
        }
        invalid.Add(JsonUtility.ToJson(new ColoringRegionDataAsset
        {
            width = 8, height = 8, regionCount = 2,
            regions = new[]
            {
                new ColoringRegionSpanItem { id = 1, pixelCount = 2, spans = new[] { new ColoringPixelSpan { start = 0, length = 2 } } },
                new ColoringRegionSpanItem { id = 2, pixelCount = 2, spans = new[] { new ColoringPixelSpan { start = 1, length = 2 } } }
            }
        }));
        foreach (string json in invalid)
        {
            definition.regionData = json != null ? new TextAsset(json) : null;
            if (definition.regionData != null) resources.Add(definition.regionData);
            game.SelectPainting(0);
            game.ResetPainting();
            Check(Get<ColoringRegionSpanItem[]>(game, "cachedRegionSpans") == null, "Invalid spans were accepted.");
            Check(Get<int>(game, "fillableRegionCount") == 1, "Largest enclosed region was excluded as background.");
            game.palette = new[] { Color.red };
            game.SelectPaletteIndex(0);
            game.OnPointerClick(new PointerEventData(null) { position = RectTransformUtility.WorldToScreenPoint(null, image.rectTransform.position) });
            Check(Get<int>(game, "paintedRegionCount") == 1, "Fallback did not paint the enclosed region.");
            Check(((Texture2D)image.texture).GetPixels32()[27].Equals((Color32)Color.red), "Fallback pixel color is incorrect.");
        }
    }
}
