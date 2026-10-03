using UnityEngine;
using UnityEngine.UI;

// Shared geometry for the editor preview and runtime (positive Y points up).
public static class ColoringArtworkLayout
{
    public static Vector2 Fit(Vector2 image, Vector2 available)
    {
        float factor = FitFactor(image, available);
        return factor > 0f ? image * factor : Vector2.zero;
    }

    private static bool ValidSize(Vector2 size)
        => Finite(size.x) && Finite(size.y) && size.x > 0f && size.y > 0f;

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static float FitFactor(Vector2 image, Vector2 available)
    {
        if (!ValidSize(image) || !ValidSize(available)) return 0f;
        float factor = Mathf.Min(available.x / image.x, available.y / image.y);
        return Finite(factor) ? factor : 0f;
    }

    public static Rect ReferenceRect(Vector2 line, Vector2 reference, Vector2 available, float scale, Vector2 offset)
    {
        if (!ValidSize(line) || !ValidSize(reference) || !ValidSize(available)) return new Rect();
        float fit = FitFactor(line, available);
        if (!Finite(scale)) scale = 1f;
        if (!Finite(offset.x)) offset.x = 0f;
        if (!Finite(offset.y)) offset.y = 0f;
        Vector2 size = reference * (fit * Mathf.Max(0.05f, scale));
        Vector2 center = Vector2.Scale(offset, line * fit);
        return new Rect(center - size * 0.5f, size);
    }

    public static Rect ReferencePreviewRect(Vector2 line, Vector2 reference, Rect area, float scale, Vector2 offset)
    {
        Rect aligned = ReferenceRect(line, reference, area.size, scale, offset);
        // Editor GUI Y points down; the stored offset and runtime Y point up.
        return new Rect(area.center + new Vector2(aligned.xMin, -aligned.yMax), aligned.size);
    }

    public static RectTransform Prepare(RawImage image, RectTransform frame, bool reference)
    {
        if (image == null || frame == null) return null;
        Transform existing = frame.Find("ImageViewport");
        RectTransform viewport = existing as RectTransform;
        if (viewport == null)
        {
            viewport = new GameObject("ImageViewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(frame, false);
        }
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(12, 12);
        viewport.offsetMax = new Vector2(-12, -34);
        RectTransform parent = viewport;
        if (reference)
        {
            parent = viewport.Find("ReferenceAlignment") as RectTransform;
            if (parent == null)
            {
                parent = new GameObject("ReferenceAlignment", typeof(RectTransform)).GetComponent<RectTransform>();
                parent.SetParent(viewport, false);
                Center(parent);
                parent.sizeDelta = Vector2.zero;
            }
        }
        if (image.transform.parent != parent) image.transform.SetParent(parent, false);
        AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>();
        if (fitter != null) fitter.enabled = false;
        Center(image.rectTransform);
        image.rectTransform.localScale = Vector3.one;
        return viewport;
    }

    public static void Center(RectTransform rect)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
