using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Automated entry points may exit the Editor. Run on an isolated project copy.
public static class TestColorPreprocessing
{
    private static readonly List<string> results = new List<string>();
    private static int checks;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; results.Add("PASS: " + message); }
    private static Color32[] Flat(int w, int h, Color32 color) => Enumerable.Repeat(color, w * h).ToArray();
    private static readonly Color32 Black = new Color32(20, 20, 20, 255);
    private static ColorArtworkSegmentation.Options Settings() => new ColorArtworkSegmentation.Options
        { MergeDistance = 24, MinimumPixels = 200, Denoise = 0.8f, KeepEdges = 0.7f, PreserveInk = true };
    private static void Validate(ColorArtworkSegmentation a, ColorArtworkSegmentation.Output o)
    {
        var used = new bool[a.Width * a.Height];
        foreach (var region in o.Regions)
        {
            int count = 0;
            foreach (var span in region.spans)
            {
                Check(span.start >= 0 && span.length > 0 && span.start + span.length <= used.Length
                    && span.start / a.Width == (span.start + span.length - 1) / a.Width, "Span stays inside one image row");
                for (int p = span.start; p < span.start + span.length; p++)
                { if (used[p] || o.Mask[p].r != 255 || o.Lines[p].r != 255) throw new Exception("Overlap or disagreement at " + p); used[p] = true; count++; }
            }
            Check(count == region.pixelCount, "pixelCount matches actual span coverage");
        }
        for (int p = 0; p < used.Length; p++) if (used[p] != (o.Mask[p].r == 255)) throw new Exception("Mask has unlabelled pixel");
        Check(true, "Every preview pixel agrees with the span map");
    }
    public static void Run()
    {
        results.Clear(); checks = 0; Directory.CreateDirectory("Logs/ColorPreprocessing");
        const int w = 160, h = 96;
        var close = Flat(w, h, new Color32(235, 130, 100, 255));
        for (int y = 0; y < h; y++) for (int x = 80; x < w; x++) close[y * w + x] = new Color32(225, 135, 105, 255);
        for (int y = 0; y < h; y++) close[y * w + 79] = Black;
        var original = (Color32[])close.Clone();
        var a = ColorArtworkSegmentation.Analyze(close, w, h, Settings()); var o = a.BuildOutput(new HashSet<int>(), 1);
        Check(close.SequenceEqual(original), "Preprocessing never writes input pixels");
        Check(a.Labels[48 * w + 40] != a.Labels[48 * w + 110], "Close colours remain separate across a one-pixel ink stroke");
        Check(Enumerable.Range(0, h).All(y => o.Lines[y * w + 79].r == 0 && o.Mask[y * w + 79].r == 0), "Entire thin stroke survives filtering and export");
        Check(o.Lines[48 * w + 78].r == 255 && o.Lines[48 * w + 80].r == 255, "No second generated line thickens the native stroke");
        Check(o.LostRegions == 0, "Thin stroke fixture adds no lost interiors"); Validate(a, o);
        Check(!a.MergeAdjacent(a.Labels[48 * w + 40], a.Labels[48 * w + 110]), "Manual merge cannot cross ink or join disconnected interiors");
        // Four-pixel coloured detail enclosed by ink: smaller than the requested merge threshold.
        var detail = Flat(w, h, new Color32(240, 190, 70, 255));
        for (int y = 40; y < 44; y++) for (int x = 60; x < 64; x++) detail[y * w + x] = Black;
        for (int y = 41; y < 43; y++) for (int x = 61; x < 63; x++) detail[y * w + x] = new Color32(245, 225, 200, 255);
        a = ColorArtworkSegmentation.Analyze(detail, w, h, Settings()); o = a.BuildOutput(new HashSet<int>(), 1);
        Check(o.Regions.Any(r => r.pixelCount == 4) && o.Mask[41 * w + 61].r == 255, "Enclosed four-pixel detail survives even at min area 200");
        Check(o.LostRegions == 0, "Enclosed small detail remains paintable"); Validate(a, o);
        // Native ink with either a bright intended gap or a faint neutral hint.
        close[48 * w + 79] = new Color32(240, 170, 110, 255); var gap = Settings(); gap.JoinFaintGaps = true;
        a = ColorArtworkSegmentation.Analyze(close, w, h, gap);
        Check(!a.Ink[48 * w + 79], "Intentional bright gap is never closed");
        close[48 * w + 79] = new Color32(125, 125, 125, 255);
        a = ColorArtworkSegmentation.Analyze(close, w, h, Settings());
        Check(!a.Ink[48 * w + 79], "Faint-gap joining stays opt-in, not hidden in anti-alias cleanup");
        a = ColorArtworkSegmentation.Analyze(close, w, h, gap);
        Check(a.Ink[48 * w + 79], "One-pixel neutral faded ink hint can join a stroke");
        var alpha = Flat(w, h, new Color32(250, 240, 230, 255));
        alpha[0] = new Color32(0, 0, 0, 64); alpha[1] = new Color32(220, 180, 120, 192);
        a = ColorArtworkSegmentation.Analyze(alpha, w, h, Settings()); o = a.BuildOutput(new HashSet<int>(), 1);
        Check(a.Labels[0] == 0 && !a.Ink[0] && o.Mask[0].r == 0 && a.Labels[1] > 0, "Alpha cutoff uses original alpha, never hidden RGB or filter bleed");
        var noisy = Flat(w, h, new Color32(220, 170, 90, 255)); var random = new System.Random(432);
        for (int p = 0; p < noisy.Length; p++) { int n = random.Next(-15,16); noisy[p] = new Color32((byte)(220+n),(byte)(170+n),(byte)(90+n),255); }
        a = ColorArtworkSegmentation.Analyze(noisy, w, h, Settings()); o = a.BuildOutput(new HashSet<int>(), 1);
        Check(a.RegionCount == 1 && o.LostRegions == 0, "Deterministic paper texture stays a single paint region");
        double before = noisy.Sum(c => Math.Pow(c.r - 220,2)), after = a.Filtered.Sum(c => Math.Pow(c.r - 220,2));
        Check(after < before * 0.6, "Edge-aware filter reduces texture variance, not just region count");
        var gradient = new Color32[w*h]; for(int y=0;y<h;y++)for(int x=0;x<w;x++) gradient[y*w+x]=new Color32((byte)(160+x/4),(byte)(120+x/4),(byte)(80+x/4),255);
        a = ColorArtworkSegmentation.Analyze(gradient, w, h, Settings());
        Check(a.RegionCount <= 4, "Smooth gradient does not become hundreds of islands");
        // Cancellation before/within processing and during output leaves data untouched.
        var canceled = Settings(); canceled.Cancel = (phase, progress) => progress >= 0.1f;
        bool aborted = false; try { ColorArtworkSegmentation.Analyze(original, w, h, canceled); } catch(OperationCanceledException) { aborted=true; }
        Check(aborted && original[79].Equals(Black), "Cancellation interrupts filtering without modifying input");
        var outputCancel = Settings(); a = ColorArtworkSegmentation.Analyze(original,w,h,outputCancel); var labels=(int[])a.Labels.Clone(); outputCancel.Cancel=(phase,progress)=>true;
        aborted=false; try {a.BuildOutput(new HashSet<int>(),1);}catch(OperationCanceledException){aborted=true;}
        Check(aborted && a.Labels.SequenceEqual(labels), "Output cancellation does not partially rewrite labels");
        var limited=Settings();limited.TimeLimitSeconds=0.00001;bool timedOut=false;
        try{ColorArtworkSegmentation.Analyze(original,w,h,limited);}catch(InvalidOperationException){timedOut=true;}
        Check(timedOut,"Processing time budget stops work");
        bool oversized=false;try{ColorArtworkSegmentation.Analyze(original,4096,4096,Settings());}catch(ArgumentException){oversized=true;}
        Check(oversized,"Oversized input rejected before pipeline buffers are allocated");
        // Preview coordinates under zoom/pan are GUI content coordinates; Y flips once.
        Rect image=new Rect(40,20,640,384);
        Check(AddColoringArtworkWindow.ColorPreviewPixel(image,new Vector2(360,212),160,96)==new Vector2Int(80,48),"Zoomed preview center maps to exact source grid");
        Check(AddColoringArtworkWindow.ColorPreviewPixel(image,new Vector2(41,21),160,96)==new Vector2Int(0,95),"Preview upper-left maps to bottom-up texture Y once");
        using(var window=new Owner()) {
            var type=typeof(AddColoringArtworkWindow);var privateFlags=BindingFlags.Instance|BindingFlags.NonPublic;
            type.GetField("excludedColorRegions",privateFlags).GetValue(window.Value).GetType().GetMethod("Add").Invoke(type.GetField("excludedColorRegions",privateFlags).GetValue(window.Value),new object[]{9});
            window.Value.ApplyColorPreset(0);
            Check((float)type.GetField("colorMergeDistance",privateFlags).GetValue(window.Value)==12f,"Flat preset changes grouping control");
            Check(((HashSet<int>)type.GetField("excludedColorRegions",privateFlags).GetValue(window.Value)).Count==0,"Preset change resets manual edits even without source");
            window.Value.ApplyColorPreset(2);Check(!(bool)type.GetField("preserveColorInk",privateFlags).GetValue(window.Value),"Photo preset does not assume all shadows are ink");
        }
        PreviewRecoveryChecks();
        const int size=2048; var large=Flat(size,size,new Color32(225,185,95,255));long baseline=GC.GetTotalMemory(true), peak=baseline;
        var perf=Settings();perf.Cancel=(phase,progress)=>{peak=Math.Max(peak,GC.GetTotalMemory(false));return false;};var clock=Stopwatch.StartNew();
        a=ColorArtworkSegmentation.Analyze(large,size,size,perf);o=a.BuildOutput(new HashSet<int>(),1);clock.Stop();peak=Math.Max(peak,GC.GetTotalMemory(false));
        Check(o.LostRegions==0 && o.Regions.Count==1,"Four-million-pixel pipeline keeps one complete region");
        Check(clock.Elapsed.TotalSeconds<30,"Large fixture completes within 30 seconds");
        results.Add("PERF: 2048x2048; seconds="+clock.Elapsed.TotalSeconds+"; sampled managed delta bytes="+(peak-baseline)+"; no hardware/total-process peak claim");
        results.Add("RESULT: PASS; "+checks+" assertions; edge/ink/alpha/gap/span/cancel/preview/budget fixtures");
        File.WriteAllLines("Logs/ColorPreprocessing/QualityResults.txt",results);
    }

    private const BindingFlags PreviewPrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T PreviewGet<T>(object value, string name)
        => (T)value.GetType().GetField(name, PreviewPrivate).GetValue(value);
    private static void PreviewSet(object value, string name, object fieldValue)
        => value.GetType().GetField(name, PreviewPrivate).SetValue(value, fieldValue);
    private static void PreviewRefresh(AddColoringArtworkWindow window)
        => typeof(AddColoringArtworkWindow).GetMethod("RefreshColorPreview", PreviewPrivate).Invoke(window, null);

    // Real preview cancellation/timeout and complete export, only on the isolated project copy.
    private static void PreviewRecoveryChecks()
    {
        const int width = 128, height = 80, separatorX = 64;
        string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/PreviewRecoveryFixture");
        AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        string sourcePath = folder + "/BlackFill.png";
        var input = Flat(width, height, new Color32(20, 20, 20, 255));
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try { texture.SetPixels32(input); texture.Apply(); File.WriteAllBytes(sourcePath, texture.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false; importer.SaveAndReimport();
        var sourceBytes = File.ReadAllBytes(sourcePath);
        var sourceMeta = File.ReadAllBytes(sourcePath + ".meta");
        var testObject = new GameObject("PreviewRecoveryWorkshop");
        testObject.SetActive(false);
        var workshop = testObject.AddComponent<ColoringPageMinigame>();
        try
        {
            using (var owner = new Owner())
            {
                var window = owner.Value;
                PreviewSet(window, "workshop", workshop);
                PreviewSet(window, "sourceMode", 1);
                PreviewSet(window, "artworkName", "RecoveredPreview");
                PreviewSet(window, "originalImage", AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath));
                window.ApplyColorPreset(0);
                window.EditColorBoundary(new Vector2Int(separatorX, 0), new Vector2Int(separatorX, height - 1), 0, false);
                var analysis = PreviewGet<ColorArtworkSegmentation>(window, "colorAnalysis");
                var settings = PreviewGet<ColorArtworkSegmentation.Options>(analysis, "options");
                var originalCancel = settings.Cancel;
                double originalLimit = settings.TimeLimitSeconds;
                var labels = (int[])analysis.Labels.Clone();
                var expectedLine = PreviewGet<Texture2D>(window, "previewLineArt").GetPixels32();
                var expectedMask = PreviewGet<Texture2D>(window, "previewMask").GetPixels32();
                string expectedSpans = JsonUtility.ToJson(new ColoringRegionDataAsset
                {
                    width = width, height = height,
                    regionCount = PreviewGet<List<ColoringRegionSpanItem>>(window, "calculatedRegions").Count,
                    regions = PreviewGet<List<ColoringRegionSpanItem>>(window, "calculatedRegions").ToArray()
                });
                Check(PreviewGet<string>(window, "colorAnalysisError") == null
                    && PreviewGet<int>(window, "fillableRegionCount") == 2
                    && expectedLine[40 * width + separatorX].r == 0,
                    "Valid preview contains the hand-drawn separator and two paintable interiors");

                foreach (bool timeout in new[] { false, true })
                {
                    int callbacks = 0;
                    var previousPreview = PreviewGet<Texture2D>(window, "previewLineArt");
                    settings.Cancel = (phase, progress) => { callbacks++; return !timeout && progress >= 0.25f; };
                    if (timeout) settings.TimeLimitSeconds = 0;
                    PreviewRefresh(window);
                    string failure = PreviewGet<string>(window, "colorAnalysisError");
                    Check(callbacks > 0 && failure != null && failure.Contains(timeout ? "vượt giới hạn" : "Đã hủy"),
                        (timeout ? "Timeout" : "Cancellation") + " in BuildOutput marks the preview invalid");
                    Check(ReferenceEquals(previousPreview, PreviewGet<Texture2D>(window, "previewLineArt")),
                        "Failed refresh retains the last preview without treating it as saveable");
                    int assetCount = Directory.GetFiles(folder).Length;
                    Check(!window.TryAddArtworkToWorkshop(out var rejected, out string rejection)
                        && rejected == null && rejection != null && Directory.GetFiles(folder).Length == assetCount,
                        "Failed preview blocks the real save path without creating assets");
                    Check(workshop.paintings.Length == 0, "Failed save never attaches an artwork");
                    settings.Cancel = originalCancel; settings.TimeLimitSeconds = originalLimit;
                    PreviewRefresh(window);
                    Check(PreviewGet<string>(window, "colorAnalysisError") == null,
                        "Successful retry clears the preview error after complete refresh");
                    Check(ReferenceEquals(analysis, PreviewGet<ColorArtworkSegmentation>(window, "colorAnalysis"))
                        && analysis.Labels.SequenceEqual(labels), "Retry keeps the existing analysis instead of resetting manual edits");
                    Check(PreviewGet<Texture2D>(window, "previewLineArt").GetPixels32().SequenceEqual(expectedLine)
                        && PreviewGet<Texture2D>(window, "previewMask").GetPixels32().SequenceEqual(expectedMask),
                        "Retry retains every hand-drawn line and mask pixel");
                    Check(PreviewGet<int>(window, "fillableRegionCount") == 2
                        && JsonUtility.ToJson(new ColoringRegionDataAsset
                        {
                            width = width, height = height, regionCount = 2,
                            regions = PreviewGet<List<ColoringRegionSpanItem>>(window, "calculatedRegions").ToArray()
                        }) == expectedSpans, "Retry restores both independent interiors with identical spans");
                }
                Check(window.TryAddArtworkToWorkshop(out var artwork, out string error),
                    "The real save path succeeds after preview recovery: " + error);
                Check(workshop.paintings.Length == 1 && ReferenceEquals(workshop.paintings[0], artwork),
                    "Recovered artwork is attached exactly once");
                Check(artwork.lineArt.GetPixels32().SequenceEqual(expectedLine)
                    && artwork.paintMask.GetPixels32().SequenceEqual(expectedMask)
                    && artwork.regionData.text == expectedSpans,
                    "Export/import preserves the recovered preview pixels and manual split spans");
                Check(File.ReadAllBytes(sourcePath).SequenceEqual(sourceBytes)
                    && File.ReadAllBytes(sourcePath + ".meta").SequenceEqual(sourceMeta),
                    "Preview recovery and export leave source pixels/importer unchanged");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(testObject); }
    }
    public static void RunAndExit(){try{Run();EditorApplication.Exit(0);}catch(Exception e){Directory.CreateDirectory("Logs/ColorPreprocessing");File.WriteAllText("Logs/ColorPreprocessing/QualityFailure.txt",e.ToString());UnityEngine.Debug.LogException(e);EditorApplication.Exit(1);}}
    public static void RunAndAuthoring(){Run();TestColorReferenceAuthoring.BeginWithRegression();}
    public static void AuditCrossTargetAndExit()
    {
        try
        {
            // Reimported saved assets use the existing independent pixel/span/runtime audit.
            // No new artworks, scene saves or authoring calls are performed here.
            var type = typeof(TestColorReferenceAuthoring);
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            type.GetMethod("AuditPreviousGeneratedReferences", flags).Invoke(null, null);
            var lines = (List<string>)type.GetField("results", flags).GetValue(null);
            Directory.CreateDirectory("Logs/ColorPreprocessing");
            File.WriteAllLines("Logs/ColorPreprocessing/CrossTargetResults.txt", lines.Concat(new[] { "RESULT: PASS; saved Windows/Android assets audited after reimport; no new exports" }));
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Directory.CreateDirectory("Logs/ColorPreprocessing");
            File.WriteAllText("Logs/ColorPreprocessing/CrossTargetFailure.txt", e.ToString());
            UnityEngine.Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
    private sealed class Owner:IDisposable{internal readonly AddColoringArtworkWindow Value=ScriptableObject.CreateInstance<AddColoringArtworkWindow>();public void Dispose()=>UnityEngine.Object.DestroyImmediate(Value);}
}
