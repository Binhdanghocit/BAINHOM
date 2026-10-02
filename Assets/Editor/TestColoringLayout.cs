using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class TestColoringLayout
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // Run only on the isolated validation project; scene output is copied back after validation.
    public static void MigrateAndTest()
    {
        Directory.CreateDirectory("Logs");
        int cases = 0;
        foreach (string name in new[] { "MainMenu", "SampleScene" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            var games = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ColoringPageMinigame>(true)).ToArray();
            Check(games.Length > 0, "No workshop in " + name);
            foreach (var game in games)
            {
                string before = JsonUtility.ToJson(new ArtworkSnapshot { paintings = game.paintings });
                ColoringArtworkLayout.Prepare(game.coloringImage, game.coloringImageFrame, false);
                ColoringArtworkLayout.Prepare(game.referenceImage, game.referenceImageFrame, true);
                if (game.lineArtOverlayImage != null)
                {
                    var fitter = game.lineArtOverlayImage.GetComponent<AspectRatioFitter>();
                    if (fitter != null) fitter.enabled = false;
                    var rect = game.lineArtOverlayImage.rectTransform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    rect.localScale = Vector3.one;
                }
                // Repeat migration: no duplicates and no changes to per-artwork alignment.
                ColoringArtworkLayout.Prepare(game.coloringImage, game.coloringImageFrame, false);
                ColoringArtworkLayout.Prepare(game.referenceImage, game.referenceImageFrame, true);
                Check(before == JsonUtility.ToJson(new ArtworkSnapshot { paintings = game.paintings }), "Artwork data changed");
                Check(game.coloringImageFrame.GetComponentsInChildren<RawImage>(true).Count(x => x.name == "Artwork") == 1, "Duplicate artwork");
                Check(game.referenceImage.transform.parent.name == "ReferenceAlignment", "Alignment hierarchy missing");
                Check(game.coloringImage.transform.parent.name == "ImageViewport", "Padding hierarchy missing");
            }
            EditorSceneManager.SaveScene(scene);
            string saved = File.ReadAllText(scene.path);
            string[] artworkData = games.Select(game => JsonUtility.ToJson(new ArtworkSnapshot { paintings = game.paintings })).ToArray();
            SetupColoringWorkshopTool.SetupWorkshop();
            SetupColoringWorkshopTool.SetupWorkshop();
            Check(!scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Any(x => x.name == "Instructions"), "Setup recreated instructions");
            for (int i = 0; i < games.Length; i++)
            {
                Check(artworkData[i] == JsonUtility.ToJson(new ArtworkSnapshot { paintings = games[i].paintings }), "Setup reset artwork alignment");
                Check(games[i].coloringImageFrame.GetComponentsInChildren<RawImage>(true).Count(x => x.name == "Artwork") == 1, "Setup duplicated artwork");
            }
            Check(saved == File.ReadAllText(scene.path), "Setup unexpectedly saved scene");
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        GameObject root = new GameObject("LayoutTestCanvas", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        GameObject frameObject = new GameObject("Frame", typeof(RectTransform));
        RectTransform frame = frameObject.GetComponent<RectTransform>();
        frame.SetParent(root.transform, false);
        ColoringArtworkLayout.Center(frame);
        RawImage image = new GameObject("Artwork", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter)).GetComponent<RawImage>();
        image.transform.SetParent(frame, false);
        var viewport = ColoringArtworkLayout.Prepare(image, frame, false);
        RectTransform refFrame = new GameObject("ReferenceFrame", typeof(RectTransform)).GetComponent<RectTransform>();
        refFrame.SetParent(root.transform, false);
        ColoringArtworkLayout.Center(refFrame);
        RawImage referenceImage = new GameObject("Artwork", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        referenceImage.transform.SetParent(refFrame,false);
        ColoringPageMinigame gameLayout = image.gameObject.AddComponent<ColoringPageMinigame>();
        gameLayout.coloringImage = image;
        gameLayout.coloringImageFrame = frame;
        gameLayout.referenceImage = referenceImage;
        gameLayout.referenceImageFrame = refFrame;
        gameLayout.lineArtOverlayImage = new GameObject("LineArtOverlay",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter)).GetComponent<RawImage>();
        gameLayout.lineArtOverlayImage.transform.SetParent(image.transform,false);
        typeof(ColoringPageMinigame).GetField("selectedPaintingIndex",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(gameLayout,0);
        Camera pointerCamera = new GameObject("PointerCamera",typeof(Camera)).GetComponent<Camera>();
        pointerCamera.transform.position = new Vector3(0,0,-100);
        pointerCamera.orthographic = true;
        pointerCamera.orthographicSize = 1000;
        foreach (Vector2 screen in new[] { new Vector2(1280,720), new Vector2(1920,1080), new Vector2(2560,1080), new Vector2(390,844), new Vector2(844,390) })
        foreach (Vector2 line in new[] { new Vector2(1600,900), new Vector2(900,1600), new Vector2(1000,1000) })
        foreach (float scale in new[] { 0.5f, 1f, 2f })
        {
            // Safe-area-sized frame, then resize the same hierarchy without reselection.
            frame.sizeDelta = new Vector2(screen.x * 0.4f, screen.y * 0.6f);
            refFrame.sizeDelta = frame.sizeDelta;
            Canvas.ForceUpdateCanvases();
            Vector2 size = ColoringArtworkLayout.Fit(line, viewport.rect.size);
            image.rectTransform.sizeDelta = size;
            Check(Mathf.Abs(size.x / size.y - line.x / line.y) < 0.001f, "Distorted line art");
            Check(size.x <= viewport.rect.width + 0.01f && size.y <= viewport.rect.height + 0.01f, "Image escapes padding");
            Vector2 offset = new Vector2(0.2f, -0.15f);
            Rect aligned = ColoringArtworkLayout.ReferenceRect(line, new Vector2(700,1000), viewport.rect.size, scale, offset);
            Texture2D lineTexture = new Texture2D((int)line.x,(int)line.y);
            Texture2D refTexture = new Texture2D(700,1000);
            gameLayout.paintings = new[] { new ColoringArtworkDefinition { lineArt = lineTexture, referenceArt = refTexture, referenceScale = scale, referenceOffset = offset } };
            gameLayout.RefreshArtworkLayout();
            Check(Vector2.Distance(image.rectTransform.sizeDelta,size) < .01f, "Runtime fit differs from preview");
            Check(Vector2.Distance(referenceImage.rectTransform.sizeDelta,aligned.size) < .01f, "Runtime reference scale differs from preview");
            Check(Vector2.Distance(((RectTransform)referenceImage.transform.parent).anchoredPosition,aligned.center) < .01f, "Runtime offset differs from preview");
            Check(Vector2.Distance(gameLayout.lineArtOverlayImage.rectTransform.rect.size,image.rectTransform.rect.size) < .01f, "Line overlay does not match coloring image");
            Check(!gameLayout.lineArtOverlayImage.GetComponent<AspectRatioFitter>().enabled && !gameLayout.lineArtOverlayImage.raycastTarget, "Overlay intercepts input or layout");
            Check(Vector2.Distance(aligned.center, Vector2.Scale(offset,size)) < 0.01f, "Scale moved reference center");
            Check(Mathf.Abs(aligned.width / aligned.height - 0.7f) < 0.001f, "Distorted reference");
            foreach (Vector2 uv in new[] { new Vector2(.5f,.5f), new Vector2(.02f,.02f), new Vector2(.98f,.02f), new Vector2(.02f,.98f), new Vector2(.98f,.98f) })
            {
                Vector2 local = (uv - Vector2.one * .5f) * size;
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, image.rectTransform.TransformPoint(local));
                Check(RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform,point,null,out Vector2 result), "Pointer conversion failed");
                Vector2 restored = new Vector2((result.x-image.rectTransform.rect.xMin)/size.x,(result.y-image.rectTransform.rect.yMin)/size.y);
                Check(Vector2.Distance(uv,restored) < .001f, "Pointer shifted");
                Vector2 cameraPoint = RectTransformUtility.WorldToScreenPoint(pointerCamera,image.rectTransform.TransformPoint(local));
                Check(RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform,cameraPoint,pointerCamera,out Vector2 cameraResult), "Camera pointer failed");
                Check(Vector2.Distance(local,cameraResult) < .02f, "Camera pointer shifted");
            }
            Vector2 outside = RectTransformUtility.WorldToScreenPoint(null,image.rectTransform.TransformPoint(new Vector2(size.x * .6f,0)));
            Check(!RectTransformUtility.RectangleContainsScreenPoint(image.rectTransform,outside,null), "Blank margin accepts click");
            Check(!image.GetComponent<AspectRatioFitter>().enabled, "Fitter still controls artwork");
            UnityEngine.Object.DestroyImmediate(lineTexture);
            UnityEngine.Object.DestroyImmediate(refTexture);
            cases++;
        }
        // Actual coloring handler, five distinct regions: catches wrong pixel selection.
        Texture2D clickTexture = new Texture2D(10,10,TextureFormat.RGBA32,false);
        clickTexture.SetPixels(Enumerable.Repeat(Color.white,100).ToArray());
        clickTexture.Apply();
        int[] targets = { 55, 0, 9, 90, 99 };
        TextAsset regions = new TextAsset(JsonUtility.ToJson(new ColoringRegionDataAsset {
            width=10,height=10,regionCount=5,
            regions=targets.Select((p,i) => new ColoringRegionSpanItem { id=i+1,pixelCount=1,spans=new[] { new ColoringPixelSpan { start=p,length=1 } } }).ToArray()
        }));
        gameLayout.paintings = new[] { new ColoringArtworkDefinition { lineArt=clickTexture,regionData=regions } };
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(ColoringPageMinigame).GetMethod("PreparePaintingList",flags).Invoke(gameLayout,null);
        gameLayout.SelectPainting(0);
        GraphicRaycaster raycaster = root.AddComponent<GraphicRaycaster>();
        foreach (bool cameraPointer in new[] { false,true })
        foreach (Vector2 resized in new[] { new Vector2(700,400),new Vector2(400,700) })
        {
            canvas.renderMode = cameraPointer ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = cameraPointer ? pointerCamera : null;
            frame.sizeDelta = resized;
            Canvas.ForceUpdateCanvases();
            gameLayout.RefreshArtworkLayout();
            gameLayout.ResetPainting();
            gameLayout.palette = new[] { Color.red };
            gameLayout.SelectPaletteIndex(0);
            PointerEventData Pointer(Vector2 local) => new PointerEventData(null) {
                position=RectTransformUtility.WorldToScreenPoint(cameraPointer ? pointerCamera : null,image.rectTransform.TransformPoint(local)),
                pointerPressRaycast=new RaycastResult { module=raycaster }
            };
            gameLayout.OnPointerClick(Pointer(new Vector2(image.rectTransform.rect.xMax + 5,0)));
            Check(((Texture2D)image.texture).GetPixels32().All(c => c.Equals((Color32)Color.white)), "Margin painted a region");
            foreach (int target in targets)
            {
                Vector2 uv = new Vector2((target % 10 + .5f)/10f,(target / 10 + .5f)/10f);
                Vector2 local = image.rectTransform.rect.min + Vector2.Scale(uv,image.rectTransform.rect.size);
                gameLayout.OnPointerClick(Pointer(local));
                Check(((Texture2D)image.texture).GetPixels32()[target].Equals((Color32)Color.red), "Actual coloring pointer hit wrong region");
            }
        }
        UnityEngine.Object.DestroyImmediate(pointerCamera.gameObject);
        UnityEngine.Object.DestroyImmediate(root);
        UnityEngine.Object.DestroyImmediate(clickTexture);
        UnityEngine.Object.DestroyImmediate(regions);
        TestColoringWorkshopSuite.RunAllTests(true);
        File.WriteAllText("Logs/ColoringLayoutResults.txt", "PASS: " + cases + " combinations: 5 viewport sizes x 3 artwork ratios x 3 scales, nonzero offset; runtime/preview parity after resize; center/four corners and margins; overlay and camera pointer conversion.\nActual OnPointerClick: five distinct pixel regions plus blank margin, after landscape/portrait resize, overlay and world-space camera pointers (4 runs).\n2 saved scene migrations and repeated setup: no duplicate artwork or Instructions, artwork alignment preserved.\nColoring regression 5/5. Physical mobile/VR input and rendered device appearance not verified.\n");
    }

    [Serializable] private class ArtworkSnapshot { public ColoringArtworkDefinition[] paintings; }
}
