using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Render existing scene UI to a camera target at simulated viewport/safe-area sizes.
public static class TestSettingsLayoutPlayMode
{
    private const string Flag = "BAINHOM.SettingsLayoutPlayMode";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> results = new List<string>();
    private static readonly Vector2Int[] sizes = { new Vector2Int(1080, 2400), new Vector2Int(720, 1600), new Vector2Int(1920, 1080), new Vector2Int(1280, 360) };
    private static readonly Rect[] safeAreas = { new Rect(80, 160, 900, 2080), new Rect(0, 64, 720, 1500), new Rect(70, 20, 1850, 1040), new Rect(70, 0, 1210, 340) };
    private static int stage;
    private static bool scenePrepared;
    private static bool workshopPrepared;
    private static double startedAt, nextStepAt;

    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Flag, true);
        ResumeIfRequested();
        EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    private static void ResumeIfRequested()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }
    private static void Layout(SettingsManager settings, Rect safe, Vector2 size)
        => typeof(SettingsManager).GetMethod("ApplySettingsLayout", Private).Invoke(settings, new object[] { safe, size });

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (startedAt == 0) { startedAt = EditorApplication.timeSinceStartup; nextStepAt = startedAt + 2; }
        if (EditorApplication.timeSinceStartup - startedAt > 180) { Finish(new TimeoutException("Settings render test timed out.")); return; }
        if (EditorApplication.timeSinceStartup < nextStepAt) return;
        nextStepAt = EditorApplication.timeSinceStartup + 0.5;
        try
        {
            // TMP_Dropdown initializes its tween runner in Start, on the frame
            // after an initially inactive Settings panel is first activated.
            if (stage < 9 && stage != 4 && !scenePrepared)
            {
                UnityEngine.Object.FindAnyObjectByType<SettingsManager>().OpenPanel();
                scenePrepared = true;
                return;
            }
            if ((stage >= 9 && stage < 11 || stage >= 12 && stage < 15) && !workshopPrepared)
            {
                UnityEngine.Object.FindAnyObjectByType<ColoringPageMinigame>(FindObjectsInactive.Include).workshopPanel.SetActive(true);
                workshopPrepared = true;
                return;
            }
            if (stage < 4) RenderSettings(sizes[stage], safeAreas[stage]);
            else if (stage == 4) { scenePrepared = false; SceneManager.LoadScene("SampleScene"); }
            else if (stage < 9) RenderSettings(sizes[stage - 5], safeAreas[stage - 5]);
            else if (stage == 9) RenderWorkshop(new Vector2Int(1920, 1080), new Rect(70, 20, 1850, 1040));
            else if (stage == 10) RenderWorkshop(new Vector2Int(1080, 2400), new Rect(80, 160, 900, 2080));
            else if (stage == 11) { workshopPrepared = false; SceneManager.LoadScene("MainMenu"); }
            else if (stage == 12) RenderWorkshop(new Vector2Int(1920, 1080), new Rect(70, 20, 1850, 1040));
            else if (stage == 13) RenderWorkshop(new Vector2Int(1080, 2400), new Rect(80, 160, 900, 2080));
            else if (stage == 14)
            {
                UnityEngine.Object.FindAnyObjectByType<ColoringPageMinigame>(FindObjectsInactive.Include).SelectPainting(1);
                RenderWorkshop(new Vector2Int(720, 1600), new Rect(0, 64, 720, 1500));
            }
            else { Finish(null); return; }
            stage++;
        }
        catch (Exception error) { Finish(error); }
    }

    private static Camera CameraFor(Canvas canvas, Vector2Int size)
    {
        foreach (Canvas other in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            other.enabled = other == canvas;
        foreach (GraphicRaycaster raycaster in UnityEngine.Object.FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include))
            raycaster.enabled = raycaster.GetComponent<Canvas>() == canvas;
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>()) renderer.enabled = false;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        float factor = 1f;
        if (scaler != null)
        {
            Vector2 reference = scaler.referenceResolution;
            factor = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(size.x / reference.x, 2f), Mathf.Log(size.y / reference.y, 2f), scaler.matchWidthOrHeight));
            scaler.enabled = false;
        }
        canvas.scaleFactor = factor;
        var camera = new GameObject("Layout capture camera", typeof(Camera)).GetComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = size.y / (2f * factor);
        camera.transform.position = new Vector3(0, 0, -100);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
        camera.targetTexture = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10;
        Canvas.ForceUpdateCanvases();
        return camera;
    }

    private static void SaveImage(Camera camera, string file)
    {
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = camera.targetTexture;
        var image = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0); image.Apply();
        Directory.CreateDirectory("Logs/Round4Images");
        File.WriteAllBytes("Logs/Round4Images/" + file + ".png", image.EncodeToPNG());
        RenderTexture.active = previous;
        UnityEngine.Object.Destroy(image);
        camera.targetTexture.Release();
        UnityEngine.Object.Destroy(camera.targetTexture);
        UnityEngine.Object.Destroy(camera.gameObject);
        results.Add("IMAGE: Logs/Round4Images/" + file + ".png");
    }

    private static void Inside(RectTransform rect, Camera camera, Rect safe, string label)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            if (point.x < safe.xMin - 1 || point.x > safe.xMax + 1 || point.y < safe.yMin - 1 || point.y > safe.yMax + 1)
                throw new InvalidOperationException(label + " outside safe area: " + point + ", safe " + safe);
        }
        results.Add("PASS: " + label + " fully inside safe area");
    }

    private static void Click(Selectable selectable, Camera camera)
    {
        RectTransform rect = (RectTransform)selectable.transform;
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>();
        Canvas.ForceUpdateCanvases(); camera.Render();
        EventSystem.current.RaycastAll(pointer, hits);
        if (hits.Count == 0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) != selectable.gameObject)
        {
            string details = "position " + pointer.position + ", hits " + string.Join(", ", hits.Select(h => h.gameObject.name));
            SaveImage(camera, "Diagnostic-" + selectable.name);
            throw new InvalidOperationException("Top target differs from " + selectable.name + ": " + details);
        }
        Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == selectable.gameObject,
            "Top UI raycast/click target is " + selectable.name);
        pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
        pointer.pressPosition = pointer.position;
        pointer.eligibleForClick = true;
        pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(selectable.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static void RenderSettings(Vector2Int size, Rect safe)
    {
        var settings = UnityEngine.Object.FindAnyObjectByType<SettingsManager>();
        settings.enabled = false;
        settings.OpenPanel();
        Canvas canvas = settings.settingsPanel.GetComponentInParent<Canvas>();
        Camera camera = CameraFor(canvas, size);
        Layout(settings, safe, size);
        Canvas.ForceUpdateCanvases();
        var controls = new Selectable[] { settings.masterSlider, settings.bgmSlider, settings.sfxSlider, settings.bgmDropdown, settings.fpsDropdown, settings.closeSettingButton, settings.mainMenuButton, settings.quitButton }
            .Concat(settings.settingsPanel.GetComponentsInChildren<Toggle>(false).Where(t => t.name == "CrosshairToggle" || t.name == "JoystickToggle")).ToArray();
        foreach (Selectable control in controls)
        {
            Check(control != null && control.IsActive() && control.IsInteractable(), control.name + " active/interactable at " + size);
            Inside((RectTransform)control.transform, camera, safe, control.name);
            Check(control.transform.localScale == Vector3.one, control.name + " has no inherited template scaling");
        }
        foreach (TMP_Text label in settings.settingsPanel.GetComponentsInChildren<TMP_Text>(false))
            Inside(label.rectTransform, camera, safe, label.name + " label");
        // A changed safe inset with identical viewport dimensions also reflows controls.
        Rect alteredSafe = new Rect(safe.x + 8, safe.y + 12, safe.width - 16, safe.height - 24);
        Layout(settings, alteredSafe, size);
        Canvas.ForceUpdateCanvases();
        foreach (Selectable control in controls) Inside((RectTransform)control.transform, camera, alteredSafe, control.name + " after safe-inset-only change");
        Layout(settings, safe, size);
        Canvas.ForceUpdateCanvases();
        foreach (Toggle toggle in controls.OfType<Toggle>())
        {
            bool initial = toggle.isOn;
            Click(toggle, camera);
            Check(toggle.isOn != initial, toggle.name + " toggles through actual UI click");
            Click(toggle, camera);
            Check(toggle.isOn == initial, toggle.name + " second click restores original state");
        }
        foreach (TMP_Dropdown dropdown in new[] { settings.bgmDropdown, settings.fpsDropdown })
        {
            Click(dropdown, camera);
            Transform list = dropdown.transform.Find("Dropdown List");
            Check(list != null && list.gameObject.activeInHierarchy, dropdown.name + " opens actual TMP dropdown list");
            Inside((RectTransform)list, camera, safe, dropdown.name + " expanded list");
            var blocker = (GameObject)typeof(TMP_Dropdown).GetField("m_Blocker", Private).GetValue(dropdown);
            dropdown.Hide();
            if (blocker != null) blocker.SetActive(false);
            // Hide uses a delayed fade; deactivate test list so it cannot intercept the next click.
            list.gameObject.SetActive(false);
        }
        // Only navigation effects are replaced here; button raycast and click dispatch remain real.
        settings.mainMenuButton.onClick.RemoveListener(settings.GoToMainMenu);
        settings.quitButton.onClick.RemoveListener(settings.QuitGame);
        foreach (Button button in new[] { settings.mainMenuButton, settings.quitButton })
        {
            int invoked = 0;
            UnityEngine.Events.UnityAction callback = () => invoked++;
            button.onClick.AddListener(callback);
            Click(button, camera);
            Check(invoked == 1, button.name + " dispatches one UI click callback");
            button.onClick.RemoveListener(callback);
        }
        SaveImage(camera, SceneManager.GetActiveScene().name + "-Settings-" + size.x + "x" + size.y);
        // Check close through actual UI; recreate a camera because SaveImage released its target.
        camera = CameraFor(canvas, size); Layout(settings, safe, size); Canvas.ForceUpdateCanvases();
        Click(settings.closeSettingButton, camera);
        Check(!settings.IsSettingsOpen(), "Actual close button closes Settings");
        camera.targetTexture.Release(); UnityEngine.Object.Destroy(camera.targetTexture); UnityEngine.Object.Destroy(camera.gameObject);
    }

    private static void RenderWorkshop(Vector2Int size, Rect safe)
    {
        var game = UnityEngine.Object.FindAnyObjectByType<ColoringPageMinigame>(FindObjectsInactive.Include);
        game.workshopPanel.SetActive(true);
        Check(game.coloringImage.texture != null && game.referenceImage.texture != null, "Workshop render includes selected line/color and reference textures");
        var canvas = game.workshopPanel.GetComponentInParent<Canvas>();
        var camera = CameraFor(canvas, size);
        game.safeAreaRect.anchorMin = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
        game.safeAreaRect.anchorMax = new Vector2(safe.xMax / size.x, safe.yMax / size.y);
        game.safeAreaRect.offsetMin = game.safeAreaRect.offsetMax = Vector2.zero;
        // Let responsive layout use the simulated safe-area dimensions while keeping these anchors.
        typeof(ColoringPageMinigame).GetMethod("ApplyResponsiveLayout", Private).Invoke(game, null);
        game.safeAreaRect.anchorMin = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
        game.safeAreaRect.anchorMax = new Vector2(safe.xMax / size.x, safe.yMax / size.y);
        Canvas.ForceUpdateCanvases();
        game.RefreshArtworkLayout();
        Inside(game.coloringImageFrame, camera, safe, "Workshop coloring frame");
        Inside(game.referenceImageFrame, camera, safe, "Workshop reference frame");
        SaveImage(camera, SceneManager.GetActiveScene().name + "-Workshop-" + size.x + "x" + size.y);
        game.workshopPanel.SetActive(false);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Flag, false);
        results.Add(error == null
            ? "RESULT: PASS. Eight Settings renders and five workshop renders (all three saved artwork pairs); bounds, safe-inset-only reflow, expanded dropdowns, raycast/click dispatch and close checked. Simulated offscreen viewport/safe area and UI callbacks; no device render or physical touch/controller input. Navigation/quit side effects replaced only in layout-click fixture; menu navigation checked by separate regression suite."
            : "RESULT: FAIL at stage " + stage + "\n" + error);
        Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/SettingsLayoutPlayModeResults.txt", results);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
