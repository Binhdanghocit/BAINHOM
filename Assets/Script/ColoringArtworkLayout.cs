using UnityEngine;
using UnityEngine.UI;

// Shared geometry for the editor preview and runtime (positive Y points up).
public static class ColoringArtworkLayout
{
    public static Vector2 Fit(Vector2 image, Vector2 available)
    {
        if (image.x <= 0 || image.y <= 0) return Vector2.zero;
        return image * Mathf.Max(0, Mathf.Min(available.x / image.x, available.y / image.y));
    }

    public static Rect ReferenceRect(Vector2 line, Vector2 reference, Vector2 available, float scale, Vector2 offset)
    {
        Vector2 size = Fit(reference, available) * Mathf.Max(0.05f, scale);
        Vector2 center = Vector2.Scale(offset, Fit(line, available));
        return new Rect(center - size * 0.5f, size);
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
