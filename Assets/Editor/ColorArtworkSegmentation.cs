using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using UnityEngine;

// Editor-only. Labels are spatial components, even when two components share a colour.
internal sealed class ColorArtworkSegmentation
{
    internal readonly int Width, Height;
    internal readonly int[] Labels;
    internal int SmallRegionsMerged { get; private set; }
    internal int RegionCount { get; private set; }

    internal sealed class Options
    {
        internal float MergeDistance = 24f, Denoise = 0.25f, KeepEdges = 0.85f;
        internal int MinimumPixels = 60;
        internal bool PreserveInk, SmoothBoundaries = true, JoinFaintGaps;
        internal double TimeLimitSeconds = 30;
        internal Func<string, float, bool> Cancel;
    }
    internal readonly bool[] Ink;
    internal readonly Color32[] Filtered;
    private readonly Options options;
    private bool[] manualBoundaries;
    private ColorArtworkSegmentation(int width, int height, int[] labels, bool[] ink, Color32[] filtered, Options settings)
    { Width = width; Height = height; Labels = labels; Ink = ink; Filtered = filtered; options = settings; }

    private sealed class Budget
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Options settings;
        internal Budget(Options options) { settings = options; }
        internal void Check(string phase, float progress)
        {
            if (settings.Cancel != null && settings.Cancel(phase, progress)) throw new OperationCanceledException("Đã hủy; chưa lưu tài nguyên.");
            if (clock.Elapsed.TotalSeconds > settings.TimeLimitSeconds)
                throw new InvalidOperationException("Xử lý vượt giới hạn thời gian; giảm kích thước ảnh hoặc mức xử lý rồi thử lại.");
        }
    }

    // Colour boundaries are the default. Dark fills participate in the palette.
    internal static ColorArtworkSegmentation Analyze(Color32[] source, int width, int height,
        float mergeDistance, int minimumPixels, bool denoise)
        => Analyze(source, width, height, new Options { MergeDistance = mergeDistance,
            MinimumPixels = minimumPixels, Denoise = denoise ? 0.25f : 0f });

    internal static ColorArtworkSegmentation Analyze(Color32[] source, int width, int height, Options settings)
    {
        if (width <= 0 || height <= 0 || (long)width * height > 4194304 || source == null || source.Length != width * height)
            throw new ArgumentException("Ảnh phải có kích thước hợp lệ, tối đa 4 triệu pixel; giảm Max Size trong importer nếu cần.");
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        float mergeDistance = Mathf.Clamp(settings.MergeDistance, 2f, 40f);
        int minimumPixels = Mathf.Max(1, settings.MinimumPixels);
        float edgeThreshold = Mathf.Lerp(38f, 12f, Mathf.Clamp01(settings.KeepEdges));
        var budget = new Budget(settings); budget.Check("Chuẩn bị màu / alpha", 0);
        // One small Lab lookup, rather than a Vector3 per pixel per filter pass.
        var lab = new Vector3[32768];
        for (int key = 0; key < lab.Length; key++)
            lab[key] = Lab(new Vector3(((key >> 10) & 31) * 8 + 4, ((key >> 5) & 31) * 8 + 4, (key & 31) * 8 + 4));
        var ink = new bool[source.Length];
        if (settings.PreserveInk)
            for (int p = 0; p < source.Length; p++)
            {
                if ((p & 16383) == 0) budget.Check("Giữ nét tối", 0.05f);
                Color32 c = source[p]; int max = Math.Max(c.r, Math.Max(c.g, c.b)), min = Math.Min(c.r, Math.Min(c.g, c.b));
                // Neutral dark strokes: do not turn saturated red/green fills into ink.
                ink[p] = c.a >= 128 && max <= 110 && max - min <= 38;
            }
        // Neutral anti-alias pixels need nearby ink; bright highlights stay paintable.
        if (settings.PreserveInk)
        {
            var antialias = (bool[])ink.Clone();
            for (int y = 1; y < height - 1; y++)
            {
                if ((y & 31) == 0) budget.Check("Biên chống răng cưa của nét", 0.07f);
                for (int x = 1; x < width - 1; x++)
                {
                    int p = y * width + x; Color32 c = source[p];
                    int max = Math.Max(c.r, Math.Max(c.g, c.b)), min = Math.Min(c.r, Math.Min(c.g, c.b));
                    if (c.a < 128 || max > 200 || max - min > 38 || ink[p]) continue;
                    int support = 0;
                    for (int yy = y - 1; yy <= y + 1; yy++) for (int xx = x - 1; xx <= x + 1; xx++) if (ink[yy * width + xx]) support++;
                    // Two opposing endpoints are a gap, not a side anti-alias pixel.
                    // Leave this decision to the explicit JoinFaintGaps option.
                    if (support == 2 && (ink[p - 1] && ink[p + 1] || ink[p - width] && ink[p + width])) continue;
                    if (support >= (max <= 180 ? 2 : 5)) antialias[p] = true;
                }
            }
            ink = antialias;
        }
        if (settings.JoinFaintGaps)
        {
            var closed = (bool[])ink.Clone();
            for (int y = 1; y < height - 1; y++)
            {
                if ((y & 31) == 0) budget.Check("Nối khe nét nhạt", 0.08f);
                for (int x = 1; x < width - 1; x++)
                {
                    int p = y * width + x; Color32 c = source[p];
                    // Only an existing faint ink hint may close a one-pixel gap.
                    // A bright intentional opening stays open.
                    if (c.a < 128 || Math.Max(c.r, Math.Max(c.g, c.b)) > 140 || Math.Max(c.r, Math.Max(c.g, c.b)) - Math.Min(c.r, Math.Min(c.g, c.b)) > 38) continue;
                    if (ink[p - 1] && ink[p + 1] || ink[p - width] && ink[p + width]) closed[p] = true;
                }
            }
            ink = closed;
        }
        Color32[] pixels = FilterEdges(source, width, height, ink, lab, settings.Denoise, edgeThreshold, budget);
        var histogram = new int[32768]; var sums = new Vector3[histogram.Length];
        for (int p = 0; p < pixels.Length; p++)
        {
            if ((p & 16383) == 0) budget.Check("Gộp màu Lab", 0.35f);
            if (source[p].a < 128 || ink[p]) continue;
            int key = Key(pixels[p]); histogram[key]++;
            sums[key] += new Vector3(pixels[p].r, pixels[p].g, pixels[p].b);
        }
        int[] buckets = Enumerable.Range(0, histogram.Length).Where(i => histogram[i] > 0)
            .OrderByDescending(i => histogram[i]).ThenBy(i => i).ToArray();
        var palette = new List<Vector3>(); var mapping = new int[histogram.Length];
        foreach (int bucket in buckets)
        {
            if ((bucket & 255) == 0) budget.Check("Tạo palette", 0.4f);
            Vector3 color = Lab(sums[bucket] / histogram[bucket]); int best = -1; float distance = float.PositiveInfinity;
            for (int k = 0; k < palette.Count; k++)
            { float d = (color - palette[k]).sqrMagnitude; if (d < distance) { distance = d; best = k; } }
            if (distance > mergeDistance * mergeDistance && palette.Count < 256)
            { best = palette.Count; palette.Add(color); }
            mapping[bucket] = best + 1;
        }
        var classes = new int[pixels.Length];
        for (int i = 0; i < pixels.Length; i++) classes[i] = source[i].a < 128 || ink[i] ? 0 : mapping[Key(pixels[i])];
        if (settings.SmoothBoundaries)
        {
            var smooth = (int[])classes.Clone();
            for (int y = 1; y < height - 1; y++)
            {
                if ((y & 31) == 0) budget.Check("Làm mượt biên yếu", 0.45f);
                for (int x = 1; x < width - 1; x++)
                {
                    int p = y * width + x; if (classes[p] <= 0) continue;
                    int candidate = classes[p], count = 0; bool safe = true;
                    for (int yy = y - 1; yy <= y + 1; yy++) for (int xx = x - 1; xx <= x + 1; xx++)
                    {
                        int n = yy * width + xx;
                        if (classes[n] <= 0 || (lab[Key(pixels[p])] - lab[Key(pixels[n])]).sqrMagnitude > 64f) safe = false;
                        if (count == 0) candidate = classes[n]; count += candidate == classes[n] ? 1 : -1;
                    }
                    if (!safe || candidate <= 0 || candidate == classes[p]) continue;
                    count = 0; for (int yy = y - 1; yy <= y + 1; yy++) for (int xx = x - 1; xx <= x + 1; xx++) if (classes[yy * width + xx] == candidate) count++;
                    if (count >= 6) smooth[p] = candidate;
                }
            }
            classes = smooth;
        }
        var labels = new int[pixels.Length]; var queue = new int[pixels.Length];
        var components = new List<List<int>> { new List<int>() }; var means = new List<Vector3> { Vector3.zero };
        for (int start = 0; start < labels.Length; start++)
        {
            if ((start & 16383) == 0) budget.Check("Tách vùng liên thông", 0.55f);
            if (classes[start] == 0 || labels[start] != 0) continue;
            int id = components.Count, head = 0, tail = 0; queue[tail++] = start; labels[start] = id; Vector3 sum = Vector3.zero;
            while (head < tail)
            {
                if ((head & 16383) == 0) budget.Check("Tách vùng liên thông", 0.55f);
                int p = queue[head++], x = p % width, y = p / width; sum += lab[Key(pixels[p])];
                Visit(p - 1, x > 0); Visit(p + 1, x + 1 < width); Visit(p - width, y > 0); Visit(p + width, y + 1 < height);
                void Visit(int n, bool valid)
                {
                    if (!valid || labels[n] != 0 || classes[n] != classes[start]) return;
                    // A palette anchor can cover two nearby colours. Keep their
                    // coherent sharp edge instead of connecting across it.
                    if ((lab[Key(pixels[p])] - lab[Key(pixels[n])]).sqrMagnitude >= edgeThreshold * edgeThreshold) return;
                    labels[n] = id; queue[tail++] = n;
                }
            }
            var items = new List<int>(tail); for (int i = 0; i < tail; i++) items.Add(queue[i]);
            components.Add(items); means.Add(sum / tail);
            if (components.Count > 100000) throw new InvalidOperationException("Ảnh có quá nhiều mảng vụn. Tăng giảm nhiễu hoặc gộp màu rồi phân tích lại.");
        }
        var parent = Enumerable.Range(0, components.Count).ToArray(); var sizes = components.Select(c => c.Count).ToArray();
        int Root(int id) { int root = id; while (parent[root] != root) root = parent[root]; while (parent[id] != id) { int next = parent[id]; parent[id] = root; id = next; } return root; }
        var result = new ColorArtworkSegmentation(width, height, labels, ink, pixels, settings);
        // Small-region candidates include the actual shared-edge contrast; no merge through ink.
        foreach (int id in Enumerable.Range(1, components.Count - 1).OrderBy(i => sizes[i]).ThenBy(i => i))
        {
            if ((id & 127) == 0) budget.Check("Gộp mảng theo màu và cạnh", 0.75f);
            if (Root(id) != id) continue;
            var neighbours = new Dictionary<int, Vector2>();
            foreach (int p in components[id])
            {
                int x = p % width, y = p / width;
                Edge(p - 1, x > 0); Edge(p + 1, x + 1 < width); Edge(p - width, y > 0); Edge(p + width, y + 1 < height);
                void Edge(int n, bool valid)
                {
                    if (!valid) return; if (ink[n] || labels[n] == 0) return;
                    int other = Root(labels[n]); if (other == id) return;
                    neighbours.TryGetValue(other, out Vector2 shared);
                    shared.x++; shared.y += Vector3.Distance(lab[Key(pixels[p])], lab[Key(pixels[n])]); neighbours[other] = shared;
                }
            }
            int target = -1; float score = float.PositiveInfinity;
            foreach (var pair in neighbours.OrderBy(n => n.Key))
            {
                float color = Vector3.Distance(means[id], means[pair.Key]), edge = pair.Value.y / pair.Value.x;
                bool small = sizes[id] < minimumPixels;
                // Ink remains a hard barrier. Coherent strong edges protect larger details.
                if (small ? edge >= edgeThreshold * 1.8f && color >= mergeDistance
                    : color > mergeDistance * 0.65f || edge > edgeThreshold * 0.65f) continue;
                float d = (color * color + edge * edge * 2f) / Mathf.Sqrt(pair.Value.x);
                if (d < score) { score = d; target = pair.Key; }
            }
            if (target <= 0) continue;
            means[target] = (means[target] * sizes[target] + means[id] * sizes[id]) / (sizes[target] + sizes[id]);
            sizes[target] += sizes[id]; parent[id] = target;
            components[target].AddRange(components[id]); components[id].Clear(); result.SmallRegionsMerged++;
        }
        for (int i = 0; i < labels.Length; i++) if (labels[i] > 0) labels[i] = Root(labels[i]);
        result.RegionCount = labels.Where(id => id > 0).Distinct().Count(); budget.Check("Hoàn tất phân vùng", 1);
        return result;
    }

    private static Color32[] FilterEdges(Color32[] source, int width, int height, bool[] ink, Vector3[] lab, float amount, float edgeThreshold, Budget budget)
    {
        amount = Mathf.Clamp01(amount); if (amount <= 0f) return source;
        var current = source; float range = Mathf.Lerp(6f, 60f, amount) * Mathf.Lerp(1f, 0.7f, 1f - (edgeThreshold - 12f) / 26f);
        for (int pass = 0; pass < 2; pass++)
        {
            var next = new Color32[source.Length];
            for (int y = 0; y < height; y++)
            {
                if ((y & 15) == 0) budget.Check("Lọc nhiễu giữ cạnh", 0.1f + 0.2f * (pass + (float)y / height) / 2);
                for (int x = 0; x < width; x++)
                {
                    int p = y * width + x; if (source[p].a < 128 || ink[p]) { next[p] = source[p]; continue; }
                    Vector3 center = lab[Key(current[p])], sum = Vector3.zero; float weights = 0;
                    for (int yy = Mathf.Max(0,y-1); yy <= Mathf.Min(height-1,y+1); yy++)
                    for (int xx = Mathf.Max(0,x-1); xx <= Mathf.Min(width-1,x+1); xx++)
                    {
                        int n = yy * width + xx; if (source[n].a < 128 || ink[n]) continue;
                        float difference = (center - lab[Key(current[n])]).sqrMagnitude;
                        float spatial = xx == x && yy == y ? 4f : xx == x || yy == y ? 2f : 1f;
                        float weight = spatial / (1f + difference / (range * range)); weight *= weight;
                        Color32 c = current[n]; sum += new Vector3(c.r,c.g,c.b) * weight; weights += weight;
                    }
                    Vector3 filtered = Vector3.Lerp(new Vector3(current[p].r,current[p].g,current[p].b),sum / weights,amount);
                    next[p] = new Color32((byte)Mathf.RoundToInt(filtered.x),(byte)Mathf.RoundToInt(filtered.y),(byte)Mathf.RoundToInt(filtered.z),source[p].a);
                }
            }
            current = next;
        }
        return current;
    }

    internal bool MergeAdjacent(int first, int second)
    {
        if (first <= 0 || second <= 0 || first == second) return false;
        bool adjacent = false;
        for (int p = 0; p < Labels.Length && !adjacent; p++)
        {
            if (Labels[p] != first) continue;
            int x = p % Width, y = p / Width;
            adjacent = x > 0 && Labels[p - 1] == second || x + 1 < Width && Labels[p + 1] == second
                || y > 0 && Labels[p - Width] == second || y + 1 < Height && Labels[p + Width] == second;
        }
        if (!adjacent) return false;
        for (int p = 0; p < Labels.Length; p++) if (Labels[p] == second) Labels[p] = first;
        RegionCount--;
        return true;
    }

    internal sealed class Output
    {
        internal Color32[] Lines, Mask, Overlay;
        internal List<ColoringRegionSpanItem> Regions;
        internal int LostRegions, PaintablePixels;
        internal HashSet<int> LostRegionIds;
    }

    // Additional separators clip the existing labels; they never reclassify colours.
    // Erasing removes only hand-drawn separators, preserving automatic boundaries.
    internal void DrawManualBoundary(int x0, int y0, int x1, int y1, int radius, bool erase)
    {
        if (x0 < 0 || y0 < 0 || x1 < 0 || y1 < 0 || x0 >= Width || x1 >= Width || y0 >= Height || y1 >= Height)
            throw new ArgumentOutOfRangeException(nameof(x0));
        if (manualBoundaries == null) manualBoundaries = new bool[Labels.Length];
        radius = Mathf.Clamp(radius, 0, 6);
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, error = dx + dy;
        while (true)
        {
            for (int y = Math.Max(0, y0 - radius); y <= Math.Min(Height - 1, y0 + radius); y++)
            for (int x = Math.Max(0, x0 - radius); x <= Math.Min(Width - 1, x0 + radius); x++)
                if ((x - x0) * (x - x0) + (y - y0) * (y - y0) <= radius * radius)
                    manualBoundaries[y * Width + x] = !erase;
            if (x0 == x1 && y0 == y1) break;
            int twice = error * 2;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
    }

    internal Output BuildOutput(HashSet<int> excluded, int thickness)
    {
        thickness = Mathf.Clamp(thickness, 1, 8);
        var budget = new Budget(options);
        int length = Labels.Length; var edges = new bool[length]; var lines = (bool[])Ink.Clone();
        var areas = new Dictionary<int, int>();
        for (int p = 0; p < length; p++)
            if (Labels[p] > 0) { areas.TryGetValue(Labels[p], out int area); areas[Labels[p]] = area + 1; }
        for (int p = 0; p < length; p++)
        {
            int x = p % Width, y = p / Width;
            if ((p & 16383) == 0) budget.Check("Sinh nét từ vùng / nét gốc", 0.1f);
            // The image rectangle is not a drawn contour. Do not consume details
            // touching the border merely to manufacture a closed outer frame.
            if (x + 1 < Width) Edge(p, p + 1);
            if (y + 1 < Height) Edge(p, p + Width);
        }
        void Edge(int p, int n)
        {
            // Actual ink already separates pixels. Nearby ink does not: skipping
            // that whole neighbourhood left invisible joins between distinct IDs.
            if (Labels[p] == Labels[n] || Ink[p] || Ink[n]) return;
            // At one-pixel thickness, place the contour on the larger region's side
            // instead of consuming a narrow detail merely because it is left/bottom.
            int target = Labels[p] <= 0 ? n : Labels[n] <= 0 ? p
                : areas[Labels[p]] < areas[Labels[n]] ? n : p;
            edges[target] = true;
        }
        int low = (thickness - 1) / 2, high = thickness / 2;
        for (int p = 0; p < length; p++)
        {
            if ((p & 16383) == 0) budget.Check("Độ dày nét sinh", 0.25f);
            if (!edges[p]) continue;
            int x = p % Width, y = p / Width;
            for (int yy = Mathf.Max(0, y - low); yy <= Mathf.Min(Height - 1, y + high); yy++)
            for (int xx = Mathf.Max(0, x - low); xx <= Mathf.Min(Width - 1, x + high); xx++) lines[yy * Width + xx] = true;
        }
        if (manualBoundaries != null)
            for (int p = 0; p < length; p++) lines[p] |= manualBoundaries[p];
        var output = new Output { Lines = new Color32[length], Mask = new Color32[length], Overlay = new Color32[length], Regions = new List<ColoringRegionSpanItem>() };
        var interior = new int[length]; var queue = new int[length]; var surviving = new HashSet<int>();
        // Thick boundaries can split a narrow region. Each surviving connected interior
        // is exported separately so one click cannot jump to a disconnected patch.
        for (int start = 0; start < length; start++)
        {
            if ((start & 16383) == 0) budget.Check("Tạo vùng tô / span", 0.45f);
            int rawId = Labels[start];
            if (rawId <= 0 || excluded.Contains(rawId) || lines[start] || interior[start] != 0) continue;
            int id = output.Regions.Count + 1, head = 0, tail = 0;
            queue[tail++] = start; interior[start] = id;
            while (head < tail)
            {
                if ((head & 16383) == 0) budget.Check("Tạo vùng tô / span", 0.55f);
                int p = queue[head++], x = p % Width, y = p / Width;
                Visit(p - 1, x > 0); Visit(p + 1, x + 1 < Width);
                Visit(p - Width, y > 0); Visit(p + Width, y + 1 < Height);
                void Visit(int n, bool valid)
                {
                    if (!valid || interior[n] != 0 || lines[n] || Labels[n] != rawId) return;
                    interior[n] = id; queue[tail++] = n;
                }
            }
            surviving.Add(rawId); Array.Sort(queue, 0, tail);
            var spans = new List<ColoringPixelSpan>();
            for (int i = 0; i < tail;)
            {
                int first = queue[i++], count = 1;
                while (i < tail && queue[i] == first + count && queue[i] / Width == first / Width) { count++; i++; }
                spans.Add(new ColoringPixelSpan { start = first, length = count });
            }
            output.Regions.Add(new ColoringRegionSpanItem { id = id, pixelCount = tail, spans = spans.ToArray() });
            output.PaintablePixels += tail;
        }
        output.LostRegionIds = new HashSet<int>(Labels.Where(id => id > 0 && !excluded.Contains(id) && !surviving.Contains(id)));
        output.LostRegions = output.LostRegionIds.Count;
        for (int p = 0; p < length; p++)
        {
            if ((p & 16383) == 0) budget.Check("Preview đồng bộ", 0.9f);
            byte line = (byte)(lines[p] ? 0 : 255), mask = (byte)(interior[p] > 0 ? 255 : 0);
            output.Lines[p] = new Color32(line, line, line, 255);
            output.Mask[p] = new Color32(mask, mask, mask, 255);
            Color c = interior[p] > 0 ? Color.HSVToRGB((interior[p] * 0.618034f) % 1f, 0.5f, 0.95f)
                : excluded.Contains(Labels[p]) ? new Color(0.55f, 0.55f, 0.55f) : Color.white;
            output.Overlay[p] = output.LostRegionIds.Contains(Labels[p]) ? new Color32(255, 0, 180, 255)
                : lines[p] ? new Color32(0, 0, 0, 255) : (Color32)c;
        }
        budget.Check("Hoàn tất", 1);
        return output;
    }

    private static int Key(Color32 c) => (c.r >> 3) << 10 | (c.g >> 3) << 5 | c.b >> 3;
    private static Vector3 Lab(Vector3 rgb)
    {
        float r = Mathf.GammaToLinearSpace(rgb.x / 255f), g = Mathf.GammaToLinearSpace(rgb.y / 255f), b = Mathf.GammaToLinearSpace(rgb.z / 255f);
        float x = F((0.4124564f * r + 0.3575761f * g + 0.1804375f * b) / 0.95047f);
        float y = F(0.2126729f * r + 0.7151522f * g + 0.0721750f * b);
        float z = F((0.0193339f * r + 0.1191920f * g + 0.9503041f * b) / 1.08883f);
        return new Vector3(116f * y - 16f, 500f * (x - y), 200f * (y - z));
        float F(float v) => v > 0.008856f ? Mathf.Pow(v, 1f / 3f) : 7.787f * v + 16f / 116f;
    }
}
