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
using UnityEngine.XR.Management;

// Batch-only test: uses a fake XR loader, never hardware or a native runtime.
public static class TestQuestLinkEntry
{
    private const string Flag = "BAINHOM.QuestLinkEntryTest";
    private static readonly List<string> results = new List<string>();
    private static XRManagerSettings original, fixture;
    private static QuestLinkTestLoader loader;
    private static int stage;
    private static double deadline, next;

    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Flag, true);
        Resume();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (deadline == 0) { deadline = EditorApplication.timeSinceStartup + 90; next = EditorApplication.timeSinceStartup + 2; }
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Quest Link entry test timed out");
            if (EditorApplication.timeSinceStartup < next) return;
            if (stage == 0)
            {
                var settings = XRGeneralSettings.Instance;
                Check(settings != null && !settings.InitManagerOnStart && settings.Manager.activeLoader == null,
                    "PC starts flat without initializing native OpenXR");
                original = settings.Manager;
                fixture = ScriptableObject.CreateInstance<XRManagerSettings>();
                fixture.automaticLoading = fixture.automaticRunning = false;
                loader = ScriptableObject.CreateInstance<QuestLinkTestLoader>();
                // Register this test-only provider: runtime APIs only accept providers registered before Play.
                var registered = (HashSet<XRLoader>)typeof(XRManagerSettings)
                    .GetField("m_RegisteredLoaders", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fixture);
                registered.Add(loader);
                Check(fixture.TryAddLoader(loader), "Fake loader registered without modifying saved XR settings");
                settings.Manager = fixture;
                var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuManager>();
                menu.minLoadingTime = 0;
                var button = GameObject.Find("PlayVRButton").GetComponent<Button>();
                Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)button.transform;
                var canvas = button.GetComponentInParent<Canvas>();
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var pointer = new PointerEventData(EventSystem.current) {
                    position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                    button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
                    "UI raycast reaches Quest Link button");
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                // Flat Play during VR startup must also wait, rather than loading a second scene.
                menu.PlayGame();
                stage = 1;
                next = EditorApplication.timeSinceStartup + 1;
                return;
            }
            if (SceneManager.GetActiveScene().name != "SampleScene") return;
            Check(loader.initializeCalls == 1 && loader.startCalls == 1,
                "Repeated VR click initializes and starts loader exactly once before gallery entry");
            Check(fixture.activeLoader == loader, "Explicit VR entry uses initialized loader");
            XRBoot.StopXR();
            Check(loader.stopCalls == 1 && loader.deinitializeCalls == 1 && fixture.activeLoader == null,
                "Explicit stop releases loader");
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Flag, false);
        if (original != null && XRGeneralSettings.Instance != null) XRGeneralSettings.Instance.Manager = original;
        results.Add(error == null ? "RESULT: PASS; fake loader and simulated UI click; Quest 2 hardware unverified."
            : "RESULT: FAIL; " + error);
        Directory.CreateDirectory("Logs/ConsoleStartupReview");
        File.WriteAllLines("Logs/ConsoleStartupReview/QuestLinkEntryResults.txt", results);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public class QuestLinkTestLoader : XRLoader
{
    public int initializeCalls, startCalls, stopCalls, deinitializeCalls;
    public override bool Initialize() { initializeCalls++; return true; }
    public override bool Start() { startCalls++; return true; }
    public override bool Stop() { stopCalls++; return true; }
    public override bool Deinitialize() { deinitializeCalls++; return true; }
    public override T GetLoadedSubsystem<T>() { return null; }
}
