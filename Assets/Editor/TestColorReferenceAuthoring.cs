using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Execute only on an isolated project copy: creates assets/fixture scene and exits the Editor.
public static class TestColorReferenceAuthoring
{
    private const string Flag = "BAINHOM.ColorReferenceAuthoring";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> results = new List<string>(), warnings = new List<string>();
    private static ColoringPageMinigame game;
    private static int stage, checks;
    private static double started, next;
    private static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Private).GetValue(o);
    private static void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; results.Add("PASS: " + message); }
    private static string Hash(string path)
    { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
    private static Color32[] Fixture(bool transparent, bool texture)
    {
        var pixels = new Color32[256 * 160]; var random = new System.Random(42);
        for (int y = 0; y < 160; y++)
        for (int x = 0; x < 256; x++)
        {
            Color32 c = transparent ? new Color32(255, 0, 255, 0) : new Color32(255, 255, 255, 255);
            if (y >= 40 && y < 120 && x >= 20 && x < 230)
                c = x < 70 || x >= 170 ? new Color32(230, 60, 60, 255)
                    : x < 120 ? new Color32(40, 80, 220, 255) : new Color32(40, 190, 80, 255);
            // A high-contrast four-pixel detail is preserved; paper noise is filtered separately.
            if (x >= 35 && x < 37 && y >= 65 && y < 67) c = new Color32(240, 230, 40, 255);
            if (texture && c.a > 0 && c.r < 250)
            {
                int noise = random.Next(-4, 5);
                c.r = (byte)Mathf.Clamp(c.r + noise, 0, 255); c.g = (byte)Mathf.Clamp(c.g + noise, 0, 255); c.b = (byte)Mathf.Clamp(c.b + noise, 0, 255);
            }
            pixels[y * 256 + x] = c;
        }
        return pixels;
    }
    private static Texture2D Source(string path, Color32[] pixels, int width = 256, int height = 160)
    {
        var t = new Texture2D(width, height, TextureFormat.RGBA32, false); t.SetPixels32(pixels); t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false;
        importer.isReadable = false; importer.alphaIsTransparency = false;
        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" }) importer.ClearPlatformTextureSettings(platform);
        importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static AddColoringArtworkWindow Analyze(Texture2D source, string title, int min = 20)
    {
        var w = ScriptableObject.CreateInstance<AddColoringArtworkWindow>();
        Set(w, "workshop", game); Set(w, "originalImage", source); Set(w, "sourceMode", 1);
        Set(w, "artworkName", title); Set(w, "minimumRegionPixels", min); Set(w, "smoothLines", false);
        Set(w, "colorLineThickness", 2);
        typeof(AddColoringArtworkWindow).GetMethod("ReanalyzeArtwork", Private).Invoke(w, null);
        Check(Get<string>(w, "colorAnalysisError") == null, "Colour analysis succeeds: " + title);
        return w;
    }
    private static void AlgorithmChecks()
    {
        var flat = ColorArtworkSegmentation.Analyze(Fixture(false, false), 256, 160, 12, 20, false);
        Check(flat.RegionCount == 6 && flat.SmallRegionsMerged == 0, "Flat image: spatial regions plus protected high-contrast four-pixel detail");
        Check(flat.Labels[80 * 256 + 40] != flat.Labels[80 * 256 + 200], "Identical colours in disconnected areas have different IDs");
        Check(!flat.MergeAdjacent(flat.Labels[80 * 256 + 40], flat.Labels[80 * 256 + 200]), "Manual merge refuses disconnected regions");
        var textured = ColorArtworkSegmentation.Analyze(Fixture(false, true), 256, 160, 12, 20, true);
        Check(textured.RegionCount == 6, "Paper noise is grouped while the contrasting small detail survives");
        var alpha = ColorArtworkSegmentation.Analyze(Fixture(true, false), 256, 160, 12, 20, false);
        Check(alpha.RegionCount == 5 && alpha.Labels[0] == 0, "Transparent background never becomes paintable; small detail survives");
        var near = new Color32[64 * 32];
        for (int p = 0; p < near.Length; p++) near[p] = p % 64 < 32 ? new Color32(255, 100, 100, 255) : new Color32(235, 100, 100, 255);
        Check(ColorArtworkSegmentation.Analyze(near, 64, 32, 2, 1, false).RegionCount == 2
            && ColorArtworkSegmentation.Analyze(near, 64, 32, 20, 1, false).RegionCount == 1, "Merge strength distinguishes or joins neighbouring close colours");
        // Semi-transparent anti-alias pixels below the cutoff remain outside all spans.
        near[0] = new Color32(255, 0, 0, 64); near[1] = new Color32(255, 0, 0, 192);
        var aa = ColorArtworkSegmentation.Analyze(near, 64, 32, 12, 4, true);
        Check(aa.Labels[0] == 0 && aa.Labels[1] > 0, "Anti-alias alpha cutoff is independent of hidden RGB");
    }
    private static void Audit(ColoringArtworkDefinition art)
    {
        var data = JsonUtility.FromJson<ColoringRegionDataAsset>(art.regionData.text);
        Check(data.width == art.lineArt.width && data.height == art.lineArt.height
            && data.width == art.paintMask.width && data.height == art.paintMask.height
            && data.width == art.referenceArt.width && data.height == art.referenceArt.height, "Imported reference/line/mask/JSON have one grid: " + art.title);
        object[] args = { data, data.width, data.height, null, null };
        Check((bool)typeof(ColoringPageMinigame).GetMethod("TryBuildCachedRegions", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args), "Runtime accepts actual exported cache: " + art.title);
        int[] labels = (int[])args[4]; var mask = art.paintMask.GetPixels32(); var lines = art.lineArt.GetPixels32();
        for (int p = 0; p < labels.Length; p++)
            if ((labels[p] > 0) != (mask[p].r == 255) || labels[p] > 0 && lines[p].r != 255)
                throw new Exception("Line/mask/label disagreement at " + p);
        Check(true, "Every mask pixel and white interior agrees with exported spans: " + art.title);
        Check(data.regionCount > 0 && data.regionCount <= 5000, "Nonempty, bounded region count: " + data.regionCount);
        foreach (Texture2D output in new[] { art.lineArt, art.paintMask, art.referenceArt })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(output));
            Check(importer.npotScale == TextureImporterNPOTScale.None && !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed, "Generated grid cannot resize or compress by default");
            foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
                Check(!importer.GetPlatformTextureSettings(platform).overridden, "Generated override cleared: " + platform);
        }
    }
    private static void ExportPreview(AddColoringArtworkWindow window, Texture2D original, string name)
    {
        string path = "Logs/ColorAuthoringPreview"; Directory.CreateDirectory(path);
        foreach (string field in new[] { "previewLineArt", "previewRegionOverlay", "previewMask" })
            File.WriteAllBytes(path + "/" + name + "_" + field + ".png", Get<Texture2D>(window, field).EncodeToPNG());
        File.WriteAllText(path + "/" + name + "_Stats.txt", "Imported grid: " + original.width + "x" + original.height
            + "; colour regions=" + Get<ColorArtworkSegmentation>(window, "colorAnalysis").RegionCount
            + "; paintable=" + Get<int>(window, "fillableRegionCount") + "; lost=" + Get<int>(window, "lostColorRegions"));
    }
    private static ColoringArtworkDefinition SaveWithPreviewCheck(AddColoringArtworkWindow window)
    {
        var line = Get<Texture2D>(window, "previewLineArt"); int width = line.width, height = line.height;
        var linePixels = line.GetPixels32(); var maskPixels = Get<Texture2D>(window, "previewMask").GetPixels32();
        var regions = Get<List<ColoringRegionSpanItem>>(window, "calculatedRegions").ToArray();
        var reference = Get<Color32[]>(window, "colorReferencePixels");
        Check(window.TryAddArtworkToWorkshop(out var art, out string error), "Complete save succeeds: " + error);
        var data = JsonUtility.FromJson<ColoringRegionDataAsset>(art.regionData.text);
        Check(data.width == width && data.height == height && art.lineArt.GetPixels32().SequenceEqual(linePixels)
            && art.paintMask.GetPixels32().SequenceEqual(maskPixels), "Imported NPOT output matches preview pixel-for-pixel without resize");
        Check(art.referenceArt.GetPixels32().SequenceEqual(reference), "Saved reference matches the untouched colour input, before smoothing/segmentation");
        Check(data.regionCount == regions.Length && data.regions.Select((r, i) => r.id == regions[i].id
            && r.pixelCount == regions[i].pixelCount && JsonUtility.ToJson(r) == JsonUtility.ToJson(regions[i])).All(v => v), "Every preview region/span retained after import");
        return art;
    }
    public static void BeginWithRegression()
    {
        TestGameplayRegressionSuite.RunAllTests(); TestColoringWorkshopSuite.RunAllTests(true); Begin();
    }
    private static void AuditPreviousGeneratedReferences()
    {
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/ColorReferenceFixture", StringComparison.Ordinal)) continue;
            EditorSceneManager.OpenScene(path);
            var previous = UnityEngine.Object.FindAnyObjectByType<ColoringPageMinigame>();
            if (previous == null || previous.paintings == null) continue;
            foreach (var art in previous.paintings)
            {
                if (art.referenceArt == null || !AssetDatabase.GetAssetPath(art.referenceArt).Contains("_Reference")) continue;
                Audit(art); count++;
            }
        }
        results.Add("CROSS-TARGET: " + count + " existing stable references audited on " + EditorUserBuildSettings.activeBuildTarget);
    }
    public static void Begin()
    {
        results.Clear(); warnings.Clear(); checks = 0;
        try
        {
            AlgorithmChecks();
            AuditPreviousGeneratedReferences();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var image = new GameObject("Paint", typeof(RectTransform), typeof(RawImage)); image.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)image.transform; rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.sizeDelta = new Vector2(600, 375);
            game = image.AddComponent<ColoringPageMinigame>(); game.coloringImage = image.GetComponent<RawImage>(); game.paintings = Array.Empty<ColoringArtworkDefinition>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/ColorReferenceFixture"); AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            foreach (bool transparent in new[] { false, true })
            {
                string path = folder + "/" + (transparent ? "Transparent" : "Flat") + ".png";
                Texture2D source = Source(path, Fixture(transparent, false)); string hash = Hash(path), meta = Hash(path + ".meta");
                using (var window = new WindowOwner(Analyze(source, "GeneratedColour")))
                {
                    if (!transparent) window.Value.EditColorRegionAt(3, 3, 1);
                    Check(Get<int>(window.Value, "fillableRegionCount") == 5, "Click exclusion leaves foreground targets and protected small detail");
                    window.Value.EditColorRegionAt(90, 80, 2); window.Value.EditColorRegionAt(140, 80, 2);
                    Check(Get<int>(window.Value, "fillableRegionCount") == 4, "Preview manual merge removes only the shared blue/green boundary");
                    ExportPreview(window.Value, source, transparent ? "Transparent" : "FlatEdited");
                    var art = SaveWithPreviewCheck(window.Value);
                    Check(art.referenceArt != source && art.referenceScale == 1 && art.referenceOffset == Vector2.zero, "Dedicated stable reference preserves original source"); Audit(art);
                }
                Check(Hash(path) == hash && Hash(path + ".meta") == meta && !source.isReadable, "Analysis/save preserve unreadable source PNG and importer");
            }
            Check(game.paintings[0].title == game.paintings[1].title && game.paintings[0].lineArt != game.paintings[1].lineArt, "Same title produces unique assets and appends; no overwrite");
            // Saved real reference assets exercise importer grid and textured art.
            foreach (string path in new[] { "Assets/TRAnh/Gà đàn.jpg", "Assets/TRAnh/Lợn đàn.jpg" })
            {
                Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(path); Check(source != null, "Existing colour source found: " + path);
                string hash = Hash(path), meta = Hash(path + ".meta");
                using (var window = new WindowOwner(Analyze(source, Path.GetFileNameWithoutExtension(path) + " Colour", 80)))
                {
                    // These paper-textured controls use the explicit Texture preset,
                    // rather than inheriting the lighter new-window default.
                    window.Value.ApplyColorPreset(1);
                    Set(window.Value, "smoothLines", true); Set(window.Value, "colorMergeDistance", 24f);
                    Set(window.Value, "minimumRegionPixels", 200); Set(window.Value, "colorLineThickness", 1);
                    typeof(AddColoringArtworkWindow).GetMethod("ReanalyzeArtwork", Private).Invoke(window.Value, null);
                    int lost = Get<int>(window.Value, "lostColorRegions");
                    results.Add("REVIEW EDIT: " + path + "; Texture preset, nativeInk=off, Lab=24, min=200, line=1; explicitly exclude " + lost + " narrow regions consumed by outlines");
                    window.Value.ExcludeUnpaintableColorRegions();
                    window.Value.EditColorRegionAt(source.width - 8, source.height - 8, 1);
                    Check(Get<Texture2D>(window.Value, "previewMask").GetPixels32()[(source.height - 8) * source.width + source.width - 8].r == 0,
                        "Explicitly excluded real painting background is absent from mask");
                    Check(Get<int>(window.Value, "lostColorRegions") == 0, "Explicit preview edit resolves narrow-region warning");
                    ExportPreview(window.Value, source, Path.GetFileNameWithoutExtension(path));
                    var art = SaveWithPreviewCheck(window.Value); Audit(art);
                }
                Check(Hash(path) == hash && Hash(path + ".meta") == meta, "Existing painting and importer preserved");
            }
            foreach (Vector2Int size in new[] { new Vector2Int(554, 416), new Vector2Int(1024, 683) })
            {
                Color32[] small = Fixture(false, false), large = new Color32[size.x * size.y];
                for (int y = 0; y < size.y; y++) for (int x = 0; x < size.x; x++)
                    large[y * size.x + x] = small[(int)((long)y * 160 / size.y) * 256 + (int)((long)x * 256 / size.x)];
                Texture2D source = Source(folder + "/NPOT" + size.x + ".png", large, size.x, size.y);
                using (var window = new WindowOwner(Analyze(source, "ColourNPOT" + size.x)))
                { window.Value.EditColorRegionAt(3, 3, 1); Audit(SaveWithPreviewCheck(window.Value)); }
            }
            // A one-pixel isolated island cannot survive thick outlines; fail before creating any output.
            var tiny = new Color32[32 * 32]; tiny[16 * 32 + 16] = new Color32(255, 0, 0, 255);
            using (var window = new WindowOwner(Analyze(Source(folder + "/Tiny.png", tiny, 32, 32), "RejectTiny", 1)))
            {
                int count = game.paintings.Length, files = Directory.GetFiles(folder).Length;
                Check(Get<int>(window.Value, "lostColorRegions") == 1, "Preview detects a region consumed by outline");
                Check(!window.Value.TryAddArtworkToWorkshop(out _, out _) && game.paintings.Length == count && Directory.GetFiles(folder).Length == files, "Lost region rejects save without assets or workshop mutation");
            }
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), folder + "/Runtime.unity");
            SessionState.SetString(Flag + ".results", string.Join("\n", results)); SessionState.SetInt(Flag + ".checks", checks);
            SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Finish(error); }
    }
    private sealed class WindowOwner : IDisposable
    { internal readonly AddColoringArtworkWindow Value; internal WindowOwner(AddColoringArtworkWindow value) { Value = value; } public void Dispose() { UnityEngine.Object.DestroyImmediate(Value); } }
    [InitializeOnLoadMethod] private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Warning; Application.logMessageReceived += Warning;
    }
    private static void Warning(string message, string stack, LogType type)
    { if (message.Contains("[ColoringPageMinigame]") && (type == LogType.Warning || type == LogType.Error || type == LogType.Exception)) warnings.Add(message); }
    private static void ClickPixel(int p)
    {
        Canvas.ForceUpdateCanvases(); var t = game.paintings[stage].lineArt; var rect = game.coloringImage.rectTransform;
        float u = (p % t.width + 0.5f) / t.width, v = (p / t.width + 0.5f) / t.height;
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(rect.rect.xMin + u * rect.rect.width, rect.rect.yMin + v * rect.rect.height))) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject == game.gameObject, "Actual UI raycast reaches colour artwork");
        pointer.pointerPressRaycast = hits[0]; ExecuteEvents.Execute(game.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (started == 0)
        {
            started = EditorApplication.timeSinceStartup; next = started + 1; results.Clear();
            results.AddRange(SessionState.GetString(Flag + ".results", "").Split('\n')); checks = SessionState.GetInt(Flag + ".checks", 0);
            game = UnityEngine.Object.FindAnyObjectByType<ColoringPageMinigame>();
        }
        if (EditorApplication.timeSinceStartup - started > 60) { Finish(new TimeoutException("Runtime timeout")); return; }
        if (EditorApplication.timeSinceStartup < next) return; next = EditorApplication.timeSinceStartup + 0.3;
        try
        {
            if (stage < game.paintings.Length)
            {
                game.SelectPainting(stage); var regions = Get<ColoringRegionSpanItem[]>(game, "cachedRegionSpans");
                Check(regions != null, "Runtime uses generated cache without fallback: " + stage);
                int pixel = regions[0].spans.OrderByDescending(s => s.length).First().start;
                pixel += regions[0].spans.OrderByDescending(s => s.length).First().length / 2;
                game.SelectPaletteIndex(1); ClickPixel(pixel);
                var pixels = ((Texture2D)game.coloringImage.texture).GetPixels32(); var before = game.paintings[stage].lineArt.GetPixels32();
                var painted = new HashSet<int>(); foreach (var s in regions[0].spans) for (int i = s.start; i < s.start + s.length; i++) painted.Add(i);
                for (int p = 0; p < pixels.Length; p++)
                    if (!pixels[p].Equals(painted.Contains(p) ? (Color32)game.palette[1] : before[p])) throw new Exception("Paint escaped chosen component at " + p);
                Check(Get<int>(game, "paintedRegionCount") == 1, "One click fills only chosen region, not neighbour/line/excluded background");
                // Explicitly try an excluded/transparent background click for synthetic fixtures.
                if (stage < 2) { ClickPixel(3 * 256 + 3); Check(Get<int>(game, "paintedRegionCount") == 1, "Excluded/transparent background does not change progress"); }
                stage++;
            }
            else
            {
                game.SelectPainting(0); Check(Get<int>(game, "paintedRegionCount") == 1, "Switch back restores colour/progress");
                var oldPixels = ((Texture2D)game.coloringImage.texture).GetPixels32();
                game.gameObject.SetActive(false); game.gameObject.SetActive(true); game.SelectPainting(0);
                Check(Get<int>(game, "paintedRegionCount") == 1 && ((Texture2D)game.coloringImage.texture).GetPixels32().SequenceEqual(oldPixels), "Close/reopen retains pixels and progress");
                Check(warnings.Count == 0, "No invalid cache warning/fallback throughout runtime flow"); Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Warning; SessionState.SetBool(Flag, false);
        results.AddRange(warnings); results.Add(error == null ? "RESULT: PASS; " + EditorUserBuildSettings.activeBuildTarget + "; " + checks + " assertions; colour segmentation, real export/import, 6 runtime artworks; simulated pointer only" : "RESULT: FAIL stage=" + stage + "\n" + error);
        Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/ColorReferenceAuthoringResults.txt", results);
        if (error != null) Debug.LogException(error); EditorApplication.Exit(error == null ? 0 : 1);
    }
}
