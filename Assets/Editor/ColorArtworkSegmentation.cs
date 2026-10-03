using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Editor-only. Labels are spatial components, even when two components share a colour.
internal sealed class ColorArtworkSegmentation
{
    internal readonly int Width, Height;
    internal readonly int[] Labels;
    internal int SmallRegionsMerged { get; private set; }
    internal int RegionCount { get; private set; }

    private ColorArtworkSegmentation(int width, int height, int[] labels)
    { Width = width; Height = height; Labels = labels; }

    internal static ColorArtworkSegmentation Analyze(Color32[] source, int width, int height,
        float mergeDistance, int minimumPixels, bool denoise)
    {
        if (width <= 0 || height <= 0 || (long)width * height > 4194304 || source == null || source.Length != width * height)
            throw new ArgumentException("Ảnh phải có kích thước hợp lệ, tối đa 4 triệu pixel; giảm Max Size trong importer nếu cần.");
        mergeDistance = Mathf.Clamp(mergeDistance, 2f, 40f);
        minimumPixels = Mathf.Max(1, minimumPixels);
        Color32[] pixels = denoise ? Median(source, width, height) : source;
        var histogram = new int[32768];
        var sums = new Vector3[histogram.Length];
        for (int p = 0; p < pixels.Length; p++)
        {
            if (source[p].a < 128) continue;
            int key = Key(pixels[p]); histogram[key]++;
            sums[key] += new Vector3(pixels[p].r, pixels[p].g, pixels[p].b);
        }
        // Frequent colours become fixed palette anchors. This avoids a chain of
        // tiny adjacent colour differences merging an entire gradient into one colour.
        int[] buckets = Enumerable.Range(0, histogram.Length).Where(i => histogram[i] > 0)
            .OrderByDescending(i => histogram[i]).ThenBy(i => i).ToArray();
        var palette = new List<Vector3>();
        var mapping = new int[histogram.Length];
        foreach (int bucket in buckets)
        {
            Vector3 rgb = sums[bucket] / histogram[bucket];
            Vector3 lab = Lab(rgb);
            int best = -1; float distance = float.PositiveInfinity;
            for (int k = 0; k < palette.Count; k++)
            {
                float d = (lab - palette[k]).sqrMagnitude;
                if (d < distance) { distance = d; best = k; }
            }
            if (distance > mergeDistance * mergeDistance && palette.Count < 256)
            { best = palette.Count; palette.Add(lab); }
            mapping[bucket] = best + 1;
        }
        var classes = new int[pixels.Length];
        for (int i = 0; i < pixels.Length; i++) classes[i] = source[i].a < 128 ? 0 : mapping[Key(pixels[i])];
        var labels = new int[pixels.Length]; var queue = new int[pixels.Length];
        var components = new List<List<int>> { new List<int>() };
        var means = new List<Vector3> { Vector3.zero };
        for (int start = 0; start < labels.Length; start++)
        {
            if (classes[start] == 0 || labels[start] != 0) continue;
            int id = components.Count, head = 0, tail = 0;
            queue[tail++] = start; labels[start] = id; Vector3 sum = Vector3.zero;
            while (head < tail)
            {
                int p = queue[head++], x = p % width, y = p / width;
                sum += palette[classes[p] - 1];
                Visit(p - 1, x > 0); Visit(p + 1, x + 1 < width);
                Visit(p - width, y > 0); Visit(p + width, y + 1 < height);
                void Visit(int n, bool valid)
                {
                    if (!valid || labels[n] != 0 || classes[n] != classes[start]) return;
                    labels[n] = id; queue[tail++] = n;
                }
            }
            var items = new List<int>(tail);
            for (int i = 0; i < tail; i++) items.Add(queue[i]);
            components.Add(items); means.Add(sum / tail);
            if (components.Count > 100000)
                throw new InvalidOperationException("Ảnh có quá nhiều mảng vụn. Bật giảm nhiễu hoặc tăng mức gộp màu rồi phân tích lại.");
        }
        var parent = Enumerable.Range(0, components.Count).ToArray();
        var sizes = components.Select(c => c.Count).ToArray();
        int Root(int id)
        {
            int root = id;
            while (parent[root] != root) root = parent[root];
            while (parent[id] != id) { int next = parent[id]; parent[id] = root; id = next; }
            return root;
        }
        var result = new ColorArtworkSegmentation(width, height, labels);
        // Absorb small fragments into an adjacent region, favouring colour similarity.
        // Transparent pixels are never a candidate and disconnected islands stay separate.
        foreach (int id in Enumerable.Range(1, components.Count - 1).OrderBy(i => sizes[i]).ThenBy(i => i))
        {
            if (Root(id) != id || sizes[id] >= minimumPixels) continue;
            var neighbours = new Dictionary<int, int>();
            foreach (int p in components[id])
            {
                int x = p % width, y = p / width;
                Edge(p - 1, x > 0); Edge(p + 1, x + 1 < width);
                Edge(p - width, y > 0); Edge(p + width, y + 1 < height);
                void Edge(int n, bool valid)
                {
                    if (!valid || labels[n] == 0) return;
                    int other = Root(labels[n]); if (other == id) return;
                    neighbours.TryGetValue(other, out int count); neighbours[other] = count + 1;
                }
            }
            int target = -1; float score = float.PositiveInfinity;
            foreach (var pair in neighbours.OrderBy(n => n.Key))
            {
                float d = (means[id] - means[pair.Key]).sqrMagnitude / Mathf.Sqrt(pair.Value);
                if (d < score) { score = d; target = pair.Key; }
            }
            if (target <= 0) continue;
            // Keep all pixel lists on the root so later small-region merges see the full boundary.
            means[target] = (means[target] * sizes[target] + means[id] * sizes[id]) / (sizes[target] + sizes[id]);
            sizes[target] += sizes[id]; parent[id] = target;
            components[target].AddRange(components[id]); components[id].Clear();
            result.SmallRegionsMerged++;
        }
        for (int i = 0; i < labels.Length; i++) if (labels[i] > 0) labels[i] = Root(labels[i]);
        result.RegionCount = labels.Where(id => id > 0).Distinct().Count();
        return result;
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

    internal Output BuildOutput(HashSet<int> excluded, int thickness)
    {
        thickness = Mathf.Clamp(thickness, 1, 8);
        int length = Labels.Length; var edges = new bool[length]; var lines = new bool[length];
        for (int p = 0; p < length; p++)
        {
            int x = p % Width, y = p / Width;
            edges[p] = Labels[p] > 0 && (x == 0 || y == 0 || x + 1 == Width || y + 1 == Height)
                || x + 1 < Width && Labels[p] != Labels[p + 1]
                || y + 1 < Height && Labels[p] != Labels[p + Width];
        }
        int low = (thickness - 1) / 2, high = thickness / 2;
        for (int p = 0; p < length; p++)
        {
            if (!edges[p]) continue;
            int x = p % Width, y = p / Width;
            for (int yy = Mathf.Max(0, y - low); yy <= Mathf.Min(Height - 1, y + high); yy++)
            for (int xx = Mathf.Max(0, x - low); xx <= Mathf.Min(Width - 1, x + high); xx++) lines[yy * Width + xx] = true;
        }
        var output = new Output { Lines = new Color32[length], Mask = new Color32[length], Overlay = new Color32[length], Regions = new List<ColoringRegionSpanItem>() };
        var interior = new int[length]; var queue = new int[length]; var surviving = new HashSet<int>();
        // Thick boundaries can split a narrow region. Each surviving connected interior
        // is exported separately so one click cannot jump to a disconnected patch.
        for (int start = 0; start < length; start++)
        {
            int rawId = Labels[start];
            if (rawId <= 0 || excluded.Contains(rawId) || lines[start] || interior[start] != 0) continue;
            int id = output.Regions.Count + 1, head = 0, tail = 0;
            queue[tail++] = start; interior[start] = id;
            while (head < tail)
            {
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
            byte line = (byte)(lines[p] ? 0 : 255), mask = (byte)(interior[p] > 0 ? 255 : 0);
            output.Lines[p] = new Color32(line, line, line, 255);
            output.Mask[p] = new Color32(mask, mask, mask, 255);
            Color c = interior[p] > 0 ? Color.HSVToRGB((interior[p] * 0.618034f) % 1f, 0.5f, 0.95f)
                : excluded.Contains(Labels[p]) ? new Color(0.55f, 0.55f, 0.55f) : Color.white;
            output.Overlay[p] = output.LostRegionIds.Contains(Labels[p]) ? new Color32(255, 0, 180, 255)
                : lines[p] ? new Color32(0, 0, 0, 255) : (Color32)c;
        }
        return output;
    }

    private static int Key(Color32 c) => (c.r >> 3) << 10 | (c.g >> 3) << 5 | c.b >> 3;
    private static Color32[] Median(Color32[] source, int width, int height)
    {
        var result = new Color32[source.Length];
        var red = new byte[9]; var green = new byte[9]; var blue = new byte[9];
        for (int p = 0; p < source.Length; p++)
        {
            int x = p % width, y = p / width, count = 0;
            for (int yy = Mathf.Max(0, y - 1); yy <= Mathf.Min(height - 1, y + 1); yy++)
            for (int xx = Mathf.Max(0, x - 1); xx <= Mathf.Min(width - 1, x + 1); xx++)
            {
                Color32 c = source[yy * width + xx]; if (c.a < 128) continue;
                red[count] = c.r; green[count] = c.g; blue[count++] = c.b;
            }
            if (count == 0) { result[p] = source[p]; continue; }
            Array.Sort(red, 0, count); Array.Sort(green, 0, count); Array.Sort(blue, 0, count);
            result[p] = new Color32(red[count / 2], green[count / 2], blue[count / 2], source[p].a);
        }
        return result;
    }
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
