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
    private int sourceMode;
    private float colorMergeDistance = 24f;
    private int colorLineThickness = 1;
    private ColorArtworkSegmentation colorAnalysis;
    private readonly HashSet<int> excludedColorRegions = new HashSet<int>();
    private int colorEditMode, pendingMergeRegion, lostColorRegions;
    private string colorAnalysisError;
    private HashSet<int> unpaintableColorRegions = new HashSet<int>();
    private Color32[] colorReferencePixels;

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
        EditorGUILayout.HelpBox(sourceMode == 1
            ? "Tách các mảng màu liên thông, sinh nét từ ranh giới; chỉnh vùng trên preview trước khi thêm vào workshop."
            : "Cấu hình một lần gồm: Ảnh gốc, Tên tranh và Ảnh mẫu tùy chọn. " +
            "Công cụ sẽ tự chuẩn hóa ảnh nét đơn sắc (không ghi đè ảnh gốc), lấp khe nhỏ, " +
            "phát hiện cảnh báo rò/gộp vùng và cho phép vẽ thêm nét ngăn thủ công trực tiếp trên preview trước khi lưu.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            workshop = (ColoringPageMinigame)EditorGUILayout.ObjectField("Workshop", workshop, typeof(ColoringPageMinigame), true);
            if (GUILayout.Button("Tìm", GUILayout.Width(50f))) FindWorkshop();
        }

        artworkName = EditorGUILayout.TextField("Tên tranh", artworkName);
        int nextSourceMode = GUILayout.Toolbar(sourceMode, new[] { "Từ ảnh nét", "Tạo tranh tô từ ảnh màu" });
        if (nextSourceMode != sourceMode)
        {
            sourceMode = nextSourceMode;
            manualStrokes = null;
            ReanalyzeArtwork();
        }
        originalImage = (Texture2D)EditorGUILayout.ObjectField("Ảnh tranh gốc", originalImage, typeof(Texture2D), false);
        if (sourceMode == 0)
            referenceArt = (Texture2D)EditorGUILayout.ObjectField("Ảnh mẫu hoàn thiện (tùy chọn)", referenceArt, typeof(Texture2D), false);
        else
            EditorGUILayout.HelpBox("Giữ ảnh gốc; lưu bản mẫu riêng cùng kích thước với ảnh nét để không lệch khi đổi platform. Vùng xám đã loại không nhận tô; nền transparent tự loại. Ảnh có texture có thể cần gộp vùng thủ công.", MessageType.Info);

        if (originalImage != cachedOriginalImage)
        {
            cachedOriginalImage = originalImage;
            manualStrokes = null;
            ReanalyzeArtwork();
        }

        if (originalImage != null)
        {
            if (sourceMode == 1)
            {
                DrawColorAuthoring();
            }
            else
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
            && !string.IsNullOrWhiteSpace(artworkName) && originalImage != null && fillableRegionCount > 0
            && (sourceMode == 0 || colorAnalysis != null && lostColorRegions == 0 && colorAnalysisError == null);

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
            Rect refRect = ColoringArtworkLayout.ReferencePreviewRect(
                new Vector2(alignmentTarget.lineArt.width, alignmentTarget.lineArt.height),
                new Vector2(alignmentTarget.referenceArt.width, alignmentTarget.referenceArt.height),
                area, referenceScale, referenceOffset);
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
        if (sourceMode == 1)
        {
            AnalyzeColorArtwork();
            return;
        }
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
            Rect sampleRect = ColoringArtworkLayout.ReferencePreviewRect(
                new Vector2(originalImage.width, originalImage.height),
                new Vector2(referenceArt.width, referenceArt.height), area, referenceScale, referenceOffset);
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
        if (!TryAddArtworkToWorkshop(out ColoringArtworkDefinition added, out string error))
        {
            EditorUtility.DisplayDialog("Lỗi lưu tài nguyên", error, "OK");
            return;
        }
        EditorUtility.DisplayDialog("Thành công!",
            $"Đã thêm tranh '{added.title}' thành công!\n" +
            $"- Ảnh nét: {AssetDatabase.GetAssetPath(added.lineArt)}\n" +
            $"- Mask: {AssetDatabase.GetAssetPath(added.paintMask)}\n" +
            $"- Vùng tô: {JsonUtility.FromJson<ColoringRegionDataAsset>(added.regionData.text).regionCount} vùng " +
            $"(TextAsset JSON: {AssetDatabase.GetAssetPath(added.regionData)})\n" +
            $"- Ảnh gốc không bị ghi đè.", "Tuyệt vời!");
    }

    // The button and automated integration checks share this complete save path.
    internal bool TryAddArtworkToWorkshop(out ColoringArtworkDefinition added, out string error)
    {
        added = null;
        error = null;
        var createdPaths = new List<string>();
        try
        {
            if (sourceMode == 1 && (colorAnalysis == null || colorAnalysisError != null || lostColorRegions > 0))
                throw new InvalidOperationException("Phân tích lại ảnh màu; giảm độ dày nét hoặc gộp/loại vùng bị mất trước khi lưu.");
            if (workshop == null || originalImage == null || string.IsNullOrWhiteSpace(artworkName)
                || calculatedRegions.Count == 0 || previewLineArt == null || previewMask == null)
                throw new InvalidOperationException("Hãy chọn workshop, đặt tên tranh và phân tích các vùng trước khi lưu.");
            if (originalImage.width != previewLineArt.width || originalImage.height != previewLineArt.height
                || previewMask.width != previewLineArt.width || previewMask.height != previewLineArt.height)
                throw new InvalidOperationException("Kích thước ảnh đã thay đổi. Hãy phân tích lại tranh trước khi lưu.");
            string sourcePath = AssetDatabase.GetAssetPath(originalImage);
            if (string.IsNullOrEmpty(sourcePath))
                throw new InvalidOperationException("Ảnh gốc phải là tài nguyên đã lưu trong dự án.");
            string folder = Path.GetDirectoryName(sourcePath).Replace('\\', '/');
            string name = SanitizeFileName(artworkName.Trim());
            string linePath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}_LineArt.png");
            createdPaths.Add(linePath);
            File.WriteAllBytes(linePath, previewLineArt.EncodeToPNG());
            AssetDatabase.ImportAsset(linePath, ImportAssetOptions.ForceSynchronousImport);
            if (sourceMode == 1)
                ConfigureFinalArtworkImporter(linePath, previewLineArt.width, previewLineArt.height, false);
            else
                ConfigureTextureImporter(linePath, isReadable: true, uncompressed: true, pointFilter: false);
            Texture2D importedLine = AssetDatabase.LoadAssetAtPath<Texture2D>(linePath);
            if (importedLine == null) throw new InvalidOperationException("Không nạp được ảnh nét sau import.");

            // The first import may resize through NPOT/default/platform presets.
            // Bake every output in that actual pixel grid, using the same discrete UV sample.
            int width = importedLine.width, height = importedLine.height;
            ColoringRegionDataAsset data = BuildImportedRegionData(width, height, out Color32[] linePixels, out Color32[] maskPixels);
            WritePixels(linePath, width, height, linePixels);
            ConfigureFinalArtworkImporter(linePath, width, height, false);
            string maskPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}_PaintMask.png");
            createdPaths.Add(maskPath);
            WritePixels(maskPath, width, height, maskPixels);
            AssetDatabase.ImportAsset(maskPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureFinalArtworkImporter(maskPath, width, height, true);
            Texture2D savedLine = AssetDatabase.LoadAssetAtPath<Texture2D>(linePath);
            Texture2D savedMask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            ValidateImportedArtwork(data, savedLine, savedMask);

            string jsonPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}_RegionData.json");
            createdPaths.Add(jsonPath);
            File.WriteAllText(jsonPath, JsonUtility.ToJson(data));
            AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceSynchronousImport);
            TextAsset savedData = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            if (savedData == null) throw new InvalidOperationException("Không nạp được JSON vùng tô sau import.");
            ValidateImportedArtwork(JsonUtility.FromJson<ColoringRegionDataAsset>(savedData.text), savedLine, savedMask);

            Texture2D savedReference = referenceArt;
            if (sourceMode == 1)
            {
                if (colorReferencePixels == null || colorReferencePixels.Length != width * height)
                    throw new InvalidOperationException("Ảnh mẫu không còn khớp bản phân vùng; hãy phân tích lại.");
                string samplePath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}_Reference.png");
                createdPaths.Add(samplePath);
                WritePixels(samplePath, width, height, colorReferencePixels);
                AssetDatabase.ImportAsset(samplePath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureFinalArtworkImporter(samplePath, width, height, false);
                savedReference = AssetDatabase.LoadAssetAtPath<Texture2D>(samplePath);
                if (savedReference == null || savedReference.width != width || savedReference.height != height)
                    throw new InvalidOperationException("Ảnh mẫu sau import không khớp kích thước vùng tô.");
            }

            // Attach only after all reimports and validation; existing entries stay untouched.
            added = new ColoringArtworkDefinition
            {
                title = artworkName.Trim(), lineArt = savedLine, referenceArt = savedReference,
                paintMask = savedMask, regionData = savedData, referenceScale = referenceScale,
                referenceOffset = sourceMode == 1 ? Vector2.zero : referenceOffset, minimumRegionPixels = minimumRegionPixels,
                whiteThreshold = sourceMode == 1 ? 180 : lineThreshold, regions = Array.Empty<ColoringRegionSeed>()
            };
            if (sourceMode == 1) added.referenceScale = 1f;
            Undo.RecordObject(workshop, "Add Coloring Artwork");
            var list = workshop.paintings == null ? new List<ColoringArtworkDefinition>()
                : new List<ColoringArtworkDefinition>(workshop.paintings);
            list.Add(added);
            workshop.paintings = list.ToArray();
            EditorUtility.SetDirty(workshop);
            EditorSceneManager.MarkSceneDirty(workshop.gameObject.scene);
        }
        catch (Exception exception)
        {
            // These paths were generated uniquely for this attempt; never remove existing artwork.
            foreach (string path in createdPaths) AssetDatabase.DeleteAsset(path);
            added = null;
            error = "Không thêm tranh: " + exception.Message;
            return false;
        }
        artworkName = string.Empty;
        originalImage = null;
        cachedOriginalImage = null;
        referenceArt = null;
        manualStrokes = null;
        colorAnalysis = null;
        colorReferencePixels = null;
        excludedColorRegions.Clear();
        ReleasePreviewTextures();
        Repaint();
        return true;
    }

    private void DrawColorAuthoring()
    {
        EditorGUI.BeginChangeCheck();
        float distance = EditorGUILayout.Slider("Mức gộp màu (Lab)", colorMergeDistance, 2f, 40f);
        int min = EditorGUILayout.IntSlider("Gộp vùng nhỏ dưới (px)", minimumRegionPixels, 1, 2000);
        bool smooth = EditorGUILayout.Toggle("Giảm nhiễu màu (median 3×3)", smoothLines);
        if (EditorGUI.EndChangeCheck())
        {
            colorMergeDistance = distance; minimumRegionPixels = min; smoothLines = smooth;
            ReanalyzeArtwork();
        }
        int thickness = EditorGUILayout.IntSlider("Độ dày nét (px)", colorLineThickness, 1, 8);
        if (thickness != colorLineThickness)
        { colorLineThickness = thickness; RefreshColorPreview(); }
        EditorGUILayout.HelpBox("Đổi tham số phân vùng hoặc ảnh sẽ xóa các chỉnh sửa vùng. Đổi độ dày nét giữ các vùng đã gộp/loại. Bấm hai vùng kề nhau để gộp; vùng rời nhau không tự gộp dù cùng màu.", MessageType.Info);
        colorEditMode = GUILayout.Toolbar(colorEditMode, new[] { "Kiểm tra vùng", "Loại / khôi phục", "Gộp 2 vùng kề" });
        if (GUILayout.Button("Phân tích lại / bỏ chỉnh sửa vùng")) ReanalyzeArtwork();
        if (colorAnalysisError != null) EditorGUILayout.HelpBox(colorAnalysisError, MessageType.Error);
        if (colorAnalysis == null) return;
        EditorGUILayout.LabelField($"{colorAnalysis.RegionCount} mảng màu; {fillableRegionCount} vùng tô; {excludedColorRegions.Count} vùng đã loại; gộp {colorAnalysis.SmallRegionsMerged} vùng nhỏ.");
        if (lostColorRegions > 0)
        {
            EditorGUILayout.HelpBox($"{lostColorRegions} vùng không còn pixel bên trong nét. Giảm độ dày nét, gộp hoặc loại vùng này trước khi lưu.", MessageType.Error);
            if (GUILayout.Button($"Loại {lostColorRegions} vùng màu hồng không đủ chỗ tô")) ExcludeUnpaintableColorRegions();
        }
        DrawColorPreview("Ảnh mẫu gốc", originalImage, false);
        DrawColorPreview("Ảnh nét", previewLineArt, false);
        DrawColorPreview("Vùng tô (bấm để sửa)", previewRegionOverlay, true);
        EditorGUILayout.LabelField(previewProbe, EditorStyles.wordWrappedMiniLabel);
    }

    private void DrawColorPreview(string title, Texture2D texture, bool interactive)
    {
        if (texture == null) return;
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        float height = Mathf.Min(340f, Mathf.Max(160f, (position.width - 36f) * texture.height / texture.width));
        Rect area = GUILayoutUtility.GetRect(260f, height, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(area, new Color(0.15f, 0.15f, 0.15f));
        Rect image = ContainRect(texture.width, texture.height, area);
        GUI.DrawTexture(image, texture, ScaleMode.StretchToFill, true);
        Event e = Event.current;
        if (!interactive || e.type != EventType.MouseDown || e.button != 0 || !image.Contains(e.mousePosition)) return;
        int x = Mathf.Clamp((int)((e.mousePosition.x - image.xMin) / image.width * texture.width), 0, texture.width - 1);
        int y = Mathf.Clamp((int)((image.yMax - e.mousePosition.y) / image.height * texture.height), 0, texture.height - 1);
        EditColorRegionAt(x, y, colorEditMode);
        e.Use(); Repaint();
    }

    // The preview and integration suite call the same edit path.
    internal void EditColorRegionAt(int x, int y, int mode)
    {
        if (colorAnalysis == null || x < 0 || y < 0 || x >= colorAnalysis.Width || y >= colorAnalysis.Height) return;
        int id = colorAnalysis.Labels[y * colorAnalysis.Width + x];
        if (id <= 0) { previewProbe = "Nền transparent: không thuộc vùng tô."; return; }
        if (mode != 2) pendingMergeRegion = 0;
        if (mode == 1)
        {
            if (!excludedColorRegions.Add(id)) excludedColorRegions.Remove(id);
            RefreshColorPreview();
        }
        else if (mode == 2)
        {
            if (excludedColorRegions.Contains(id)) { previewProbe = "Khôi phục vùng đã loại trước khi gộp."; return; }
            if (pendingMergeRegion == 0)
            { pendingMergeRegion = id; previewProbe = $"Đã chọn #{id}; bấm vùng kề để gộp."; return; }
            if (!colorAnalysis.MergeAdjacent(pendingMergeRegion, id))
            { pendingMergeRegion = 0; previewProbe = "Hai vùng phải khác nhau và kề nhau. Bấm chọn lại."; return; }
            pendingMergeRegion = 0; RefreshColorPreview();
        }
        previewProbe = $"Mảng #{id}: {(excludedColorRegions.Contains(id) ? "đã loại" : "được tô")}.";
    }

    internal void ExcludeUnpaintableColorRegions()
    {
        excludedColorRegions.UnionWith(unpaintableColorRegions);
        pendingMergeRegion = 0;
        RefreshColorPreview();
    }

    private void AnalyzeColorArtwork()
    {
        colorAnalysis = null; excludedColorRegions.Clear(); pendingMergeRegion = 0;
        colorReferencePixels = null;
        colorAnalysisError = null; lostColorRegions = 0;
        Texture2D readable = null;
        try
        {
            if ((long)originalImage.width * originalImage.height > 4194304)
                throw new InvalidOperationException("Ảnh trên 4 triệu pixel. Giảm Max Size trong importer rồi phân tích lại.");
            EditorUtility.DisplayProgressBar("Tạo tranh tô từ ảnh màu", "Đang gộp màu và tách các vùng liên thông…", 0.3f);
            // GPU readback leaves the original importer and file intact, including non-readable images.
            RenderTexture previous = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(originalImage.width, originalImage.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(originalImage, rt); RenderTexture.active = rt;
                readable = new Texture2D(originalImage.width, originalImage.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); readable.Apply();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); }
            colorReferencePixels = readable.GetPixels32();
            colorAnalysis = ColorArtworkSegmentation.Analyze(colorReferencePixels, originalImage.width, originalImage.height,
                colorMergeDistance, minimumRegionPixels, smoothLines);
            RefreshColorPreview();
        }
        catch (Exception e) { colorAnalysisError = "Không phân tích được ảnh màu: " + e.Message; }
        finally { EditorUtility.ClearProgressBar(); if (readable != null) DestroyImmediate(readable); }
    }

    private void RefreshColorPreview()
    {
        if (colorAnalysis == null) return;
        ReleasePreviewTextures();
        ColorArtworkSegmentation.Output result = colorAnalysis.BuildOutput(excludedColorRegions, colorLineThickness);
        calculatedRegions = result.Regions; fillableRegionCount = result.Regions.Count;
        maskPixelCount = result.PaintablePixels; lostColorRegions = result.LostRegions;
        unpaintableColorRegions = result.LostRegionIds;
        previewLineArt = ColorPreviewTexture(result.Lines, "ColorLinePreview", FilterMode.Bilinear);
        previewMask = ColorPreviewTexture(result.Mask, "ColorMaskPreview", FilterMode.Point);
        previewRegionOverlay = ColorPreviewTexture(result.Overlay, "ColorRegionsPreview", FilterMode.Point);
    }

    private Texture2D ColorPreviewTexture(Color32[] pixels, string name, FilterMode filter)
    {
        var texture = new Texture2D(colorAnalysis.Width, colorAnalysis.Height, TextureFormat.RGBA32, false)
        { name = name, filterMode = filter, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels); texture.Apply(); return texture;
    }

    private ColoringRegionDataAsset BuildImportedRegionData(int width, int height,
        out Color32[] linePixels, out Color32[] maskPixels)
    {
        int sourceWidth = previewLineArt.width, sourceHeight = previewLineArt.height;
        var sourceData = new ColoringRegionDataAsset
        {
            width = sourceWidth, height = sourceHeight, regionCount = calculatedRegions.Count,
            regions = calculatedRegions.ToArray()
        };
        int[] sourceLabels = ValidateRegionSpans(sourceData);
        Color32[] sourceLines = previewLineArt.GetPixels32();
        Color32[] sourceMask = previewMask.GetPixels32();
        for (int i = 0; i < sourceLabels.Length; i++)
            if ((sourceMask[i].r >= 128) != (sourceLabels[i] > 0))
                throw new InvalidOperationException("Mask preview không khớp nhãn vùng đã phân tích.");
        var labels = new int[checked(width * height)];
        linePixels = new Color32[labels.Length];
        maskPixels = new Color32[labels.Length];
        var spans = new List<ColoringPixelSpan>[sourceData.regionCount];
        var counts = new int[spans.Length];
        var paintable = new bool[spans.Length];
        for (int i = 0; i < spans.Length; i++) spans[i] = new List<ColoringPixelSpan>();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int sx = (int)(((long)x * 2 + 1) * sourceWidth / (2L * width));
            int sy = (int)(((long)y * 2 + 1) * sourceHeight / (2L * height));
            int source = sy * sourceWidth + sx, target = y * width + x;
            int id = labels[target] = sourceLabels[source];
            linePixels[target] = sourceLines[source];
            byte value = (byte)(id > 0 ? 255 : 0);
            maskPixels[target] = new Color32(value, value, value, 255);
            if (id > 0)
            {
                counts[id - 1]++;
                if (Luminance(linePixels[target]) > lineThreshold) paintable[id - 1] = true;
            }
        }
        for (int y = 0; y < height; y++)
        for (int i = y * width; i < (y + 1) * width;)
        {
            int start = i, id = labels[i++];
            while (i < (y + 1) * width && labels[i] == id) i++;
            if (id > 0) spans[id - 1].Add(new ColoringPixelSpan { start = start, length = i - start });
        }
        var regions = new ColoringRegionSpanItem[spans.Length];
        for (int i = 0; i < regions.Length; i++)
        {
            if (counts[i] == 0 || !paintable[i])
                throw new InvalidOperationException($"Resize đã làm mất vùng tô #{i + 1}. Hãy tăng kích thước import hoặc phân tích lại.");
            regions[i] = new ColoringRegionSpanItem { id = i + 1, pixelCount = counts[i], spans = spans[i].ToArray() };
        }
        return new ColoringRegionDataAsset { width = width, height = height, regionCount = regions.Length, regions = regions };
    }

    private static int[] ValidateRegionSpans(ColoringRegionDataAsset data)
    {
        if (data == null || data.width <= 0 || data.height <= 0 || data.regionCount <= 0
            || data.regions == null || data.regions.Length != data.regionCount)
            throw new InvalidOperationException("Dữ liệu vùng tô không hợp lệ.");
        var labels = new int[checked(data.width * data.height)];
        var seen = new bool[data.regionCount];
        foreach (var region in data.regions)
        {
            if (region.id < 1 || region.id > seen.Length || seen[region.id - 1]
                || region.spans == null || region.spans.Length == 0)
                throw new InvalidOperationException("Nhãn vùng tô không hợp lệ hoặc bị trùng.");
            seen[region.id - 1] = true;
            long total = 0;
            foreach (var span in region.spans)
            {
                if (span.start < 0 || span.length <= 0 || (long)span.start + span.length > labels.Length)
                    throw new InvalidOperationException("Span vùng tô vượt giới hạn ảnh.");
                for (int p = span.start; p < span.start + span.length; p++)
                {
                    if (labels[p] != 0) throw new InvalidOperationException("Span vùng tô bị chồng nhau.");
                    labels[p] = region.id;
                }
                total += span.length;
            }
            if (total != region.pixelCount) throw new InvalidOperationException("pixelCount không khớp span vùng tô.");
        }
        return labels;
    }

    private static void ValidateImportedArtwork(ColoringRegionDataAsset data, Texture2D line, Texture2D mask)
    {
        int[] labels = ValidateRegionSpans(data);
        if (line == null || mask == null || line.width != data.width || line.height != data.height
            || mask.width != data.width || mask.height != data.height)
            throw new InvalidOperationException("Texture sau import không khớp kích thước JSON vùng tô.");
        Color32[] pixels = mask.GetPixels32();
        for (int p = 0; p < labels.Length; p++)
            if ((pixels[p].r >= 128) != (labels[p] > 0))
                throw new InvalidOperationException("Mask sau import không khớp nhãn vùng tô.");
    }

    private static void WritePixels(string path, int width, int height, Color32[] pixels)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { DestroyImmediate(texture); }
    }

    private static void ConfigureFinalArtworkImporter(string path, int width, int height, bool pointFilter)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Không tìm thấy texture importer.");
        // Generated line/mask are now baked in the cache's pixel grid. Do not resize
        // again on the project's PC/Android targets or the common mobile/web targets.
        importer.textureType = TextureImporterType.Default;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = Mathf.Max(32, Mathf.NextPowerOfTwo(Mathf.Max(width, height)));
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        importer.mipmapEnabled = false;
        importer.filterMode = pointFilter ? FilterMode.Point : FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
            importer.ClearPlatformTextureSettings(platform);
        importer.SaveAndReimport();
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
