using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Integration through ReanalyzeArtwork and the complete save path used by the button.
// Runs in an isolated project, creates a fixture scene, then exits the batch Editor.
public static class TestArtworkAuthoringDimensions
{
    private const string Flag = "BAINHOM.ArtworkAuthoringDimensions";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> results = new List<string>();
    private static readonly List<string> warnings = new List<string>();
    private static int stage;
    private static double started, next;
    private static ColoringPageMinigame game;
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }
    private static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
    }
    private static Texture2D MakeSource(string path, int width, int height, bool tiny)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            bool box = x >= 20 && x < width - 20 && y >= 20 && y < height - 20;
            bool inside = x >= 24 && x < width - 24 && y >= 24 && y < height - 24;
            bool line = box && (!inside || x >= width / 2 - 2 && x < width / 2 + 2);
            if (tiny && x >= 96 && x < 108 && y >= 96 && y < 108)
                line = !(x >= 100 && x < 104 && y >= 100 && y < 104);
            pixels[y * width + x] = line ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255);
        }
        texture.SetPixels32(pixels); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
        importer.isReadable = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" }) importer.ClearPlatformTextureSettings(platform);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static AddColoringArtworkWindow Analyze(Texture2D source, string name, int minPixels)
    {
        var window = ScriptableObject.CreateInstance<AddColoringArtworkWindow>();
        Set(window, "workshop", game); Set(window, "artworkName", name);
        Set(window, "originalImage", source); Set(window, "referenceArt", source);
        Set(window, "lineThreshold", 180); Set(window, "gapClosureRadius", 0);
        Set(window, "smoothLines", false); Set(window, "minimumRegionPixels", minPixels);
        Set(window, "referenceScale", 0.75f); Set(window, "referenceOffset", new Vector2(0.1f, -0.2f));
        typeof(AddColoringArtworkWindow).GetMethod("ReanalyzeArtwork", Private).Invoke(window, null);
        return window;
    }
    private static int[] Labels(ColoringRegionDataAsset data)
    {
        var labels = new int[checked(data.width * data.height)];
        var ids = new HashSet<int>();
        foreach (var region in data.regions)
        {
            if (!ids.Add(region.id) || region.id < 1 || region.id > data.regionCount) throw new Exception("Duplicate/invalid ID");
            long count = 0;
            foreach (var span in region.spans)
            {
                if (span.start < 0 || span.length <= 0 || (long)span.start + span.length > labels.Length) throw new Exception("Out-of-bounds span");
                for (int p = span.start; p < span.start + span.length; p++)
                {
                    if (labels[p] != 0) throw new Exception("Overlapping span");
                    labels[p] = region.id;
                }
                count += span.length;
            }
            if (count != region.pixelCount || count == 0) throw new Exception("Incorrect pixelCount/empty region");
        }
        if (ids.Count != data.regionCount || data.regions.Length != data.regionCount) throw new Exception("Incorrect region count");
        return labels;
    }
    private static void CheckOutputs(ColoringArtworkDefinition art, int sourceWidth, int sourceHeight, int policy)
    {
        var data = JsonUtility.FromJson<ColoringRegionDataAsset>(art.regionData.text);
        Check(data.width == art.lineArt.width && data.height == art.lineArt.height
            && data.width == art.paintMask.width && data.height == art.paintMask.height, art.title + ": line/mask/JSON dimensions match after import");
        Check(data.regionCount == 2, art.title + ": both authored region IDs retained");
        Check(art.referenceScale == 0.75f && art.referenceOffset == new Vector2(0.1f, -0.2f), art.title + ": alignment retained");
        if (policy == 0)
            Check(data.width == Mathf.ClosestPowerOfTwo(sourceWidth) && data.height == Mathf.ClosestPowerOfTwo(sourceHeight), art.title + ": initial NPOT ToNearest resize exercised");
        else
            Check(Mathf.Max(data.width, data.height) == (policy == 1 ? 256 : 128), art.title + ": initial max-size/platform downscale exercised");
        int[] labels = Labels(data);
        var mask = art.paintMask.GetPixels32(); var lines = art.lineArt.GetPixels32();
        // Analytic boxes from the source fixture, independent of the remapping helper.
        for (int y = 0; y < data.height; y++)
        for (int x = 0; x < data.width; x++)
        {
            int sx = Mathf.FloorToInt((x + 0.5f) / data.width * sourceWidth);
            int sy = Mathf.FloorToInt((y + 0.5f) / data.height * sourceHeight);
            bool inside = sx >= 24 && sx < sourceWidth - 24 && sy >= 24 && sy < sourceHeight - 24;
            int expected = !inside ? 0 : sx < sourceWidth / 2 - 2 ? 1 : sx >= sourceWidth / 2 + 2 ? 2 : 0;
            int p = y * data.width + x;
            if (labels[p] != expected || (mask[p].r == 255) != (expected > 0) || mask[p].r != 0 && mask[p].r != 255)
                throw new Exception(art.title + ": UV labels/mask mismatch at " + x + "," + y);
            if (expected > 0 && lines[p].r != 255) throw new Exception("White region misaligned with line art");
        }
        Check(true, art.title + ": every output pixel matches independent source UV region/mask geometry");
        foreach (Texture2D output in new[] { art.lineArt, art.paintMask })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(output));
            importer.GetSourceTextureWidthAndHeight(out int nativeWidth, out int nativeHeight);
            Check(nativeWidth == data.width && nativeHeight == data.height && importer.npotScale == TextureImporterNPOTScale.None
                && importer.maxTextureSize >= Mathf.Max(nativeWidth, nativeHeight), art.title + ": PNG baked in cache grid; final default import cannot resize it");
            foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
                Check(!importer.GetPlatformTextureSettings(platform).overridden, art.title + ": generated " + platform + " override cleared");
        }
        results.Add("DATA: " + EditorUserBuildSettings.activeBuildTarget + " / " + art.title + " / source " + sourceWidth + "x" + sourceHeight
            + " -> saved " + data.width + "x" + data.height + ", regions=" + data.regionCount + ", spans=" + data.regions.Sum(r => r.spans.Length));
    }
    private static void AuditPreviouslyCreatedOutputs()
    {
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/ArtworkAuthoringFixture", StringComparison.Ordinal) || !path.EndsWith("_RegionData.json", StringComparison.Ordinal)) continue;
            var data = JsonUtility.FromJson<ColoringRegionDataAsset>(AssetDatabase.LoadAssetAtPath<TextAsset>(path).text);
            string prefix = path.Substring(0, path.Length - "_RegionData.json".Length);
            var line = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_LineArt.png");
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_PaintMask.png");
            Check(line != null && mask != null && line.width == data.width && line.height == data.height && mask.width == data.width && mask.height == data.height,
                "Previously created output dimensions remain valid on " + EditorUserBuildSettings.activeBuildTarget + ": " + path);
            object[] args = { data, line.width, line.height, null, null };
            bool accepted = (bool)typeof(ColoringPageMinigame).GetMethod("TryBuildCachedRegions", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            Check(accepted, "Runtime parser accepts previously created cache after target import");
            int[] labels = Labels(data); Color32[] maskPixels = mask.GetPixels32();
            for (int i = 0; i < labels.Length; i++)
                if ((labels[i] > 0) != (maskPixels[i].r >= 128)) throw new Exception("Previously created mask changed after target import");
            Check(true, "Previously created mask still matches every region label after target import");
            count++;
        }
        results.Add("CROSS-TARGET DATA: " + count + " prior generated caches inspected on " + EditorUserBuildSettings.activeBuildTarget);
    }
    public static void Begin()
    {
        results.Clear(); warnings.Clear();
        string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/ArtworkAuthoringFixture");
        AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        SessionState.SetString(Flag + ".folder", folder);
        try
        {
            AuditPreviouslyCreatedOutputs();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var canvas = new GameObject("Authoring canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var image = new GameObject("Coloring image", typeof(RectTransform), typeof(RawImage));
            image.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)image.transform; rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.sizeDelta = new Vector2(600, 300);
            game = image.AddComponent<ColoringPageMinigame>(); game.coloringImage = image.GetComponent<RawImage>(); game.paintings = Array.Empty<ColoringArtworkDefinition>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Texture2D templateSource = MakeSource(folder + "/PresetTemplate.png", 554, 416, false);
            var template = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(templateSource));
            var presetType = new Preset(template).GetPresetType();
            DefaultPreset[] originalDefaults = Preset.GetDefaultPresetsForType(presetType);
            try
            {
                foreach (Vector2Int size in new[] { new Vector2Int(554, 416), new Vector2Int(1024, 683) })
                {
                    Preset.SetDefaultPresetsForType(presetType, Array.Empty<DefaultPreset>());
                    string sourcePath = folder + "/Source" + size.x + ".png";
                    Texture2D source = MakeSource(sourcePath, size.x, size.y, false);
                    Check(source.width == size.x && source.height == size.y, "NPOT source imported without resizing: " + size);
                    string sourceHash = Hash(sourcePath);
                    for (int policy = 0; policy < 3; policy++)
                    {
                        template.npotScale = policy == 1 ? TextureImporterNPOTScale.None : TextureImporterNPOTScale.ToNearest;
                        template.maxTextureSize = policy == 1 ? 256 : 2048;
                        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" }) template.ClearPlatformTextureSettings(platform);
                        if (policy == 2)
                        foreach (string platform in new[] { "Standalone", "Android" })
                        {
                            var setting = template.GetPlatformTextureSettings(platform);
                            setting.name = platform; setting.overridden = true; setting.maxTextureSize = 128;
                            setting.format = TextureImporterFormat.RGBA32; setting.textureCompression = TextureImporterCompression.Uncompressed;
                            template.SetPlatformTextureSettings(setting);
                        }
                        var preset = new Preset(template);
                        string presetPath = folder + "/Policy" + size.x + "_" + policy + ".preset";
                        AssetDatabase.CreateAsset(preset, presetPath);
                        Preset.SetDefaultPresetsForType(presetType, new[] { new DefaultPreset { preset = preset, filter = "", enabled = true } });
                        var window = Analyze(source, "Created_" + size.x + "_" + policy, 20);
                        var before = game.paintings.ToArray();
                        try
                        {
                            Check(Get<List<ColoringRegionSpanItem>>(window, "calculatedRegions").Count == 2, "Real analysis identifies the two source regions");
                            Check(window.TryAddArtworkToWorkshop(out var added, out string error), "Real save/import/append path succeeds: " + error);
                            Check(game.paintings.Length == before.Length + 1 && before.Select((a, i) => ReferenceEquals(a, game.paintings[i])).All(v => v),
                                "Append preserves every previously authored entry");
                            CheckOutputs(added, size.x, size.y, policy);
                            Check(Hash(sourcePath) == sourceHash, "Source PNG unchanged by authoring");
                        }
                        finally { UnityEngine.Object.DestroyImmediate(window); }
                    }
                }
                Preset.SetDefaultPresetsForType(presetType, Array.Empty<DefaultPreset>());
                var tiny = MakeSource(folder + "/TinySource.png", 554, 416, true);
                template.npotScale = TextureImporterNPOTScale.None; template.maxTextureSize = 32;
                foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" }) template.ClearPlatformTextureSettings(platform);
                var tinyPreset = new Preset(template); AssetDatabase.CreateAsset(tinyPreset, folder + "/TinyPolicy.preset");
                Preset.SetDefaultPresetsForType(presetType, new[] { new DefaultPreset { preset = tinyPreset, filter = "", enabled = true } });
                var rejectWindow = Analyze(tiny, "MustNotAttach", 1);
                try
                {
                    Check(Get<List<ColoringRegionSpanItem>>(rejectWindow, "calculatedRegions").Count == 3, "Tiny source has 3 regions before downscale");
                    var before = game.paintings.ToArray(); int assetsBefore = Directory.GetFiles(folder).Length;
                    Check(!rejectWindow.TryAddArtworkToWorkshop(out var rejected, out string error) && rejected == null && error.Contains("mất vùng"),
                        "Region lost by severe resize rejects save with an actionable message");
                    Check(game.paintings.SequenceEqual(before) && Directory.GetFiles(folder).Length == assetsBefore, "Rejected resize attaches nothing and removes only its fresh outputs");
                    var badRegions = Get<List<ColoringRegionSpanItem>>(rejectWindow, "calculatedRegions");
                    var bad = badRegions[0]; bad.pixelCount++; badRegions[0] = bad;
                    Check(!rejectWindow.TryAddArtworkToWorkshop(out _, out string spanError) && spanError.Contains("pixelCount"), "Invalid source span metadata rejects save");
                    Check(game.paintings.SequenceEqual(before) && Directory.GetFiles(folder).Length == assetsBefore, "Invalid metadata also leaves workshop/assets intact");
                }
                finally { UnityEngine.Object.DestroyImmediate(rejectWindow); }
            }
            finally { Preset.SetDefaultPresetsForType(presetType, originalDefaults); }
            Check(game.paintings.Length == 6, "Exactly 6 valid artworks appended; both rejection cases omitted");
            string fixtureScene = folder + "/AuthoringRuntime.unity";
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), fixtureScene);
            SessionState.SetString(Flag + ".editor", string.Join("\n", results));
            SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Finish(error); }
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Warning; Application.logMessageReceived += Warning;
    }
    private static void Warning(string message, string stack, LogType type)
    {
        if (message.Contains("[ColoringPageMinigame]") && (type == LogType.Warning || type == LogType.Error || type == LogType.Exception)) warnings.Add(message);
    }
    private static Color32 Pixel(Vector2 uv)
    {
        var texture = (Texture2D)game.coloringImage.texture;
        return texture.GetPixels32()[Mathf.FloorToInt(uv.y * texture.height) * texture.width + Mathf.FloorToInt(uv.x * texture.width)];
    }
    private static void Click(Vector2 uv)
    {
        Canvas.ForceUpdateCanvases();
        var rect = game.coloringImage.rectTransform;
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(rect.rect.xMin + uv.x * rect.rect.width, rect.rect.yMin + uv.y * rect.rect.height)))
        };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject == game.gameObject, "Fixture UI raycast selects the authored artwork");
        pointer.pointerPressRaycast = hits[0]; ExecuteEvents.Execute(game.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (started == 0)
        {
            started = EditorApplication.timeSinceStartup; next = started + 1;
            results.Clear(); results.AddRange(SessionState.GetString(Flag + ".editor", "").Split('\n'));
            game = UnityEngine.Object.FindAnyObjectByType<ColoringPageMinigame>();
        }
        if (EditorApplication.timeSinceStartup - started > 60) { Finish(new TimeoutException("Authoring runtime test timeout")); return; }
        if (EditorApplication.timeSinceStartup < next) return; next = EditorApplication.timeSinceStartup + 0.4;
        try
        {
            if (stage < 6)
            {
                game.SelectPainting(stage);
                Check(Get<ColoringRegionSpanItem[]>(game, "cachedRegionSpans")?.Length == 2, "Runtime accepts generated cache: " + game.paintings[stage].title);
                Click(new Vector2(0.01f, 0.01f));
                Check(Get<int>(game, "paintedRegionCount") == 0, "Background click does not paint");
                game.SelectPaletteIndex(1); Click(new Vector2(0.25f, 0.5f));
                Check(Pixel(new Vector2(0.25f, 0.5f)).Equals((Color32)game.palette[1])
                    && Pixel(new Vector2(0.75f, 0.5f)).Equals((Color32)Color.white) && Get<int>(game, "paintedRegionCount") == 1, "Left region paints independently at its source UV");
                game.SelectPaletteIndex(2); Click(new Vector2(0.75f, 0.5f));
                Check(Pixel(new Vector2(0.75f, 0.5f)).Equals((Color32)game.palette[2]) && Get<int>(game, "paintedRegionCount") == 2, "Right region paints at its source UV; progress 2/2");
                var data = JsonUtility.FromJson<ColoringRegionDataAsset>(game.paintings[stage].regionData.text);
                int[] labels = Labels(data); var pixels = ((Texture2D)game.coloringImage.texture).GetPixels32(); var original = game.paintings[stage].lineArt.GetPixels32();
                for (int i = 0; i < labels.Length; i++)
                    if (labels[i] == 0 && !pixels[i].Equals(original[i])) throw new Exception("Painting leaked into line/background");
                Check(true, "All unlabelled pixels remain unchanged after both region clicks");
            }
            else if (stage == 6)
            {
                game.SelectPainting(0);
                Check(Get<int>(game, "paintedRegionCount") == 2 && Pixel(new Vector2(0.25f, 0.5f)).Equals((Color32)game.palette[1]), "Switching back preserves generated artwork colors/progress");
                game.gameObject.SetActive(false); game.gameObject.SetActive(true); game.SelectPainting(0);
                Check(Get<int>(game, "paintedRegionCount") == 2, "Close/reopen preserves generated progress");
                Check(warnings.Count == 0, "No generated-artwork warning/fallback during Start, selection, clicks or close/reopen");
                Finish(null); return;
            }
            stage++;
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Warning; SessionState.SetBool(Flag, false);
        results.AddRange(warnings.Select(w => "UNEXPECTED ARTWORK WARNING: " + w));
        results.Add(error == null ? "RESULT: PASS / " + EditorUserBuildSettings.activeBuildTarget + " / " + results.Count(r => r.StartsWith("PASS:"))
            + " assertions / 6 real creations, 2 rejected saves, 7 runtime stages. Physical device input/build not verified."
            : "RESULT: FAIL stage=" + stage + "\n" + error);
        Directory.CreateDirectory("Logs");
        string suffix = EditorUserBuildSettings.activeBuildTarget.ToString();
        File.WriteAllLines("Logs/ArtworkAuthoring_" + suffix + "Results.txt", results);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
