using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class AddColoringArtworkWindow : EditorWindow
{
    private string artworkName = string.Empty;
    private Texture2D originalImage;
    private Texture2D cachedOriginalImage;
    private Texture2D referenceArt;
    private ColoringPageMinigame workshop;
    private Vector2 scroll;
    private float referenceScale = 1f;
    private Vector2 referenceOffset;

    // Phân tích và nhận diện nét
    private int lineThreshold = 180;
    private int gapClosureRadius = 1;
    private int minimumRegionPixels = 60;
    private bool smoothLines = true;
    private bool drawSeparatorMode = false;
    private int brushRadius = 2;
    private bool[,] manualStrokes;
    private Vector2Int lastDrawnPixel = new Vector2Int(-1, -1);

    // Caches kết quả phân tích
    private Texture2D previewLineArt;
    private Texture2D previewRegionOverlay;
    private Texture2D previewMask;
    private List<ColoringRegionSpanItem> calculatedRegions = new List<ColoringRegionSpanItem>();
    private readonly List<string> leakOrMergeWarnings = new List<string>();
    private readonly HashSet<int> warnedRegionIds = new HashSet<int>();
    private int fillableRegionCount;
    private int maskPixelCount;
    private Rect previewRect;
    private string previewProbe = "Rà chuột hoặc bấm vào preview để kiểm tra điểm ảnh/vùng.";

    // Chế độ cửa sổ
    private int editorMode = 0;
    private int alignmentIndex = -1;
    private ColoringPageMinigame alignmentWorkshop;
    private ColoringArtworkDefinition alignmentTarget;
    private bool alignmentDirty;

    [MenuItem("Tools/Workshop tô màu/Thêm cặp tranh")]
    private static void Open()
    {
        AddColoringArtworkWindow window = GetWindow<AddColoringArtworkWindow>("Thêm cặp tranh");
        window.minSize = new Vector2(460f, 620f);
        window.FindWorkshop();
    }

    private void OnDisable()
    {
        ReleasePreviewTextures();
    }

    private void ReleasePreviewTextures()
    {
        if (previewLineArt != null) { DestroyImmediate(previewLineArt); previewLineArt = null; }
        if (previewRegionOverlay != null) { DestroyImmediate(previewRegionOverlay); previewRegionOverlay = null; }
        if (previewMask != null) { DestroyImmediate(previewMask); previewMask = null; }
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        editorMode = GUILayout.Toolbar(editorMode, new[] { "Thêm tranh từ ảnh gốc", "Căn tranh đã thêm" });
        if (editorMode == 1)
        {
            DrawExistingAlignmentEditor();
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.LabelField("Thêm tranh vào workshop đang mở", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Cấu hình một lần gồm: Ảnh gốc, Tên tranh và Ảnh mẫu tùy chọn. " +
            "Công cụ sẽ tự chuẩn hóa ảnh nét đơn sắc (không ghi đè ảnh gốc), lấp khe nhỏ, " +
            "phát hiện cảnh báo rò/gộp vùng và cho phép vẽ thêm nét ngăn thủ công trực tiếp trên preview trước khi lưu.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            workshop = (ColoringPageMinigame)EditorGUILayout.ObjectField("Workshop", workshop, typeof(ColoringPageMinigame), true);
            if (GUILayout.Button("Tìm", GUILayout.Width(50f))) FindWorkshop();
        }

        artworkName = EditorGUILayout.TextField("Tên tranh", artworkName);
        originalImage = (Texture2D)EditorGUILayout.ObjectField("Ảnh tranh gốc", originalImage, typeof(Texture2D), false);
        referenceArt = (Texture2D)EditorGUILayout.ObjectField("Ảnh mẫu hoàn thiện (tùy chọn)", referenceArt, typeof(Texture2D), false);

        if (originalImage != cachedOriginalImage)
        {
            cachedOriginalImage = originalImage;
            manualStrokes = null;
            ReanalyzeArtwork();
        }

        if (originalImage != null)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Tham số nhận diện nét & phân vùng", EditorStyles.boldLabel);
            int nextThreshold = EditorGUILayout.IntSlider("Ngưỡng nét viền đen", lineThreshold, 50, 240);
            int nextGapRadius = EditorGUILayout.IntSlider("Lấp khe hở nhỏ (pixels)", gapClosureRadius, 0, 4);
            int nextMinPixels = EditorGUILayout.IntSlider("Vùng tối thiểu (pixels)", minimumRegionPixels, 10, 500);
            bool nextSmooth = EditorGUILayout.Toggle("Làm mịn biên nét", smoothLines);

            if (nextThreshold != lineThreshold || nextGapRadius != gapClosureRadius
                || nextMinPixels != minimumRegionPixels || nextSmooth != smoothLines)
            {
                lineThreshold = nextThreshold;
                gapClosureRadius = nextGapRadius;
                minimumRegionPixels = nextMinPixels;
                smoothLines = nextSmooth;
                ReanalyzeArtwork();
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                drawSeparatorMode = GUILayout.Toggle(drawSeparatorMode, "✎ Vẽ nét ngăn thủ công trên Preview", "Button", GUILayout.Height(28f));
                if (GUILayout.Button("Phân tích lại vùng", GUILayout.Width(130f), GUILayout.Height(28f)))
                {
                    ReanalyzeArtwork();
                }
                if (GUILayout.Button("Xóa nét vẽ tay", GUILayout.Width(100f), GUILayout.Height(28f)))
                {
                    manualStrokes = null;
                    ReanalyzeArtwork();
                }
            }

            if (drawSeparatorMode)
            {
                brushRadius = EditorGUILayout.IntSlider("Độ dày nét ngăn (bán kính)", brushRadius, 1, 8);
                EditorGUILayout.HelpBox("Đang bật chế độ vẽ: Kéo chuột trên khung Preview bên dưới để vẽ nét ngăn màu đen vào các khe hở.", MessageType.Warning);
            }

            // Hiển thị cảnh báo rò/gộp vùng
            if (leakOrMergeWarnings.Count > 0)
            {
                string warnText = string.Join("\n• ", leakOrMergeWarnings);
                EditorGUILayout.HelpBox("CẢNH BÁO RÒ HOẶC GỘP VÙNG:\n• " + warnText + "\n=> Hãy tăng mức lấp khe hoặc bật 'Vẽ nét ngăn thủ công' để đóng các khe hở.", MessageType.Warning);
            }
            else if (fillableRegionCount > 0)
            {
                EditorGUILayout.HelpBox($"Phân vùng hoàn tất: Nhận diện {fillableRegionCount} vùng tô kín hợp lệ ({maskPixelCount:N0} pixels). Các vùng được tô màu pastel trực quan trên preview.", MessageType.Info);
            }

            DrawInteractivePreview();

            if (referenceArt != null)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Căn ảnh mẫu với tranh nét (tùy chọn)", EditorStyles.boldLabel);
                referenceScale = EditorGUILayout.Slider("Phóng to / thu nhỏ", referenceScale, 0.1f, 3f);
                referenceOffset.x = EditorGUILayout.Slider("Dịch ngang", referenceOffset.x, -1f, 1f);
                referenceOffset.y = EditorGUILayout.Slider("Dịch dọc", referenceOffset.y, -1f, 1f);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Chọn một ảnh gốc (Texture2D) để tự động sinh ảnh nét, dữ liệu vùng tô và xem trước.", MessageType.Info);
        }

        if (workshop == null)
            EditorGUILayout.HelpBox("Không tìm thấy ColoringPageMinigame trong scene. Hãy bấm 'Tìm' hoặc chạy Tools > Setup Workshop Tô Màu.", MessageType.Error);
        else
            EditorGUILayout.LabelField($"Workshop hiện có {(workshop.paintings == null ? 0 : workshop.paintings.Length)} mục tranh.");

        EditorGUILayout.Space(10f);
        bool canAdd = !EditorApplication.isPlaying && workshop != null
            && !string.IsNullOrWhiteSpace(artworkName) && originalImage != null && fillableRegionCount > 0;

        using (new EditorGUI.DisabledScope(!canAdd))
        {
            if (GUILayout.Button("Thêm tranh vào workshop", GUILayout.Height(36f)))
            {
                AddArtworkToWorkshop();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawExistingAlignmentEditor()
    {
        EditorGUILayout.LabelField("Căn ảnh mẫu cho một cặp tranh đã thêm", EditorStyles.boldLabel);
        if (workshop == null) FindWorkshop();
        if (workshop == null || workshop.paintings == null || workshop.paintings.Length == 0)
        {
            EditorGUILayout.HelpBox("Chưa có tranh. Hãy thêm một cặp ở tab Thêm tranh trước.", MessageType.Info);
            return;
        }

        string[] choices = workshop.paintings.Select((item, i) => item == null
            ? $"({i + 1}) Mục trống"
            : $"{i + 1}. {(string.IsNullOrWhiteSpace(item.title) ? (item.lineArt != null ? item.lineArt.name : "Chưa có ảnh nét") : item.title)}").ToArray();
        int selected = EditorGUILayout.Popup("Cặp tranh", Mathf.Clamp(alignmentIndex, 0, choices.Length - 1), choices);
        if (selected != alignmentIndex || alignmentWorkshop != workshop)
            LoadAlignmentTarget(selected);
        if (alignmentTarget == null || alignmentTarget.lineArt == null)
        {
            EditorGUILayout.HelpBox("Mục này chưa có ảnh nét.", MessageType.Warning);
            return;
        }
        if (alignmentTarget.referenceArt == null)
        {
            EditorGUILayout.HelpBox("Cặp này chưa có ảnh mẫu hoàn thiện để căn.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Căn chỉnh kích thước và vị trí ảnh mẫu so với khung hiển thị:");
        float nextScale = EditorGUILayout.Slider("Phóng to / thu nhỏ", referenceScale, 0.1f, 3f);
        Vector2 nextOffset = referenceOffset;
        nextOffset.x = EditorGUILayout.Slider("Dịch ngang", nextOffset.x, -1f, 1f);
        nextOffset.y = EditorGUILayout.Slider("Dịch dọc", nextOffset.y, -1f, 1f);
        if (!Mathf.Approximately(nextScale, referenceScale) || nextOffset != referenceOffset)
        {
            referenceScale = nextScale;
            referenceOffset = nextOffset;
            alignmentDirty = true;
        }

        // Preview căn chỉnh
        float previewWidth = Mathf.Max(240f, position.width - 36f);
        float previewHeight = previewWidth / 1.25f;
        Rect area = GUILayoutUtility.GetRect(240f, previewHeight, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(area, new Color(0.08f, 0.08f, 0.08f, 1f));
        Rect lineRect = ContainRect(alignmentTarget.lineArt.width, alignmentTarget.lineArt.height, area);
        GUI.color = Color.white;
        GUI.DrawTexture(lineRect, alignmentTarget.lineArt, ScaleMode.StretchToFill, true);

        if (alignmentTarget.referenceArt != null)
        {
            Rect refRect = ContainRect(alignmentTarget.referenceArt.width, alignmentTarget.referenceArt.height, area);
            Rect aligned = ColoringArtworkLayout.ReferenceRect(
                new Vector2(alignmentTarget.lineArt.width, alignmentTarget.lineArt.height),
                new Vector2(alignmentTarget.referenceArt.width, alignmentTarget.referenceArt.height),
                area.size, referenceScale, referenceOffset);
            refRect = new Rect(area.center + new Vector2(aligned.xMin, -aligned.yMax), aligned.size);
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            GUI.BeginClip(area);
            refRect.position -= area.position;
            GUI.DrawTexture(refRect, alignmentTarget.referenceArt, ScaleMode.StretchToFill, true);
            GUI.EndClip();
            GUI.color = Color.white;
        }

        using (new EditorGUI.DisabledScope(!alignmentDirty))
        {
            if (GUILayout.Button("Lưu căn ảnh mẫu", GUILayout.Height(30f)))
            {
                Undo.RecordObject(workshop, "Align coloring reference image");
                alignmentTarget.referenceScale = referenceScale;
                alignmentTarget.referenceOffset = referenceOffset;
                EditorUtility.SetDirty(workshop);
                EditorSceneManager.MarkSceneDirty(workshop.gameObject.scene);
                alignmentDirty = false;
            }
        }
    }

    private void LoadAlignmentTarget(int index)
    {
        if (workshop == null || workshop.paintings == null || index < 0 || index >= workshop.paintings.Length) return;
        alignmentWorkshop = workshop;
        alignmentIndex = index;
        alignmentTarget = workshop.paintings[index];
        referenceScale = alignmentTarget != null ? alignmentTarget.referenceScale : 1f;
        referenceOffset = alignmentTarget != null ? alignmentTarget.referenceOffset : Vector2.zero;
        alignmentDirty = false;
    }

    private void FindWorkshop()
    {
        ColoringPageMinigame[] candidates = FindObjectsByType<ColoringPageMinigame>(FindObjectsInactive.Include);
        foreach (ColoringPageMinigame candidate in candidates)
        {
            if (candidate != null && candidate.gameObject.scene.IsValid() && candidate.gameObject.scene.isLoaded)
            {
                workshop = candidate;
                return;
            }
        }
        workshop = null;
    }

    private void ReanalyzeArtwork()
    {
        ReleasePreviewTextures();
        calculatedRegions.Clear();
        leakOrMergeWarnings.Clear();
        warnedRegionIds.Clear();
        fillableRegionCount = 0;
        maskPixelCount = 0;

        if (originalImage == null) return;
        if (!MakeTextureReadable(originalImage, out Texture2D readableSource)) return;
        originalImage = readableSource;

        int width = originalImage.width;
        int height = originalImage.height;
        Color32[] srcPixels = originalImage.GetPixels32();

        if (manualStrokes == null || manualStrokes.GetLength(0) != width || manualStrokes.GetLength(1) != height)
        {
            manualStrokes = new bool[width, height];
        }

        // 1. Tạo bản đồ nét nhị phân và ảnh nét đơn sắc
        bool[] isLine = new bool[srcPixels.Length];
        Color32[] lineArtPixels = new Color32[srcPixels.Length];
        for (int i = 0; i < srcPixels.Length; i++)
        {
            int x = i % width;
            int y = i / width;
            int lum = Luminance(srcPixels[i]);
            bool manual = manualStrokes[x, y];
            bool line = manual || (lum <= lineThreshold);
            isLine[i] = line;

            if (line)
            {
                lineArtPixels[i] = new Color32(0, 0, 0, 255);
            }
            else
            {
                if (smoothLines && lum <= lineThreshold + 24)
                {
                    byte gray = (byte)Mathf.Clamp((lum - lineThreshold) * 10, 0, 255);
                    lineArtPixels[i] = new Color32(gray, gray, gray, 255);
                }
                else
                {
                    lineArtPixels[i] = new Color32(255, 255, 255, 255);
                }
            }
        }

        // 2. Lấp khe hở nhỏ (Morphological Closing)
        bool[] effectiveLines = isLine;
        if (gapClosureRadius > 0)
        {
            effectiveLines = MorphologicalClosing(isLine, width, height, gapClosureRadius);
        }

        // 3. Phân tích Connected Components (4-connected flood fill)
        int[] labels = new int[srcPixels.Length];
        int[] queue = new int[srcPixels.Length];
        List<int[]> components = new List<int[]>();
        HashSet<int> borderTouchingComponentIndices = new HashSet<int>();

        for (int start = 0; start < srcPixels.Length; start++)
        {
            if (labels[start] != 0 || effectiveLines[start]) continue;
            int compId = components.Count + 1;
            int head = 0, tail = 0;
            queue[tail++] = start;
            labels[start] = compId;
            bool touchesBorder = false;

            while (head < tail)
            {
                int index = queue[head++];
                int x = index % width;
                int y = index / width;

                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                    touchesBorder = true;

                AddNeighbor(index - 1, x > 0);
                AddNeighbor(index + 1, x + 1 < width);
                AddNeighbor(index - width, y > 0);
                AddNeighbor(index + width, y + 1 < height);
            }

            int[] compPixels = new int[tail];
            Array.Copy(queue, compPixels, tail);
            components.Add(compPixels);

            if (touchesBorder)
                borderTouchingComponentIndices.Add(compId - 1);

            void AddNeighbor(int neighbor, bool inBounds)
            {
                if (!inBounds || labels[neighbor] != 0 || effectiveLines[neighbor]) return;
                labels[neighbor] = compId;
                queue[tail++] = neighbor;
            }
        }

        // 4. Xác định vùng nền (Background) và các vùng tô hợp lệ
        // Nền ngoài là các component chạm mép tranh (hoặc component lớn nhất chạm mép)
        int backgroundComponentIndex = -1;
        int largestBorderSize = 0;
        foreach (int borderComp in borderTouchingComponentIndices)
        {
            if (components[borderComp].Length > largestBorderSize)
            {
                largestBorderSize = components[borderComp].Length;
                backgroundComponentIndex = borderComp;
            }
        }

        List<int[]> validComponents = new List<int[]>();
        int totalFillablePixels = 0;
        for (int i = 0; i < components.Count; i++)
        {
            if (i == backgroundComponentIndex) continue;
            // Nếu một component nhỏ chạm mép tranh nhưng không phải nền lớn nhất, cảnh báo rò
            if (borderTouchingComponentIndices.Contains(i))
            {
                leakOrMergeWarnings.Add($"Vùng chạm mép tranh ({components[i].Length:N0} px): có thể bị hở nét rò ra ngoài biên.");
            }
            if (components[i].Length >= minimumRegionPixels)
            {
                validComponents.Add(components[i]);
                totalFillablePixels += components[i].Length;
            }
        }

        // 5. Kiểm tra cảnh báo gộp vùng (Vùng có diện tích bất thường > 25% tổng vùng tô)
        for (int i = 0; i < validComponents.Count; i++)
        {
            int regionId = i + 1;
            int count = validComponents[i].Length;
            float ratio = totalFillablePixels > 0 ? (float)count / totalFillablePixels : 0f;
            if (ratio > 0.28f && count > 1500)
            {
                warnedRegionIds.Add(regionId);
                leakOrMergeWarnings.Add($"Vùng #{regionId} chiếm {ratio * 100f:F1}% tổng diện tích ({count:N0} px) — nghi ngờ bị gộp nhiều vùng do thiếu nét ngăn.");
            }
        }

        fillableRegionCount = validComponents.Count;
        maskPixelCount = totalFillablePixels;

        // 6. Đóng gói dữ liệu dải quét ngang (Run-Length Spans) cho từng vùng
        calculatedRegions.Clear();
        Color32[] maskPixels = new Color32[srcPixels.Length];
        Color32[] overlayPixels = new Color32[srcPixels.Length];

        for (int i = 0; i < maskPixels.Length; i++)
        {
            maskPixels[i] = new Color32(0, 0, 0, 255);
            overlayPixels[i] = new Color32(0, 0, 0, 0);
        }

        for (int i = 0; i < validComponents.Count; i++)
        {
            int regionId = i + 1;
            int[] compPixels = validComponents[i];
            Array.Sort(compPixels);

            List<ColoringPixelSpan> spans = new List<ColoringPixelSpan>();
            int spanStart = compPixels[0];
            int spanLen = 1;

            for (int p = 1; p < compPixels.Length; p++)
            {
                if (compPixels[p] == compPixels[p - 1] + 1)
                {
                    spanLen++;
                }
                else
                {
                    spans.Add(new ColoringPixelSpan { start = spanStart, length = spanLen });
                    spanStart = compPixels[p];
                    spanLen = 1;
                }
            }
            spans.Add(new ColoringPixelSpan { start = spanStart, length = spanLen });

            calculatedRegions.Add(new ColoringRegionSpanItem
            {
                id = regionId,
                pixelCount = compPixels.Length,
                spans = spans.ToArray()
            });

            // Màu trực quan cho overlay
            bool isWarned = warnedRegionIds.Contains(regionId);
            float hue = (regionId * 0.618033988749895f) % 1f;
            Color baseColor = isWarned ? new Color(1f, 0.40f, 0.12f) : Color.HSVToRGB(hue, 0.65f, 0.95f);
            Color32 overlayColor = new Color32((byte)(baseColor.r * 255), (byte)(baseColor.g * 255), (byte)(baseColor.b * 255), (byte)(isWarned ? 190 : 135));

            foreach (int pix in compPixels)
            {
                maskPixels[pix] = new Color32(255, 255, 255, 255);
                overlayPixels[pix] = overlayColor;
            }
        }

        // Vẽ các nét ngăn thủ công đè lên overlay
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (manualStrokes[x, y])
                {
                    int idx = y * width + x;
                    overlayPixels[idx] = new Color32(220, 20, 40, 255);
                }
            }
        }

        // Tạo Texture2D cho Preview
        previewLineArt = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = originalImage.name + "_LineArt_Preview",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        previewLineArt.SetPixels32(lineArtPixels);
        previewLineArt.Apply(false, false);

        previewRegionOverlay = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = originalImage.name + "_Overlay_Preview",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        previewRegionOverlay.SetPixels32(overlayPixels);
        previewRegionOverlay.Apply(false, false);

        previewMask = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = originalImage.name + "_Mask_Preview",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        previewMask.SetPixels32(maskPixels);
        previewMask.Apply(false, false);
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

                // 1. Khoảng cách Dọc (Vertical)
                int dUp = GetLineDistance(input, width, height, x, y, 0, -1, maxGap + 1);
                int dDown = GetLineDistance(input, width, height, x, y, 0, 1, maxGap + 1);
                if (dUp > 0 && dDown > 0 && dUp + dDown <= maxGap + 1)
                {
                    result[y * width + x] = true;
                    continue;
                }

                // 2. Khoảng cách Ngang (Horizontal)
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

    private void DrawInteractivePreview()
    {
        if (originalImage == null || previewLineArt == null) return;

        float previewWidth = Mathf.Max(260f, position.width - 36f);
        float previewHeight = previewWidth / 1.35f;
        Rect area = GUILayoutUtility.GetRect(260f, previewHeight, GUILayout.ExpandWidth(true));
        area.height = previewHeight;
        EditorGUI.DrawRect(area, new Color(0.10f, 0.10f, 0.11f, 1f));
        previewRect = ContainRect(originalImage.width, originalImage.height, area);

        // 1. Vẽ tranh nét
        GUI.color = Color.white;
        GUI.DrawTexture(previewRect, previewLineArt, ScaleMode.StretchToFill, true);

        // 2. Vẽ ảnh mẫu bán trong suốt (nếu có)
        if (referenceArt != null)
        {
            Rect sampleRect = ContainRect(referenceArt.width, referenceArt.height, area);
            Rect aligned = ColoringArtworkLayout.ReferenceRect(
                new Vector2(originalImage.width, originalImage.height),
                new Vector2(referenceArt.width, referenceArt.height), area.size, referenceScale, referenceOffset);
            sampleRect = new Rect(area.center + new Vector2(aligned.xMin, -aligned.yMax), aligned.size);
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            GUI.BeginClip(area);
            sampleRect.position -= area.position;
            GUI.DrawTexture(sampleRect, referenceArt, ScaleMode.StretchToFill, true);
            GUI.EndClip();
        }

        // 3. Vẽ lớp vùng tô trực quan
        if (previewRegionOverlay != null)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(previewRect, previewRegionOverlay, ScaleMode.StretchToFill, true);
        }
        GUI.color = Color.white;

        // Xử lý sự kiện chuột (vẽ nét ngăn hoặc probe)
        Event e = Event.current;
        Vector2 mousePos = e.mousePosition;
        if (previewRect.Contains(mousePos))
        {
            float normX = (mousePos.x - previewRect.xMin) / previewRect.width;
            float normY = (previewRect.yMax - mousePos.y) / previewRect.height;
            int px = Mathf.Clamp(Mathf.FloorToInt(normX * originalImage.width), 0, originalImage.width - 1);
            int py = Mathf.Clamp(Mathf.FloorToInt(normY * originalImage.height), 0, originalImage.height - 1);

            if (drawSeparatorMode)
            {
                EditorGUIUtility.AddCursorRect(previewRect, MouseCursor.Orbit);
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    DrawBrushCircle(px, py, brushRadius);
                    lastDrawnPixel = new Vector2Int(px, py);
                    e.Use();
                    ReanalyzeArtwork();
                    Repaint();
                }
                else if (e.type == EventType.MouseDrag && e.button == 0)
                {
                    if (lastDrawnPixel.x >= 0)
                    {
                        DrawBrushLine(lastDrawnPixel.x, lastDrawnPixel.y, px, py, brushRadius);
                    }
                    else
                    {
                        DrawBrushCircle(px, py, brushRadius);
                    }
                    lastDrawnPixel = new Vector2Int(px, py);
                    e.Use();
                    ReanalyzeArtwork();
                    Repaint();
                }
                else if (e.type == EventType.MouseUp && e.button == 0)
                {
                    lastDrawnPixel = new Vector2Int(-1, -1);
                    e.Use();
                    ReanalyzeArtwork();
                    Repaint();
                }
            }
            else if (e.type == EventType.MouseDown && e.button == 0)
            {
                int flatIdx = py * originalImage.width + px;
                int foundRegion = -1;
                for (int r = 0; r < calculatedRegions.Count; r++)
                {
                    var item = calculatedRegions[r];
                    foreach (var s in item.spans)
                    {
                        if (flatIdx >= s.start && flatIdx < s.start + s.length)
                        {
                            foundRegion = item.id;
                            break;
                        }
                    }
                    if (foundRegion > 0) break;
                }
                previewProbe = foundRegion > 0
                    ? $"Điểm ({px}, {py}) thuộc Vùng #{foundRegion} ({calculatedRegions[foundRegion - 1].pixelCount:N0} px)."
                    : $"Điểm ({px}, {py}): Nét viền hoặc nền ngoài (không thuộc vùng tô).";
                e.Use();
                Repaint();
            }
        }

        GUILayout.Label(previewProbe, EditorStyles.miniLabel);
    }

    private void DrawBrushCircle(int cx, int cy, int radius)
    {
        if (manualStrokes == null || originalImage == null) return;
        int w = originalImage.width;
        int h = originalImage.height;
        int rSq = radius * radius;
        for (int dy = -radius; dy <= radius; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= h) continue;
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = cx + dx;
                if (x < 0 || x >= w) continue;
                if (dx * dx + dy * dy <= rSq)
                {
                    manualStrokes[x, y] = true;
                }
            }
        }
    }

    private void DrawBrushLine(int x0, int y0, int x1, int y1, int radius)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            DrawBrushCircle(x0, y0, radius);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    private void AddArtworkToWorkshop()
    {
        if (workshop == null || originalImage == null || string.IsNullOrWhiteSpace(artworkName) || calculatedRegions.Count == 0)
            return;

        string sourcePath = AssetDatabase.GetAssetPath(originalImage);
        string folder = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? "Assets/TRAnh";
        string sanitizedName = SanitizeFileName(artworkName.Trim());

        // 1. Lưu file Line Art PNG
        string lineArtPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{sanitizedName}_LineArt.png");
        File.WriteAllBytes(lineArtPath, previewLineArt.EncodeToPNG());
        AssetDatabase.ImportAsset(lineArtPath, ImportAssetOptions.ForceSynchronousImport);
        ConfigureTextureImporter(lineArtPath, isReadable: true, uncompressed: true, pointFilter: false);
        Texture2D savedLineArt = AssetDatabase.LoadAssetAtPath<Texture2D>(lineArtPath);

        // 2. Lưu file Paint Mask PNG
        string maskPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{sanitizedName}_PaintMask.png");
        File.WriteAllBytes(maskPath, previewMask.EncodeToPNG());
        AssetDatabase.ImportAsset(maskPath, ImportAssetOptions.ForceSynchronousImport);
        ConfigureTextureImporter(maskPath, isReadable: true, uncompressed: true, pointFilter: true);
        Texture2D savedPaintMask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);

        // 3. Lưu file Region Data JSON
        string jsonPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{sanitizedName}_RegionData.json");
        ColoringRegionDataAsset dataAsset = new ColoringRegionDataAsset
        {
            width = originalImage.width,
            height = originalImage.height,
            regionCount = calculatedRegions.Count,
            regions = calculatedRegions.ToArray()
        };
        string jsonContent = JsonUtility.ToJson(dataAsset, false);
        File.WriteAllText(jsonPath, jsonContent);
        AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceSynchronousImport);
        TextAsset savedRegionData = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);

        if (savedLineArt == null || savedPaintMask == null || savedRegionData == null)
        {
            EditorUtility.DisplayDialog("Lỗi lưu tài nguyên", "Không thể nạp lại tài nguyên vừa tạo. Vui lòng thử lại.", "OK");
            return;
        }

        // 4. Thêm mục vào ColoringPageMinigame
        Undo.RecordObject(workshop, "Add Coloring Artwork");
        List<ColoringArtworkDefinition> list = workshop.paintings == null
            ? new List<ColoringArtworkDefinition>()
            : new List<ColoringArtworkDefinition>(workshop.paintings);

        list.Add(new ColoringArtworkDefinition
        {
            title = artworkName.Trim(),
            lineArt = savedLineArt,
            referenceArt = referenceArt,
            paintMask = savedPaintMask,
            regionData = savedRegionData,
            referenceScale = referenceScale,
            referenceOffset = referenceOffset,
            minimumRegionPixels = minimumRegionPixels,
            whiteThreshold = lineThreshold,
            regions = Array.Empty<ColoringRegionSeed>()
        });

        workshop.paintings = list.ToArray();
        EditorUtility.SetDirty(workshop);
        EditorSceneManager.MarkSceneDirty(workshop.gameObject.scene);

        EditorUtility.DisplayDialog("Thành công!",
            $"Đã thêm tranh '{artworkName}' thành công!\n" +
            $"- Ảnh nét: {lineArtPath}\n" +
            $"- Mask: {maskPath}\n" +
            $"- Vùng tô: {calculatedRegions.Count} vùng (TextAsset JSON: {jsonPath})\n" +
            $"- Ảnh gốc không bị ghi đè.", "Tuyệt vời!");

        artworkName = string.Empty;
        originalImage = null;
        cachedOriginalImage = null;
        referenceArt = null;
        manualStrokes = null;
        ReleasePreviewTextures();
        Repaint();
    }

    private static string SanitizeFileName(string name)
    {
        string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        string clean = Regex.Replace(name, "[" + Regex.Escape(invalid) + "]", "_");
        return clean.Replace(' ', '_');
    }

    private static void ConfigureTextureImporter(string path, bool isReadable, bool uncompressed, bool pointFilter)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.isReadable = isReadable;
            if (uncompressed) importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.filterMode = pointFilter ? FilterMode.Point : FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }

    private static Rect ContainRect(float imageWidth, float imageHeight, Rect bounds)
    {
        Vector2 size = ColoringArtworkLayout.Fit(new Vector2(imageWidth, imageHeight), bounds.size);
        return new Rect(bounds.center - size * 0.5f, size);
    }

    private static int Luminance(Color32 color) => (299 * color.r + 587 * color.g + 114 * color.b) / 1000;

    private static bool MakeTextureReadable(Texture2D texture, out Texture2D readableTexture)
    {
        readableTexture = texture;
        string path = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null || importer.isReadable) return true;

        importer.isReadable = true;
        importer.SaveAndReimport();
        readableTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        return readableTexture != null;
    }
}
