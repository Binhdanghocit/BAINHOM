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

// Saved artwork, actual scene lifecycle and raycast-selected pointer callbacks.
// This does not simulate a physical mouse, touchscreen or XR controller.
public static class TestArtworkRegionPlayMode
{
    private const string Flag = "BAINHOM.ArtworkRegionPlayMode";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> results = new List<string>();
    private static readonly List<string> dataWarnings = new List<string>();
    private static int stage, cacheRegions;
    private static double startedAt, nextStepAt;
    private static ColoringPageMinigame game;
    private static int pixelA, pixelB, pixelSample;
    private static Color32 colorA, colorB, colorSample;
    private static T Get<T>(string field) => (T)typeof(ColoringPageMinigame).GetField(field, Private).GetValue(game);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }

    // Checks spans independently of the runtime parser and of the migration tool.
    private static void AuditSavedScenes()
    {
        int artworkCount = 0, cachedArtworkCount = 0;
        foreach (string name in new[] { "MainMenu", "SampleScene" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            foreach (var workshop in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ColoringPageMinigame>(true)))
            foreach (var art in workshop.paintings)
            {
                artworkCount++;
                Check(art.lineArt != null && art.referenceArt != null, name + "/" + art.title + ": image references resolve");
                Check(art.paintMask == null || art.paintMask.width == art.lineArt.width && art.paintMask.height == art.lineArt.height,
                    name + "/" + art.title + ": mask dimensions match");
                if (art.regionData == null)
                {
                    results.Add("DATA: " + name + "/" + art.title + ": no cache configured; native connected-region detection is intentional.");
                    continue;
                }
                cachedArtworkCount++;
                var data = JsonUtility.FromJson<ColoringRegionDataAsset>(art.regionData.text);
                Check(data.width == art.lineArt.width && data.height == art.lineArt.height,
                    name + "/" + art.title + ": JSON dimensions equal imported texture " + data.width + "x" + data.height);
                Check(data.regionCount > 0 && data.regions.Length == data.regionCount, art.title + ": region count matches array");
                var occupied = new bool[checked(data.width * data.height)];
                var ids = new HashSet<int>();
                var line = art.lineArt.GetPixels32();
                var mask = art.paintMask == null ? null : art.paintMask.GetPixels32();
                foreach (var region in data.regions)
                {
                    bool structureOK = region.id >= 1 && region.id <= data.regionCount && ids.Add(region.id)
                        && region.spans != null && region.spans.Length > 0;
                    long pixels = 0;
                    int paintable = 0;
                    if (!structureOK) throw new Exception("Invalid/duplicate region ID " + region.id);
                    foreach (var span in region.spans)
                    {
                        if (span.start < 0 || span.length <= 0 || (long)span.start + span.length > occupied.Length)
                            throw new Exception("Out-of-bounds span " + region.id);
                        for (int p = span.start; p < span.start + span.length; p++)
                        {
                            if (occupied[p]) throw new Exception("Overlapping span " + p);
                            occupied[p] = true;
                            int lum = (299 * line[p].r + 587 * line[p].g + 114 * line[p].b) / 1000;
                            if (lum > art.whiteThreshold && (mask == null || mask[p].r >= 128)) paintable++;
                        }
                        pixels += span.length;
                    }
                    Check(pixels == region.pixelCount && paintable > 0,
                        art.title + " region " + region.id + ": valid non-overlapping spans, exact pixel count and paintable pixels");
                }
                cacheRegions += data.regionCount;
                Check(data.regionCount == (art.title == "2" ? 50 : 108), art.title + ": authored region count retained");
            }
        }
        Check(artworkCount == 3 && cachedArtworkCount == 2 && cacheRegions == 158, "All 3 saved artworks audited; 158 cached regions checked");
        // Static results must survive entering Play Mode / domain reload.
        SessionState.SetString(Flag + ".audit", string.Join("\n", results));
    }

    [MenuItem("Tools/Tests/Run Saved Artwork Region Play Mode")]
    public static void Begin()
    {
        results.Clear(); cacheRegions = 0;
        AuditSavedScenes();
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
        Application.logMessageReceived -= RecordWarning;
        Application.logMessageReceived += RecordWarning;
    }
    private static void RecordWarning(string message, string stack, LogType type)
    {
        if (message.Contains("[ColoringPageMinigame]") && (type == LogType.Warning || type == LogType.Error || type == LogType.Exception))
            dataWarnings.Add(message);
    }
    private static void OpenSavedWorkshop()
    {
        game = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<ColoringPageMinigame>(true)).Single();
        game.workshopPanel.SetActive(true);
    }
    private static void CheckCurrent(bool cached, int expectedRegions)
    {
        Check(game.isActiveAndEnabled && game.coloringImage.texture != null, "Saved workshop active with a working texture");
        Check((Get<ColoringRegionSpanItem[]>("cachedRegionSpans") != null) == cached,
            "Current artwork uses " + (cached ? "accepted cached regions" : "intentional native detection"));
        Check(Get<int>("fillableRegionCount") == expectedRegions && expectedRegions > 0, "Expected fillable regions: " + expectedRegions);
    }
    private static Color32 Pixel(int index) => ((Texture2D)game.coloringImage.texture).GetPixels32()[index];
    private static int PaintOne(int palette, out Color32 painted)
    {
        Canvas.ForceUpdateCanvases(); game.RefreshArtworkLayout(); Canvas.ForceUpdateCanvases();
        var source = Get<Color32[]>("sourcePixels");
        var mask = Get<Color32[]>("sourceMaskPixels");
        var cachedLabels = Get<int[]>("pixelToRegion");
        var labels = cachedLabels ?? Get<int[]>("componentLabels");
        var allowed = Get<bool[]>("fillableComponents");
        int pixel = -1;
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i].r == 255 && source[i].g == 255 && source[i].b == 255 && labels[i] > 0
                && (mask == null || mask[i].r >= 128) && (cachedLabels != null || allowed[labels[i] - 1]))
            { pixel = i; break; }
        }
        Check(pixel >= 0, "Artwork contains a fully white clickable region pixel");
        int width = game.coloringImage.texture.width, height = game.coloringImage.texture.height;
        var rect = game.coloringImage.rectTransform;
        Vector2 local = new Vector2(rect.rect.xMin + ((pixel % width + 0.5f) / width) * rect.rect.width,
            rect.rect.yMin + ((pixel / width + 0.5f) / height) * rect.rect.height);
        var canvas = game.coloringImage.GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(local)) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject == game.coloringImage.gameObject, "UI raycast reaches saved coloring image at chosen pixel");
        var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Check(target == game.gameObject, "Pointer event resolves to saved minigame handler");
        pointer.pointerPressRaycast = hits[0];
        var before = ((Texture2D)game.coloringImage.texture).GetPixels32();
        game.SelectPaletteIndex(palette);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        painted = Pixel(pixel);
        Check(painted.Equals((Color32)game.palette[palette]) && Get<int>("paintedRegionCount") == 1, "Pointer click paints selected region and records progress 1");
        var after = ((Texture2D)game.coloringImage.texture).GetPixels32();
        int clickedRegion = labels[pixel], changed = 0;
        for (int i = 0; i < before.Length; i++)
        {
            if (before[i].Equals(after[i])) continue;
            changed++;
            if (labels[i] != clickedRegion || mask != null && mask[i].r < 128)
                throw new Exception("Painting leaked outside the clicked region/mask at " + i);
        }
        Check(changed > 0, "Only pixels of the selected region inside the mask change");
        return pixel;
    }
    private static void Restored(int pixel, Color32 color, string name)
        => Check(Pixel(pixel).Equals(color) && Get<int>("paintedRegionCount") == 1, name + ": color and per-artwork progress restored");

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (startedAt == 0)
        {
            startedAt = EditorApplication.timeSinceStartup; nextStepAt = startedAt + 2;
            results.Clear(); results.AddRange(SessionState.GetString(Flag + ".audit", "").Split('\n'));
        }
        if (EditorApplication.timeSinceStartup - startedAt > 90) { Finish(new TimeoutException("Artwork play test timed out")); return; }
        if (EditorApplication.timeSinceStartup < nextStepAt) return;
        nextStepAt = EditorApplication.timeSinceStartup + 0.7;
        try
        {
            switch (stage)
            {
                case 0: OpenSavedWorkshop(); break;
                case 1: game.SelectPainting(0); CheckCurrent(true, 50); pixelA = PaintOne(1, out colorA); break;
                case 2: game.SelectPainting(1); CheckCurrent(true, 108); Check(Get<int>("paintedRegionCount") == 0, "B starts with independent empty progress"); pixelB = PaintOne(2, out colorB); break;
                case 3: game.SelectPainting(0); Restored(pixelA, colorA, "MainMenu A after B"); break;
                case 4: game.SelectPainting(1); Restored(pixelB, colorB, "MainMenu B after A"); game.workshopPanel.SetActive(false); break;
                case 5: game.workshopPanel.SetActive(true); break;
                case 6: game.SelectPainting(0); Restored(pixelA, colorA, "MainMenu A after close/open"); game.SelectPainting(1); Restored(pixelB, colorB, "MainMenu B after close/open"); SceneManager.LoadScene("SampleScene"); break;
                case 7: if (SceneManager.GetActiveScene().name != "SampleScene") return; OpenSavedWorkshop(); break;
                case 8: CheckCurrent(false, Get<int>("fillableRegionCount")); pixelSample = PaintOne(1, out colorSample); game.workshopPanel.SetActive(false); break;
                case 9: game.workshopPanel.SetActive(true); game.SelectPainting(0); break;
                case 10: Restored(pixelSample, colorSample, "SampleScene after close/open and reselection"); Check(dataWarnings.Count == 0, "No artwork data warning/error during saved scene Start, open, paint, switch or close/reopen"); Finish(null); return;
            }
            stage++;
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= RecordWarning;
        SessionState.SetBool(Flag, false);
        results.AddRange(dataWarnings.Select(w => "UNEXPECTED ARTWORK WARNING: " + w));
        int assertions = results.Count(r => r.StartsWith("PASS:"));
        results.Add(error == null ? "RESULT: PASS, 3 saved artworks / 158 cached regions / " + assertions + " assertions / 11 Play Mode stages. Simulated pointer callbacks; no physical device input/build verification."
            : "RESULT: FAIL stage=" + stage + "\n" + error);
        Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/ArtworkRegionPlayModeResults.txt", results);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
