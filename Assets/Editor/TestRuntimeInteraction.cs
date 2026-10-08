using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Reflection;

// Batch-only: changes the active scene and queues synthetic input device events.
public static class TestRuntimeInteraction
{
    private const string InputFlag = "BAINHOM.RuntimeInputValidation";
    private static GameObject uiFixture, eventFixture;
    private static readonly List<string> results = new List<string>();
    private static void Check(bool condition, string name)
    { if (!condition) throw new InvalidOperationException(name); results.Add("PASS: " + name); }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run on an isolated batchmode project copy.");
        results.Clear();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        GameObject root = new GameObject("Interaction regression fixtures");
        try
        {
            Transform player = new GameObject("Player").transform;
            player.SetParent(root.transform);
            player.tag = "Player";
            Func<string, Vector3, NPCInteractable> npcAt = (name, position) =>
            {
                var go = new GameObject(name, typeof(BoxCollider), typeof(NPCInteractable));
                go.transform.SetParent(root.transform);
                go.transform.position = position;
                var col = go.GetComponent<BoxCollider>(); col.size = new Vector3(.6f, 1.8f, .6f);
                var npc = go.GetComponent<NPCInteractable>(); npc.interactionCollider = col;
                npc.dialogueLines = new[] { "Hello", "Next" };
                return npc;
            };
            Func<string, Vector3, Vector3, BoxCollider> box = (name, position, size) =>
            {
                var go = new GameObject(name, typeof(BoxCollider)); go.transform.SetParent(root.transform);
                go.transform.position = position; var col = go.GetComponent<BoxCollider>(); col.size = size; return col;
            };
            var npc = npcAt("NPC ahead", new Vector3(0, .9f, 2.5f));
            Physics.SyncTransforms();
            Ray ray = new Ray(new Vector3(0, 1.2f, 0), Vector3.forward);
            Check(InteractionTargetResolver.TryRay(ray, player, 3.5f, out var target) && target.owner == npc, "Ray chooses NPC body");
            Check(InteractionTargetResolver.TryNearby(player, Vector3.forward, 3.5f, out target) && target.owner == npc, "Flat fallback chooses visible NPC");
            var wall = box("Wall", new Vector3(0, 1.2f, 1), new Vector3(4, 4, .1f)); Physics.SyncTransforms();
            Check(!InteractionTargetResolver.TryRay(ray, player, 3.5f, out target), "NPC behind wall rejected by ray");
            Check(!InteractionTargetResolver.TryNearby(player, Vector3.forward, 3.5f, out target), "NPC behind wall rejected by fallback");
            wall.enabled = false;
            npc.transform.position = new Vector3(0, 3.9f, 1); Physics.SyncTransforms();
            Check(!InteractionTargetResolver.TryNearby(player, Vector3.forward, 3.5f, out target), "NPC on another floor rejected without relying on floor collider");
            npc.transform.position = new Vector3(0, .9f, -2); Physics.SyncTransforms();
            Check(!InteractionTargetResolver.TryNearby(player, Vector3.forward, 3.5f, out target), "NPC behind player rejected by facing cone");
            npc.transform.position = new Vector3(0, .9f, 5); Physics.SyncTransforms();
            Check(!InteractionTargetResolver.TryNearby(player, Vector3.forward, 3.5f, out target), "Out-of-range NPC rejected");
            npc.transform.position = new Vector3(1, .9f, 2.5f);
            var doorCol = box("Closer door", new Vector3(-.6f, 1.2f, 1.5f), new Vector3(.6f, 2, .1f));
            var door = doorCol.gameObject.AddComponent<DoorMenuTrigger>(); Physics.SyncTransforms();
            Check(InteractionTargetResolver.TryNearby(player, Vector3.forward, 3.5f, out target) && target.owner == door, "Closer door beats NPC across target types");
            doorCol.enabled = false;
            npc.transform.position = new Vector3(0, .9f, 2.5f);
            wall.enabled = true; wall.isTrigger = true; Physics.SyncTransforms();
            Check(InteractionTargetResolver.TryRay(ray, player, 3.5f, out target) && target.owner == npc, "Unrelated trigger volume does not hide body ray");
            Check(InteractionTargetResolver.TryNearby(player, Vector3.forward, 3.5f, out target) && target.owner == npc, "Unrelated trigger volume does not hide fallback");
            var range = npc.gameObject.AddComponent<SphereCollider>(); range.radius = 2; range.isTrigger = true;
            Physics.SyncTransforms();
            Check(InteractionTargetResolver.TryRay(ray, player, 3.5f, out target) && target.collider == npc.interactionCollider, "NPC proximity trigger is never the selected body");
            Ray miss = new Ray(ray.origin, new Vector3(.65f, 0, 1).normalized);
            Check(!InteractionTargetResolver.TrySelect(miss, player, 3.5f, true, true, out target), "XR ray miss never falls back to nearby NPC");
            Check(!InteractionTargetResolver.TrySelect(ray, player, 3.5f, true, false, out target), "Missing XR controller ray never uses camera or proximity");
            Check(InteractionTargetResolver.TrySelect(ray, player, 3.5f, true, true, out target) && target.owner == npc, "Valid XR ray still selects aimed NPC");
            npc.enabled = false;
            var rear = npcAt("NPC behind disabled body", new Vector3(0, .9f, 3.2f)); Physics.SyncTransforms();
            Check(!InteractionTargetResolver.TryRay(ray, player, 3.5f, out target), "Disabled NPC solid body blocks target behind it");
            Check(!InteractionTargetResolver.TryNearby(player, Vector3.forward, 3.5f, out target), "Disabled NPC body blocks nearby fallback to rear target");
            npc.enabled = true; rear.gameObject.SetActive(false);
            var self = box("Player child", new Vector3(0, 1.2f, .6f), Vector3.one * .3f);
            self.transform.SetParent(player, true); Physics.SyncTransforms();
            Check(InteractionTargetResolver.TryRay(ray, player, 3.5f, out target) && target.owner == npc, "Player child collider does not block interaction");
            var large = box("Large NPC proximity only", new Vector3(0, .9f, 5), Vector3.one * .2f);
            var far = large.gameObject.AddComponent<NPCInteractable>(); far.interactionCollider = large;
            var farRange = large.gameObject.AddComponent<SphereCollider>(); farRange.radius = 5; farRange.isTrigger = true;
            npc.gameObject.SetActive(false); Physics.SyncTransforms();
            Check(!InteractionTargetResolver.TryNearby(player, Vector3.forward, 3.5f, out target), "Large proximity volume cannot bypass body distance");
            File.WriteAllLines("Logs/RuntimeInteractionResults.txt", results);
            Debug.Log("[RuntimeInteraction] PASS " + results.Count + " assertions");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    public static void BeginInput()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batchmode project copy.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetBool(InputFlag, true);
        ResumeInput();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void ResumeInput()
    {
        if (!SessionState.GetBool(InputFlag, false)) return;
        EditorApplication.update -= InputTick;
        EditorApplication.update += InputTick;
    }

    private static void InputTick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.update -= InputTick;
        SessionState.SetBool(InputFlag, false);
        results.Clear();
        var driver = new GameObject("Runtime input regression driver").AddComponent<RuntimeInputRegressionDriver>();
        driver.run = FinishInput;
    }

    private static void FinishInput()
    {
        try
        {
            TestInput();
            eventFixture = new GameObject("UI input fixture", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            uiFixture = new GameObject("UI canvas", typeof(Canvas), typeof(GraphicRaycaster));
            uiFixture.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var button = new GameObject("UI button", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(uiFixture.transform, false);
            var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.sizeDelta = new Vector2(100,100); rect.anchoredPosition = Vector2.zero;
            // Let Canvas/Graphic lifecycle complete on a rendered player frame.
            UnityEngine.Object.FindAnyObjectByType<RuntimeInputRegressionDriver>().run = FinishUI;
        }
        catch (Exception ex) { File.WriteAllText("Logs/RuntimeInputFailure.txt", ex.ToString()); Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    private static void FinishUI()
    {
        try
        {
            Canvas.ForceUpdateCanvases();
            Check(GameplayInput.IsOverUI(new Vector2(Screen.width / 2f, Screen.height / 2f)), "UI raycast protects button from scene tap regardless of touch pointer id");
            Check(!GameplayInput.IsOverUI(new Vector2(Screen.width - 10, Screen.height - 10)), "Empty screen remains available for scene tap");
            File.WriteAllLines("Logs/RuntimeInputResults.txt", results);
            Debug.Log("[RuntimeInput] PASS " + results.Count + " assertions in Play Mode");
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { File.WriteAllText("Logs/RuntimeInputFailure.txt", ex.ToString()); Debug.LogException(ex); EditorApplication.Exit(1); }
        finally { UnityEngine.Object.DestroyImmediate(uiFixture); UnityEngine.Object.DestroyImmediate(eventFixture); }
    }

    private static void TestInput()
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var touchscreen = InputSystem.AddDevice<Touchscreen>();
        MobileControlsOverlay overlay = null;
        var previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        var previousBackground = InputSystem.settings.backgroundBehavior;
        var previousScroll = InputSystem.settings.scrollDeltaBehavior;
        try
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.scrollDeltaBehavior = InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;
            InputSystem.EnableDevice(keyboard); InputSystem.EnableDevice(mouse); InputSystem.EnableDevice(touchscreen);
            EnhancedTouchSupport.Enable();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D, Key.E, Key.LeftShift, Key.Space));
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(20, -10), scroll = new Vector2(0, 1) }.WithButton(MouseButton.Left));
            InputSystem.Update();
            keyboard.MakeCurrent(); mouse.MakeCurrent(); touchscreen.MakeCurrent();
            Check(GameplayInput.GetAxisRaw("Horizontal") == 1 && GameplayInput.GetAxisRaw("Vertical") == 1,
                "Input System WASD movement: axes=" + GameplayInput.GetAxisRaw("Horizontal") + "," + GameplayInput.GetAxisRaw("Vertical")
                + "; W=" + keyboard.wKey.ReadValue() + "; D=" + keyboard.dKey.ReadValue() + "; enabled=" + keyboard.enabled
                + "; update=" + InputState.currentUpdateType);
            Check(GameplayInput.GetKeyDown(KeyCode.E) && GameplayInput.GetKey(KeyCode.LeftShift) && GameplayInput.GetButtonDown("Jump"),
                "Input System interaction, sprint and jump: E=" + GameplayInput.GetKeyDown(KeyCode.E)
                + ", shift=" + GameplayInput.GetKey(KeyCode.LeftShift) + ", jump=" + GameplayInput.GetButtonDown("Jump")
                + ", native E=" + keyboard.eKey.wasPressedThisFrame + ", update=" + InputState.currentUpdateType);
            Check(GameplayInput.GetMouseButtonDown(0), "Input System mouse press");
            Check(Mathf.Abs(GameplayInput.GetAxis("Mouse X") - 2) < .001f && Mathf.Abs(GameplayInput.GetAxis("Mouse Y") + 1) < .001f, "Mouse sensitivity preserves old 0.1 axis scaling");
            Check(Mathf.Abs(GameplayInput.GetAxis("Mouse ScrollWheel") - .1f) < .001f, "Wheel scaling preserves zoom per notch");
            InputSystem.settings.scrollDeltaBehavior = InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange;
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, 120) });
            InputSystem.Update(); mouse.MakeCurrent();
            Check(Mathf.Abs(GameplayInput.GetAxis("Mouse ScrollWheel") - .1f) < .001f, "Optional Windows platform-specific wheel range preserves the same zoom");
            overlay = MobileControlsOverlay.FindOrCreate();
            Canvas.ForceUpdateCanvases();
            var processTouches = typeof(MobileControlsOverlay).GetMethod("ProcessTouches", BindingFlags.Instance | BindingFlags.NonPublic);
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 71, phase = UnityEngine.InputSystem.TouchPhase.Began, position = new Vector2(100, 200) });
            InputSystem.Update();
            Check(GameplayInput.touchCount == 1 && GameplayInput.GetTouch(0).fingerId == 71 && GameplayInput.GetTouch(0).phase == UnityEngine.TouchPhase.Began, "Enhanced touch begins with stable touch id");
            processTouches.Invoke(overlay, null);
            Check(MobileControlsOverlay.Move == Vector2.zero, "Floating joystick clamp does not start movement on touch down");
            var stick = (RectTransform)typeof(MobileControlsOverlay).GetField("stickRoot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(overlay);
            Vector2 moved = RectTransformUtility.WorldToScreenPoint(null, stick.TransformPoint(new Vector3(54, 72)));
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 71, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = moved });
            InputSystem.Update();
            Check(GameplayInput.GetTouch(0).fingerId == 71 && GameplayInput.GetTouch(0).position == moved, "Enhanced touch movement uses screen coordinates");
            processTouches.Invoke(overlay, null);
            Check(Vector2.Distance(MobileControlsOverlay.Move, new Vector2(.6f, .8f)) < .01f, "Touch event stream drives visible joystick to independent direction and magnitude");
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 71, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = moved });
            InputSystem.Update();
            Check(GameplayInput.GetTouch(0).phase == UnityEngine.TouchPhase.Ended, "Enhanced touch keeps ended phase for release handling");
            processTouches.Invoke(overlay, null);
            Check(MobileControlsOverlay.Move == Vector2.zero, "Touch release clears joystick movement");
            InputSystem.Update();
            Check(GameplayInput.touchCount == 0, "Ended touch clears on next update");

        }
        finally
        {
            // Balance the enable above; preserve whichever state existed on entry.
            EnhancedTouchSupport.Disable();
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(touchscreen);
            if (overlay != null) UnityEngine.Object.DestroyImmediate(overlay.gameObject);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior;
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.scrollDeltaBehavior = previousScroll;
        }
    }
}

// Run inside the player loop, rather than the Editor's separate device-state buffer.
public class RuntimeInputRegressionDriver : MonoBehaviour
{
    public Action run;
    private void Update()
    {
        Action callback = run;
        run = null;
        callback?.Invoke();
    }
}
