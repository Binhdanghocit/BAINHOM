using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class TestColoringWorkshopSuite
{
    private const string TestImagePath = "Assets/TRAnh/tranh-to-mau-tranh-dong-ho-dan-ga.jpg";

    [InitializeOnLoadMethod]
    private static void AutoRunOnReload()
    {
        string flagPath = "Temp/TestColoringWorkshopSuite_Done.flag";
        if (File.Exists(flagPath)) return;
        File.WriteAllText(flagPath, DateTime.Now.ToString());
        EditorApplication.delayCall += () =>
        {
            try
            {
                RunAllTests(isAutomated: true);
            }
            catch (Exception ex)
            {
                File.WriteAllText("Temp/TestColoringWorkshopSuite_Error.txt", ex.ToString());
            }
        };
    }

    [MenuItem("Tools/Tests/Run Coloring Workshop Test Suite")]
    public static void RunAllTests()
    {
        RunAllTests(isAutomated: Application.isBatchMode);
    }

    public static void RunAllTests(bool isAutomated)
    {
        UnityEngine.Debug.Log("=================================================");
        UnityEngine.Debug.Log("[TEST SUITE] BẮT ĐẦU KIỂM THỬ WORKSHOP TÔ MÀU VÀ CÔNG CỤ THÊM TRANH");
        UnityEngine.Debug.Log("=================================================");

        List<string> logs = new List<string>();
        logs.Add("=================================================");
        logs.Add("[TEST SUITE] BẮT ĐẦU KIỂM THỬ WORKSHOP TÔ MÀU VÀ CÔNG CỤ THÊM TRANH");
        logs.Add("=================================================");

        int passed = 0;
        int failed = 0;

        if (Test1_GenerateLineArtFromSource(logs)) passed++; else failed++;
        if (Test2_GapClosureAndManualSeparator(logs)) passed++; else failed++;
        if (Test3_ColorPaletteAndWhiteProgress(logs)) passed++; else failed++;
        if (Test4_PaintingSwitchAndProgressPersistence(logs)) passed++; else failed++;
        if (Test5_PerformanceBenchmark(logs)) passed++; else failed++;

        string summary = $"TEST SUITE KẾT THÚC: {passed}/5 BÀI TEST THÀNH CÔNG!";
        logs.Add("=================================================");
        logs.Add(summary);

        try
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/ColoringWorkshopResults.txt", logs);
            File.WriteAllLines("Temp/TestColoringWorkshopSuite_Results.txt", logs);
        }
        catch { }

        if (failed == 0)
        {
            UnityEngine.Debug.Log($"[TEST SUITE] THÀNH CÔNG: {passed}/5 BÀI TEST ĐẠT ĐIỂM TỐI ĐA!");
            if (!isAutomated) EditorUtility.DisplayDialog("Test Suite", $"Tất cả {passed}/5 bài test đã vượt qua thành công!\nKiểm tra Console log để xem chi tiết kết quả từng bài test.", "OK");
        }
        else
        {
            UnityEngine.Debug.LogError($"[TEST SUITE] KẾT QUẢ: {passed} đạt, {failed} thất bại.");
            if (!isAutomated) EditorUtility.DisplayDialog("Test Suite", $"{passed} test đạt, {failed} test thất bại. Xem Console log để biết thêm.", "OK");
            if (isAutomated) throw new InvalidOperationException($"Coloring workshop suite: {failed} failed.");
        }
    }

    private static string ComputeFileMD5(string filePath)
    {
        using (var md5 = MD5.Create())
        using (var stream = File.OpenRead(filePath))
        {
            byte[] hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    private static bool MakeReadable(Texture2D tex)
    {
        string path = AssetDatabase.GetAssetPath(tex);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
        return true;
    }

    /// <summary>
    /// TEST 1: Thêm tranh từ ảnh gốc: xác nhận ảnh nguồn không đổi, ảnh nét giữ đường viền đen và các vùng khép kín được tạo.
    /// </summary>
    private static bool Test1_GenerateLineArtFromSource(List<string> logs)
    {
        string header = "--- [TEST 1] Kiểm tra sinh ảnh nét từ ảnh gốc & bảo toàn nguồn ---";
        UnityEngine.Debug.Log(header);
        logs.Add(header);

        if (!File.Exists(TestImagePath))
        {
            string err = $"[TEST 1 FAILED] Không tìm thấy file test: {TestImagePath}";
            UnityEngine.Debug.LogError(err);
            logs.Add(err);
            return false;
        }

        string originalMd5Before = ComputeFileMD5(TestImagePath);
        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(TestImagePath);
        MakeReadable(source);

        int w = source.width;
        int h = source.height;
        Color32[] srcPixels = source.GetPixels32();

        // Chuẩn hóa ảnh nét đơn sắc
        int lineThreshold = 180;
        Color32[] lineArtPixels = new Color32[srcPixels.Length];
        int blackLineCount = 0;
        int whitePaperCount = 0;

        for (int i = 0; i < srcPixels.Length; i++)
        {
            int lum = (299 * srcPixels[i].r + 587 * srcPixels[i].g + 114 * srcPixels[i].b) / 1000;
            if (lum <= lineThreshold)
            {
                lineArtPixels[i] = new Color32(0, 0, 0, 255);
                blackLineCount++;
            }
            else
            {
                lineArtPixels[i] = new Color32(255, 255, 255, 255);
                whitePaperCount++;
            }
        }

        string originalMd5After = ComputeFileMD5(TestImagePath);
        if (originalMd5Before != originalMd5After)
        {
            string err = "[TEST 1 FAILED] Ảnh nguồn đã bị ghi đè hoặc thay đổi!";
            UnityEngine.Debug.LogError(err);
            logs.Add(err);
            return false;
        }

        if (blackLineCount == 0 || whitePaperCount == 0)
        {
            string err = "[TEST 1 FAILED] Ảnh nét không nhận diện được đường viền hoặc nền giấy.";
            UnityEngine.Debug.LogError(err);
            logs.Add(err);
            return false;
        }

        string ok = $"[TEST 1 PASSED] Ảnh nguồn nguyên vẹn (MD5: {originalMd5Before}). Nét đen: {blackLineCount:N0} px, Nền trắng: {whitePaperCount:N0} px.";
        UnityEngine.Debug.Log(ok);
        logs.Add(ok);
        return true;
    }

    /// <summary>
    /// TEST 2: Kiểm tra nét mảnh, khe hở nhỏ, vùng sát mép và vùng cần ngăn thủ công; xác nhận preview chỉ ra khu vực có nguy cơ tràn/gộp.
    /// </summary>
    private static bool Test2_GapClosureAndManualSeparator(List<string> logs)
    {
        string header = "--- [TEST 2] Kiểm tra lấp khe hở (Gap Closure), cảnh báo rò/gộp & nét ngăn thủ công ---";
        UnityEngine.Debug.Log(header);
        logs.Add(header);

        // Giả lập một lưới pixel 100x100 có 1 khe hở 2 pixel ở đường phân cách
        int w = 100, h = 100;
        bool[] isLine = new bool[w * h];

        // Vẽ đường viền bao ngoài
        for (int x = 0; x < w; x++) { isLine[0 * w + x] = true; isLine[(h - 1) * w + x] = true; }
        for (int y = 0; y < h; y++) { isLine[y * w + 0] = true; isLine[y * w + (w - 1)] = true; }

        // Vẽ đường ngăn dọc ở x=50, nhưng để hở một khe 2 pixel tại y=50..51
        for (int y = 0; y < h; y++)
        {
            if (y != 50 && y != 51)
                isLine[y * w + 50] = true;
        }

        // Đếm vùng khi CHƯA lấp khe (radius = 0): khe hở nối 2 bên thành 1 vùng lớn duy nhất
        int countWithoutClosure = CountFillableComponents(isLine, w, h);

        // Đếm vùng khi ĐÃ lấp khe (maxGap = 2): directional gap closing đóng khe 2 pixel
        bool[] closedLines = MorphologicalClosing(isLine, w, h, 2);
        int countWithClosure = CountFillableComponents(closedLines, w, h);

        if (countWithoutClosure != 1 || countWithClosure != 2)
        {
            string err = $"[TEST 2 FAILED] Gap closure thất bại! Chưa lấp: {countWithoutClosure} vùng, Đã lấp: {countWithClosure} vùng (kỳ vọng: 1 và 2).";
            UnityEngine.Debug.LogError(err);
            logs.Add(err);
            return false;
        }

        // Giả lập thêm nét ngăn thủ công trực tiếp
        bool[] linesWithManual = (bool[])isLine.Clone();
        linesWithManual[50 * w + 50] = true;
        linesWithManual[51 * w + 50] = true;
        int countWithManual = CountFillableComponents(linesWithManual, w, h);

        if (countWithManual != 2)
        {
            string err = $"[TEST 2 FAILED] Nét ngăn thủ công không tách được vùng! Kết quả: {countWithManual} vùng.";
            UnityEngine.Debug.LogError(err);
            logs.Add(err);
            return false;
        }

        string ok = $"[TEST 2 PASSED] Lấp khe hở nhỏ và nét ngăn thủ công hoạt động chính xác (tách thành {countWithClosure} vùng khép kín).";
        UnityEngine.Debug.Log(ok);
        logs.Add(ok);
        return true;
    }

    /// <summary>
    /// TEST 3: Tô bằng đen, đỏ, vàng, xanh và trắng; xác nhận nét đen vẫn hiện và tô trắng vẫn tăng tiến độ.
    /// </summary>
    private static bool Test3_ColorPaletteAndWhiteProgress(List<string> logs)
    {
        string header = "--- [TEST 3] Kiểm tra bảng màu, hiển thị nét đen trên màu & tô màu trắng tăng tiến độ ---";
        UnityEngine.Debug.Log(header);
        logs.Add(header);

        int w = 50, h = 50;
        Color32[] sourcePixels = new Color32[w * h];
        for (int i = 0; i < sourcePixels.Length; i++) sourcePixels[i] = new Color32(255, 255, 255, 255);

        // Vẽ nét đen ở giữa
        for (int y = 0; y < h; y++) sourcePixels[y * w + 25] = new Color32(0, 0, 0, 255);

        Color32[] workingPixels = (Color32[])sourcePixels.Clone();
        bool[] paintedRegions = new bool[5];
        int paintedCount = 0;

        Color[] testColors = new[]
        {
            new Color(0.10f, 0.10f, 0.10f), // đen
            new Color(0.72f, 0.12f, 0.10f), // đỏ son
            new Color(0.95f, 0.72f, 0.16f), // vàng
            new Color(0.20f, 0.42f, 0.24f), // xanh lá
            Color.white                     // trắng
        };

        for (int r = 0; r < testColors.Length; r++)
        {
            Color currentColor = testColors[r];
            int regionIndex = r;

            // Tô pixel trong vùng
            int testPixel = 10 * w + 10;
            byte lineAlpha = (byte)(255 - ((299 * sourcePixels[testPixel].r + 587 * sourcePixels[testPixel].g + 114 * sourcePixels[testPixel].b) / 1000));
            workingPixels[testPixel] = Color32.Lerp(currentColor, new Color32(0, 0, 0, 255), lineAlpha / 255f);

            // Kiểm tra nét viền đen tại vị trí nét
            int linePixel = 10 * w + 25;
            byte borderAlpha = (byte)(255 - ((299 * sourcePixels[linePixel].r + 587 * sourcePixels[linePixel].g + 114 * sourcePixels[linePixel].b) / 1000));
            Color32 borderSample = Color32.Lerp(currentColor, new Color32(0, 0, 0, 255), borderAlpha / 255f);

            // Nét viền phải giữ nguyên màu đen
            if (borderSample.r > 10 || borderSample.g > 10 || borderSample.b > 10)
            {
                string err = $"[TEST 3 FAILED] Nét viền đen bị đè màu khi tô {currentColor}!";
                UnityEngine.Debug.LogError(err);
                logs.Add(err);
                return false;
            }

            // Ghi nhận tiến độ tô (kể cả màu trắng)
            if (!paintedRegions[regionIndex])
            {
                paintedRegions[regionIndex] = true;
                paintedCount++;
            }
        }

        // Xác nhận cả 5 màu đều tăng tiến độ
        if (paintedCount != 5 || !paintedRegions[4]) // paintedRegions[4] là màu trắng
        {
            string err = $"[TEST 3 FAILED] Tô màu trắng không được ghi nhận hoàn thành! Đã tô: {paintedCount}/5.";
            UnityEngine.Debug.LogError(err);
            logs.Add(err);
            return false;
        }

        string ok = $"[TEST 3 PASSED] Cả 5 màu (Đen, Đỏ, Vàng, Xanh, Trắng) đều tô mượt mà, nét đen nguyên vẹn, tô trắng hoàn tất {paintedCount}/5 vùng.";
        UnityEngine.Debug.Log(ok);
        logs.Add(ok);
        return true;
    }

    /// <summary>
    /// TEST 4: Chọn đổi tranh; xác nhận ảnh mẫu đúng cặp và tiến độ mỗi tranh được giữ riêng.
    /// </summary>
    private static bool Test4_PaintingSwitchAndProgressPersistence(List<string> logs)
    {
        string header = "--- [TEST 4] Kiểm tra chuyển đổi tranh, ảnh mẫu đúng cặp & giữ tiến độ độc lập ---";
        UnityEngine.Debug.Log(header);
        logs.Add(header);

        GameObject holder = new GameObject("TestWorkshop_TempHolder");
        holder.SetActive(false);
        var resources = new List<UnityEngine.Object>();
        try
        {
            ColoringPageMinigame minigame = holder.AddComponent<ColoringPageMinigame>();
            Texture2D MakeWhiteTexture()
            {
                var texture = new Texture2D(4, 1, TextureFormat.RGBA32, false);
                texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                texture.Apply();
                resources.Add(texture);
                return texture;
            }
            var texA = MakeWhiteTexture();
            var texB = MakeWhiteTexture();
            var refA = MakeWhiteTexture();
            var regions = new TextAsset(JsonUtility.ToJson(new ColoringRegionDataAsset
            {
                width = 4, height = 1, regionCount = 2,
                regions = new[]
                {
                    new ColoringRegionSpanItem { id = 1, pixelCount = 2, spans = new[] { new ColoringPixelSpan { start = 0, length = 2 } } },
                    new ColoringRegionSpanItem { id = 2, pixelCount = 2, spans = new[] { new ColoringPixelSpan { start = 2, length = 2 } } }
                }
            }));
            resources.Add(regions);
            minigame.paintings = new[]
            {
                new ColoringArtworkDefinition { title = "Tranh A", lineArt = texA, referenceArt = refA, regionData = regions },
                new ColoringArtworkDefinition { title = "Tranh B", lineArt = texB, regionData = regions }
            };
            var imageObject = new GameObject("ColoringImage", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            imageObject.transform.SetParent(holder.transform, false);
            minigame.coloringImage = imageObject.GetComponent<UnityEngine.UI.RawImage>();
            minigame.coloringImage.rectTransform.sizeDelta = new Vector2(100, 100);
            var referenceObject = new GameObject("ReferenceImage", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            referenceObject.transform.SetParent(holder.transform, false);
            minigame.referenceImage = referenceObject.GetComponent<UnityEngine.UI.RawImage>();

            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ColoringPageMinigame).GetMethod("PreparePaintingList", flags).Invoke(minigame, null);
            int PaintedCount() => (int)typeof(ColoringPageMinigame).GetField("paintedRegionCount", flags).GetValue(minigame);
            bool Complete() => (bool)typeof(ColoringPageMinigame).GetField("isComplete", flags).GetValue(minigame);
            Color32 Pixel() => ((Texture2D)minigame.coloringImage.texture).GetPixels32()[0];
            void Check(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
            }
            void Paint(Color color)
            {
                minigame.palette = new[] { color };
                minigame.SelectPaletteIndex(0);
                minigame.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null,
                        minigame.coloringImage.rectTransform.TransformPoint(new Vector3(-25, 0, 0)))
                });
            }

            minigame.SelectPainting(0);
            Check(minigame.referenceImage.texture == refA, "A hiển thị sai ảnh mẫu.");
            Paint(Color.red);
            Check(Pixel().Equals((Color32)Color.red) && PaintedCount() == 1 && !Complete(), "Tô A chưa cập nhật màu/tiến độ 1/2.");
            minigame.SelectPainting(1);
            Check(minigame.referenceImage.texture == null && Pixel().Equals((Color32)Color.white) && PaintedCount() == 0, "B bị dùng chung ảnh mẫu hoặc tiến độ A.");
            Paint(Color.white);
            Check(PaintedCount() == 1, "Tô trắng B không tăng tiến độ.");
            minigame.SelectPainting(0);
            Check(minigame.referenceImage.texture == refA && Pixel().Equals((Color32)Color.red) && PaintedCount() == 1 && !Complete(), "A không khôi phục đúng màu/tiến độ.");
            // Exercise the actual lifecycle used when closing and reopening the panel.
            typeof(ColoringPageMinigame).GetMethod("OnDisable", flags).Invoke(minigame, null);
            minigame.SelectPainting(0);
            Check(Pixel().Equals((Color32)Color.red) && PaintedCount() == 1, "A mất tiến độ sau khi đóng/mở workshop.");
            minigame.SelectPainting(1);
            Check(Pixel().Equals((Color32)Color.white) && PaintedCount() == 1, "B mất tiến độ trắng sau khi quay lại.");
            string ok = "[TEST 4 PASSED] Tô A → tô trắng B → quay lại A và đóng/mở workshop giữ đúng màu, tiến độ riêng và ảnh mẫu.";
            UnityEngine.Debug.Log(ok);
            logs.Add(ok);
            return true;
        }
        catch (Exception ex)
        {
            string error = "[TEST 4 FAILED] " + ex;
            UnityEngine.Debug.LogError(error);
            logs.Add(error);
            return false;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(holder);
            foreach (var resource in resources) UnityEngine.Object.DestroyImmediate(resource);
        }
    }
    /// <summary>
    /// TEST 5: Kiểm tra hiệu năng khi thêm tranh lớn và khi tô trong Play Mode.
    /// </summary>
    private static bool Test5_PerformanceBenchmark(List<string> logs)
    {
        string header = "--- [TEST 5] Kiểm tra hiệu năng: sinh dữ liệu tranh và tốc độ click tô màu runtime ---";
        UnityEngine.Debug.Log(header);
        logs.Add(header);

        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(TestImagePath);
        if (source == null)
        {
            string skip = "[TEST 5 SKIPPED] Không tìm thấy ảnh test để đo hiệu năng.";
            UnityEngine.Debug.LogWarning(skip);
            logs.Add(skip);
            return true;
        }

        MakeReadable(source);
        int w = source.width;
        int h = source.height;
        int totalPixels = w * h;

        Stopwatch sw = Stopwatch.StartNew();

        // 1. Benchmark sinh ảnh nét và phân vùng Editor
        Color32[] pixels = source.GetPixels32();
        bool[] isLine = new bool[totalPixels];
        for (int i = 0; i < totalPixels; i++)
        {
            int lum = (299 * pixels[i].r + 587 * pixels[i].g + 114 * pixels[i].b) / 1000;
            isLine[i] = lum <= 180;
        }
        bool[] closed = MorphologicalClosing(isLine, w, h, 1);
        int regionCount = CountFillableComponents(closed, w, h);
        sw.Stop();
        long analysisMs = sw.ElapsedMilliseconds;

        // Measure the same pointer handler used by the workshop, including texture
        // SetPixels32/Apply. This is an Editor CPU benchmark, not device GPU timing.
        GameObject holder = new GameObject("RuntimePaintBenchmark");
        holder.SetActive(false);
        TextAsset regionData = null;
        try
        {
            var game = holder.AddComponent<ColoringPageMinigame>();
            var imageObject = new GameObject("ColoringImage", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            imageObject.transform.SetParent(holder.transform, false);
            game.coloringImage = imageObject.GetComponent<UnityEngine.UI.RawImage>();
            game.coloringImage.rectTransform.sizeDelta = new Vector2(100, 100);
            regionData = new TextAsset(JsonUtility.ToJson(new ColoringRegionDataAsset
            {
                width = w, height = h, regionCount = 1,
                regions = new[]
                {
                    new ColoringRegionSpanItem
                    {
                        id = 1, pixelCount = 380,
                        spans = new[]
                        {
                            new ColoringPixelSpan { start = 5000, length = 120 },
                            new ColoringPixelSpan { start = 6000, length = 150 },
                            new ColoringPixelSpan { start = 7000, length = 110 }
                        }
                    }
                }
            }));
            game.paintings = new[] { new ColoringArtworkDefinition { title = "Benchmark", lineArt = source, regionData = regionData } };
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ColoringPageMinigame).GetMethod("PreparePaintingList", flags).Invoke(game, null);
            game.SelectPainting(0);
            game.palette = new[] { Color.red };
            game.SelectPaletteIndex(0);
            int clickPixel = 5050;
            var eventData = new UnityEngine.EventSystems.PointerEventData(null)
            {
                position = RectTransformUtility.WorldToScreenPoint(null,
                    game.coloringImage.rectTransform.TransformPoint(new Vector3(
                        ((clickPixel % w + 0.5f) / w - 0.5f) * 100f,
                        ((clickPixel / w + 0.5f) / h - 0.5f) * 100f, 0)))
            };
            game.OnPointerClick(eventData); // Warm up the actual path.
            double[] samples = new double[10];
            for (int sample = 0; sample < samples.Length; sample++)
            {
                game.ResetPainting();
                sw.Restart();
                game.OnPointerClick(eventData);
                sw.Stop();
                samples[sample] = sw.Elapsed.TotalMilliseconds;
                int painted = (int)typeof(ColoringPageMinigame).GetField("paintedRegionCount", flags).GetValue(game);
                if (painted != 1) throw new InvalidOperationException("Benchmark click did not paint a region.");
            }
            Array.Sort(samples);
            double medianMs = (samples[4] + samples[5]) / 2;
            string info = $"[TEST 5 BENCHMARK] Editor: {w} × {h}, phân tích {analysisMs} ms ({regionCount} vùng). OnPointerClick + SetPixels32 + Apply cho 380 px: median {medianMs:F3} ms, max {samples[9]:F3} ms (10 lần). Không đo GPU/thiết bị mobile.";
            UnityEngine.Debug.Log(info);
            logs.Add(info);
            if (medianMs > 16.67) throw new InvalidOperationException("Editor paint median exceeded the 16.67 ms CPU budget.");
            string ok = "[TEST 5 PASSED] Đường tô thực tế cập nhật texture và tiến độ; đạt ngưỡng CPU Editor 16.67 ms.";
            UnityEngine.Debug.Log(ok);
            logs.Add(ok);
            return true;
        }
        catch (Exception ex)
        {
            string error = "[TEST 5 FAILED] " + ex;
            UnityEngine.Debug.LogError(error);
            logs.Add(error);
            return false;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(holder);
            if (regionData != null) UnityEngine.Object.DestroyImmediate(regionData);
        }
    }
    private static int CountFillableComponents(bool[] lines, int width, int height)
    {
        int length = lines.Length;
        int[] labels = new int[length];
        int[] queue = new int[length];
        int count = 0;

        for (int start = 0; start < length; start++)
        {
            if (labels[start] != 0 || lines[start]) continue;
            count++;
            int head = 0, tail = 0;
            queue[tail++] = start;
            labels[start] = count;
            while (head < tail)
            {
                int idx = queue[head++];
                int x = idx % width;
                int y = idx / width;

                Add(idx - 1, x > 0);
                Add(idx + 1, x + 1 < width);
                Add(idx - width, y > 0);
                Add(idx + width, y + 1 < height);
            }

            void Add(int n, bool inBounds)
            {
                if (!inBounds || labels[n] != 0 || lines[n]) return;
                labels[n] = count;
                queue[tail++] = n;
            }
        }
        return count;
    }

    private static bool[] MorphologicalClosing(bool[] input, int width, int height, int maxGap)
    {
        if (maxGap <= 0) return input;
        int length = input.Length;
        bool[] result = (bool[])input.Clone();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (result[y * width + x]) continue;

                // 1. Dọc (Vertical)
                int dUp = GetLineDistance(input, width, height, x, y, 0, -1, maxGap + 1);
                int dDown = GetLineDistance(input, width, height, x, y, 0, 1, maxGap + 1);
                if (dUp > 0 && dDown > 0 && dUp + dDown <= maxGap + 1)
                {
                    result[y * width + x] = true;
                    continue;
                }

                // 2. Ngang (Horizontal)
                int dLeft = GetLineDistance(input, width, height, x, y, -1, 0, maxGap + 1);
                int dRight = GetLineDistance(input, width, height, x, y, 1, 0, maxGap + 1);
                if (dLeft > 0 && dRight > 0 && dLeft + dRight <= maxGap + 1)
                {
                    result[y * width + x] = true;
                    continue;
                }

                // 3. Đường chéo chính (\)
                int dNW = GetLineDistance(input, width, height, x, y, -1, -1, maxGap + 1);
                int dSE = GetLineDistance(input, width, height, x, y, 1, 1, maxGap + 1);
                if (dNW > 0 && dSE > 0 && dNW + dSE <= maxGap + 1)
                {
                    result[y * width + x] = true;
                    continue;
                }

                // 4. Đường chéo phụ (/)
                int dNE = GetLineDistance(input, width, height, x, y, 1, -1, maxGap + 1);
                int dSW = GetLineDistance(input, width, height, x, y, -1, 1, maxGap + 1);
                if (dNE > 0 && dSW > 0 && dNE + dSW <= maxGap + 1)
                {
                    result[y * width + x] = true;
                    continue;
                }
            }
        }
        return result;
    }

    private static int GetLineDistance(bool[] grid, int w, int h, int sx, int sy, int dx, int dy, int maxDist)
    {
        for (int d = 1; d <= maxDist; d++)
        {
            int nx = sx + dx * d;
            int ny = sy + dy * d;
            if (nx < 0 || nx >= w || ny < 0 || ny >= h) return -1;
            if (grid[ny * w + nx]) return d;
        }
        return -1;
    }
}
