using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class TestResponsiveLayoutSuite
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> results = new List<string>();
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }
    private static object Call(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static T Get<T>(object target, string field)
        => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        ColoringArtworkLayout.Center(rect);
        return rect;
    }
    public static void Run()
    {
        Directory.CreateDirectory("Logs");
        try
        {
            foreach (string name in new[] { "MainMenu", "SampleScene" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                var games = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ColoringPageMinigame>(true));
                foreach (var game in games)
                foreach (var art in game.paintings)
                {
                    Check(art.referenceScale == 1 && art.referenceOffset == Vector2.zero,
                        name + " saved art " + art.title + ": scale/offset unchanged; line " + art.lineArt.width + "x" + art.lineArt.height
                        + ", reference " + art.referenceArt.width + "x" + art.referenceArt.height);
                    results.Add("DATA: native reference/line ratios " + (float)art.referenceArt.width / art.lineArt.width + ","
                        + (float)art.referenceArt.height / art.lineArt.height + "; default scale/offset retained without implicit conversion.");
                }
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var canvas = new GameObject("Artwork fixture", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var frame = Rect("Line frame", canvas.transform);
            var refFrame = Rect("Reference frame", canvas.transform);
            var image = new GameObject("Artwork", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(frame, false);
            var reference = new GameObject("Reference", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            reference.transform.SetParent(refFrame, false);
            var gameLayout = image.gameObject.AddComponent<ColoringPageMinigame>();
            gameLayout.coloringImage = image;
            gameLayout.coloringImageFrame = frame;
            gameLayout.referenceImage = reference;
            gameLayout.referenceImageFrame = refFrame;
            var lineViewport = ColoringArtworkLayout.Prepare(image, frame, false);
            ColoringArtworkLayout.Prepare(reference, refFrame, true);
            int cases = 0;
            foreach (Vector2 line in new[] { new Vector2(16, 9), new Vector2(9, 16), new Vector2(10, 10) })
            {
                var lineTexture = new Texture2D((int)line.x, (int)line.y, TextureFormat.RGBA32, false);
                lineTexture.SetPixels(Enumerable.Repeat(Color.white, lineTexture.width * lineTexture.height).ToArray());
                lineTexture.Apply();
                var refTexture = new Texture2D(7, 10);
                var art = new ColoringArtworkDefinition { lineArt = lineTexture, referenceArt = refTexture };
                gameLayout.paintings = new[] { art };
                typeof(ColoringPageMinigame).GetField("selectedPaintingIndex", Private).SetValue(gameLayout, 0);
                foreach (float scale in new[] { 0.5f, 1f, 2f })
                foreach (Vector2 available in new[] { new Vector2(800, 400), new Vector2(400, 800), new Vector2(1600, 600), new Vector2(300, 250) })
                {
                    art.referenceScale = scale;
                    art.referenceOffset = new Vector2(0.2f, -0.15f);
                    frame.sizeDelta = refFrame.sizeDelta = available + new Vector2(24, 46);
                    Canvas.ForceUpdateCanvases();
                    gameLayout.RefreshArtworkLayout();
                    Vector2 fitted = image.rectTransform.rect.size;
                    Vector2 referenceSize = reference.rectTransform.rect.size;
                    Vector2 center = ((RectTransform)reference.transform.parent).anchoredPosition;
                    // Independent normalized relationships, not expected values from the helper.
                    Check(Mathf.Abs(referenceSize.x / fitted.x - 7f / line.x * scale) < 0.0001f
                        && Mathf.Abs(referenceSize.y / fitted.y - 10f / line.y * scale) < 0.0001f,
                        "Reference/line dimension ratios invariant, " + line + ", viewport " + available + ", scale " + scale);
                    Check(Mathf.Abs(center.x / fitted.x - 0.2f) < 0.0001f && Mathf.Abs(center.y / fitted.y + 0.15f) < 0.0001f,
                        "Normalized offset stays invariant after viewport resize");
                    Rect preview = ColoringArtworkLayout.ReferencePreviewRect(line, new Vector2(7, 10), new Rect(20, 30, available.x, available.y), scale, art.referenceOffset);
                    Check(Vector2.Distance(preview.size, referenceSize) < 0.01f
                        && Vector2.Distance(preview.center - new Vector2(20, 30) - available * 0.5f, new Vector2(center.x, -center.y)) < 0.01f,
                        "Editor GUI and runtime match after Y-axis conversion");
                    cases++;
                }
                UnityEngine.Object.DestroyImmediate(lineTexture);
                UnityEngine.Object.DestroyImmediate(refTexture);
            }
            Check(ColoringArtworkLayout.ReferenceRect(new Vector2(1600, 900), new Vector2(700, 1000), new Vector2(800, 600), 1, Vector2.zero).size == new Vector2(350, 500),
                "Independent numeric example: line fit 0.5 gives reference 350x500, not independent reference fit 420x600");
            foreach (Vector2 bad in new[] { Vector2.zero, new Vector2(-1, 20), new Vector2(float.NaN, 20), new Vector2(float.PositiveInfinity, 20) })
            {
                Check(ColoringArtworkLayout.Fit(bad, Vector2.one) == Vector2.zero && ColoringArtworkLayout.Fit(Vector2.one, bad) == Vector2.zero,
                    "Invalid image/viewport produces finite zero-sized fit");
                Check(ColoringArtworkLayout.ReferenceRect(Vector2.one, bad, Vector2.one, 1, Vector2.zero).size == Vector2.zero,
                    "Invalid reference dimensions produce zero rect");
            }
            var safeInvalid = ColoringArtworkLayout.ReferenceRect(new Vector2(16, 9), new Vector2(7, 10), new Vector2(800, 600), float.NaN, new Vector2(float.NaN, float.PositiveInfinity));
            Check(safeInvalid.center == Vector2.zero && !float.IsNaN(safeInvalid.width), "Invalid scale/offset uses finite defaults");

            // Paint actual center region, then resize without selecting again.
            var texture = new Texture2D(10, 10, TextureFormat.RGBA32, false);
            texture.SetPixels(Enumerable.Repeat(Color.white, 100).ToArray()); texture.Apply();
            var data = new TextAsset(JsonUtility.ToJson(new ColoringRegionDataAsset { width = 10, height = 10, regionCount = 1,
                regions = new[] { new ColoringRegionSpanItem { id = 1, pixelCount = 1, spans = new[] { new ColoringPixelSpan { start = 55, length = 1 } } } } }));
            gameLayout.paintings = new[] { new ColoringArtworkDefinition { lineArt = texture, regionData = data } };
            Call(gameLayout, "PreparePaintingList");
            gameLayout.SelectPainting(0);
            gameLayout.SelectPaletteIndex(1);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, image.rectTransform.TransformPoint(image.rectTransform.rect.center));
            gameLayout.OnPointerClick(new PointerEventData(null) { position = point });
            var painted = Get<Texture2D>(gameLayout, "workingTexture");
            Check(Get<int>(gameLayout, "paintedRegionCount") == 1, "Actual pointer callback paints the center region");
            Color pixel = painted.GetPixel(5, 5);
            frame.sizeDelta = refFrame.sizeDelta = new Vector2(500, 220);
            Canvas.ForceUpdateCanvases();
            Call(gameLayout, "UpdateArtworkLayout");
            Check(Get<Texture2D>(gameLayout, "workingTexture") == painted && painted.GetPixel(5, 5) == pixel && Get<int>(gameLayout, "paintedRegionCount") == 1,
                "Viewport resize keeps working texture, painted color and progress without reselection");
            UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(data);

            var overlay = MobileControlsOverlay.FindOrCreate();
            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            var stick = Get<RectTransform>(overlay, "stickRoot");
            foreach (float factor in new[] { 0.5f, 1f, 2f })
            {
                scaler.scaleFactor = factor;
                typeof(CanvasScaler).GetMethod("Handle", Private).Invoke(scaler, null);
                Canvas.ForceUpdateCanvases();
                MobileControlsOverlay.SetFloatingJoystickEnabled(false);
                Call(overlay, "ShowStickAt", (object)null);
                TestStick(overlay, stick, "Fixed, CanvasScaler " + factor);
                MobileControlsOverlay.SetFloatingJoystickEnabled(true);
                Call(overlay, "ShowStickAt", new Vector2(1, 1));
                TestStick(overlay, stick, "Floating clamped, CanvasScaler " + factor);
                Call(overlay, "UpdateJoystickTouch", new Vector2(1, 1), TouchPhase.Began);
                Call(overlay, "UpdateJoystickTouch", new Vector2(1, 1), TouchPhase.Stationary);
                Check(MobileControlsOverlay.Move == Vector2.zero, "Clamping a new stationary touch does not start movement");
                Vector2 center = RectTransformUtility.WorldToScreenPoint(null, stick.TransformPoint(stick.rect.center));
                Call(overlay, "UpdateJoystickTouch", center, TouchPhase.Moved);
                Check(MobileControlsOverlay.Move.sqrMagnitude < 0.0001f, "Dragged touch at clamped visual center returns zero");
                Vector2 move = (Vector2)Call(overlay, "CalculateJoystickMove", center);
                Call(overlay, "ShowStickAt", new Vector2(1, 1));
                Check(Vector2.Distance(move, (Vector2)Call(overlay, "CalculateJoystickMove", center)) < 0.0001f,
                    "Repeated clamp does not change center input");
            }
            UnityEngine.Object.DestroyImmediate(overlay.gameObject);
            results.Add("RESULT: PASS, " + cases + " cross-aspect artwork cases, resize/progress and joystick at three CanvasScaler factors. Geometry/callback checks only; physical touch/VR input not verified.");
            File.WriteAllLines("Logs/ResponsiveLayoutResults.txt", results);
            Debug.Log(results.Last());
        }
        catch (Exception error)
        {
            results.Add("RESULT: FAIL\n" + error);
            File.WriteAllLines("Logs/ResponsiveLayoutResults.txt", results);
            throw;
        }
    }

    public static void RunAndRegression()
    {
        Run();
        TestGameplayRegressionSuite.RunReviewChecks();
    }

    private static void TestStick(MobileControlsOverlay overlay, RectTransform stick, string label)
    {
        Vector2 center = stick.rect.center;
        float radius = (float)Call(overlay, "JoystickRadius");
        foreach (Vector2 expected in new[] { Vector2.zero, Vector2.right, Vector2.up, new Vector2(-0.5f, 0.5f) })
        {
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, stick.TransformPoint(center + expected * radius));
            Check(Vector2.Distance((Vector2)Call(overlay, "CalculateJoystickMove", position), expected) < 0.001f,
                label + ": center/direction/magnitude matches displayed local radius, expected " + expected);
        }
        Vector2 outside = RectTransformUtility.WorldToScreenPoint(null, stick.TransformPoint(center + new Vector2(2, 2) * radius));
        Check(Vector2.Distance((Vector2)Call(overlay, "CalculateJoystickMove", outside), new Vector2(1, 1).normalized) < 0.001f,
            label + ": diagonal beyond radius clamps to unit length");
    }
}
