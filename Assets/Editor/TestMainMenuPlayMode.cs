using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Uses raycast-selected UI targets and pointer callbacks, not physical mouse input.
public static class TestMainMenuPlayMode
{
    private const string Flag = "BAINHOM.MainMenuPlayMode";
    private static readonly List<string> results = new List<string>();
    private static int stage;
    private static double startedAt, nextStepAt;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }

    // Batch entry point: run on a separate checkout/copy; exits the Editor.
    public static void Begin()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SetupColoringWorkshopTool.SetupWorkshop();
        var fresh = UnityEngine.Object.FindAnyObjectByType<ColoringPageMinigame>(FindObjectsInactive.Include);
        if (fresh == null || fresh.workshopPanel.activeSelf)
            throw new InvalidOperationException("Setup opens a newly created workshop by default.");

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        string savedScene = File.ReadAllText(scene.path);
        var existing = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ColoringPageMinigame>(true)).Single();
        if (existing.workshopPanel.activeSelf)
            throw new InvalidOperationException("Saved MainMenu workshop starts open.");
        string artworkBefore = JsonUtility.ToJson(new ArtworkSnapshot { paintings = existing.paintings });
        existing.workshopPanel.SetActive(true);
        SetupColoringWorkshopTool.SetupWorkshop();
        SetupColoringWorkshopTool.SetupWorkshop();
        if (existing.workshopPanel.activeSelf || artworkBefore != JsonUtility.ToJson(new ArtworkSnapshot { paintings = existing.paintings }))
            throw new InvalidOperationException("Repeated setup opens workshop or changes artwork data.");
        if (savedScene != File.ReadAllText(scene.path))
            throw new InvalidOperationException("Setup saved changes unexpectedly.");
        // Discard setup preview changes; Play Mode must use the saved scene.
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
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

    private static void ClickThroughUI(Button button)
    {
        Check(button != null && button.IsActive() && button.IsInteractable(), "Requested button is active and interactable");
        Check(EventSystem.current != null, "EventSystem exists");
        Canvas.ForceUpdateCanvases();
        var canvas = button.GetComponentInParent<Canvas>();
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
            button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0, "UI raycast hits a target at " + button.name);
        var hit = hits[0];
        var clickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
        Check(clickTarget == button.gameObject, "First UI raycast belongs to " + button.name + " (hit " + hit.gameObject.name + ")");
        pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hit;
        pointer.pointerEnter = hit.gameObject;
        pointer.pressPosition = pointer.position;
        pointer.eligibleForClick = true;
        pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        Check(pointer.pointerPress == clickTarget, "Pointer press resolves to the same button");
        ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(clickTarget, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static void ClickPlay()
    {
        var workshop = UnityEngine.Object.FindAnyObjectByType<ColoringPageMinigame>(FindObjectsInactive.Include);
        Check(workshop != null && !workshop.workshopPanel.activeSelf, "Saved MainMenu workshop remains closed");
        var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuManager>();
        Check(menu != null, "MainMenuManager initializes");
        var play = UnityEngine.Object.FindObjectsByType<Button>().Single(button =>
            Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(i => button.onClick.GetPersistentMethodName(i) == "PlayGame"));
        menu.minLoadingTime = 0;
        ClickThroughUI(play);
        Check(menu.transform.Find("LoadingScreen").gameObject.activeInHierarchy, "Play UI click starts loading");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (startedAt == 0) { startedAt = EditorApplication.timeSinceStartup; nextStepAt = startedAt + 2; }
        if (EditorApplication.timeSinceStartup - startedAt > 120) { Finish(new TimeoutException("Menu UI test timed out.")); return; }
        if (EditorApplication.timeSinceStartup < nextStepAt) return;
        nextStepAt = EditorApplication.timeSinceStartup + 0.7;
        try
        {
            switch (stage)
            {
                case 0:
                    results.Add("PASS: Fresh/repeated setup keeps workshop inactive and preserves artwork data (Editor checks)");
                    ClickPlay();
                    break;
                case 1:
                    if (SceneManager.GetActiveScene().name != "SampleScene") return;
                    Check(true, "Initial Play UI click loads gallery");
                    UnityEngine.Object.FindAnyObjectByType<SettingsManager>().OpenPanel();
                    break;
                case 2:
                    ClickThroughUI(UnityEngine.Object.FindAnyObjectByType<SettingsManager>().mainMenuButton);
                    break;
                case 3:
                    if (SceneManager.GetActiveScene().name != "MainMenu") return;
                    Check(true, "Gallery Settings UI button returns to MainMenu");
                    ClickPlay();
                    break;
                case 4:
                    if (SceneManager.GetActiveScene().name != "SampleScene") return;
                    Check(true, "Play UI click after return loads gallery again");
                    Finish(null);
                    return;
            }
            stage++;
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Flag, false);
        results.Add(error == null
            ? "RESULT: PASS. Raycast-selected UI press/release/click callbacks simulated; no direct PlayGame/GoToMainMenu calls. Physical mouse/touch/VR and rendered devices not verified."
            : "RESULT: FAIL at stage " + stage + "\n" + error);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/MainMenuPlayModeResults.txt", results);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    [Serializable] private class ArtworkSnapshot { public ColoringArtworkDefinition[] paintings; }
}
