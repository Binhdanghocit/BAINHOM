using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Run only in the isolated validation copy. Scene edits remain unsaved.
public static class TestAllSourceArtworks
{
    const string Flag="AllSources.PlayAudit";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static ColoringPageMinigame game;static List<string> rows=new List<string>();static int stage;static double next;
    static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,Private).GetValue(o);
    static void Check(bool v,string m){if(!v)throw new Exception(m);rows.Add("PASS "+m);}
    static void Audit(BatchSourceArtwork.Item item) {
        var line=AssetDatabase.LoadAssetAtPath<Texture2D>(item.line);var mask=AssetDatabase.LoadAssetAtPath<Texture2D>(item.mask);var reference=AssetDatabase.LoadAssetAtPath<Texture2D>(item.reference);
        var data=JsonUtility.FromJson<ColoringRegionDataAsset>(AssetDatabase.LoadAssetAtPath<TextAsset>(item.region).text);
        Check(line.width==data.width&&line.height==data.height&&mask.width==data.width&&mask.height==data.height&&reference.width==data.width&&reference.height==data.height,item.key+" shared grid");
        var lp=line.GetPixels32();var mp=mask.GetPixels32();var seen=new bool[lp.Length];
        foreach(var r in data.regions){int count=0;Check(r.pixelCount>0&&r.spans.Length>0,item.key+" nonempty interior "+r.id);foreach(var s in r.spans){if(s.start<0||s.length<1||s.start+s.length>seen.Length||s.start/data.width!=(s.start+s.length-1)/data.width)throw new Exception("Invalid row span "+item.key);for(int p=s.start;p<s.start+s.length;p++){if(seen[p]||lp[p].r!=255||mp[p].r!=255)throw new Exception("Span/grid mismatch "+item.key);seen[p]=true;count++;}}if(count!=r.pixelCount)throw new Exception("Count mismatch");}
        for(int p=0;p<seen.Length;p++)if(seen[p]!=(mp[p].r==255))throw new Exception("Mask mismatch "+item.key);
        object[] args={data,data.width,data.height,null,null};Check((bool)typeof(ColoringPageMinigame).GetMethod("TryBuildCachedRegions",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args),item.key+" cache accepted");
    }
    public static void BeginMain()=>Begin("MainMenu");
    public static void BeginSample()=>Begin("SampleScene");
    static void Begin(string sceneName) {
        Directory.CreateDirectory(BatchSourceArtwork.OutputRoot);
        try {
            rows.Clear();SessionState.SetString(Flag+".scene",sceneName);
            var plan=BatchSourceArtwork.ReadManifest("Exported.json");
            Check(plan.items.Any(i=>i.status=="approved"),"Committed manifest contains approved artworks");
            foreach(var i in plan.items.Where(i=>i.status=="approved"))Audit(i);
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+sceneName+".unity");game=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ColoringPageMinigame>(true)).Single();
            var keep=game.transform.root.gameObject;
            foreach(var r in scene.GetRootGameObjects())if(r!=keep&&r.GetComponentInChildren<EventSystem>(true)==null)UnityEngine.Object.DestroyImmediate(r);
            if(UnityEngine.Object.FindAnyObjectByType<EventSystem>()==null)new GameObject("EventSystem",typeof(EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            game.workshopPanel.SetActive(true);SessionState.SetString(Flag+".scene",sceneName);SessionState.SetString(Flag+".rows",string.Join("\n",rows));SessionState.SetBool(Flag,true);EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }catch(Exception e){Finish(e);}
    }
    [InitializeOnLoadMethod]static void Resume(){if(SessionState.GetBool(Flag,false)){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}}
    static void Tick() {
        if(!EditorApplication.isPlaying)return;
        if(next==0){next=EditorApplication.timeSinceStartup+1;rows=SessionState.GetString(Flag+".rows","").Split('\n').ToList();game=UnityEngine.Object.FindAnyObjectByType<ColoringPageMinigame>();return;}
        if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.2;
        try {
            if(stage==0)ListAudit();
            while(stage<game.paintings.Length&&!AssetDatabase.GetAssetPath(game.paintings[stage].lineArt).StartsWith("Assets/ColoringArtworks/Generated/"))stage++;
            if(stage==game.paintings.Length){Finish(null);return;}
            game.SelectPainting(stage);game.ResetPainting();var spans=Get<ColoringRegionSpanItem[]>(game,"cachedRegionSpans");Check(spans!=null,"Play Mode no cache fallback: "+stage);
            var reference=game.paintings[stage].referenceArt.GetPixels32();
            var dark=spans.Where(r=>r.spans.Any(s=>Enumerable.Range(s.start,s.length).Any(p=>Math.Max(reference[p].r,Math.Max(reference[p].g,reference[p].b))<110))).OrderByDescending(r=>r.pixelCount).ToArray();
            var target=dark.Length>0?dark[0]:spans.OrderByDescending(r=>r.pixelCount).First();var s=target.spans.OrderByDescending(a=>a.length).First();int pixel=s.start+s.length/2;
            if(dark.Length>0)pixel=target.spans.SelectMany(a=>Enumerable.Range(a.start,a.length)).First(p=>Math.Max(reference[p].r,Math.Max(reference[p].g,reference[p].b))<110);
            int w=game.paintings[stage].lineArt.width,h=game.paintings[stage].lineArt.height;
            var before=((Texture2D)game.coloringImage.texture).GetPixels32();game.SelectPaletteIndex(1);Canvas.ForceUpdateCanvases();
            var rect=game.coloringImage.rectTransform;float u=(pixel%w+.5f)/w,v=(pixel/w+.5f)/h;
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(new Vector3(rect.rect.xMin+u*rect.rect.width,rect.rect.yMin+v*rect.rect.height)))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0&&hits[0].gameObject==game.gameObject,"Canvas raycast reaches artwork: "+stage);pointer.pointerPressRaycast=hits[0];ExecuteEvents.Execute(game.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            var after=((Texture2D)game.coloringImage.texture).GetPixels32();var chosen=new bool[w*h];foreach(var span in target.spans)for(int p=span.start;p<span.start+span.length;p++)chosen[p]=true;
            for(int p=0;p<after.Length;p++)if(!after[p].Equals(chosen[p]?(Color32)game.palette[1]:before[p]))throw new Exception("Fill escaped component "+stage);
            Check(Get<int>(game,"paintedRegionCount")==1,"One region filled: "+stage);
            game.SelectPainting((stage+1)%game.paintings.Length);game.SelectPainting(stage);Check(((Texture2D)game.coloringImage.texture).GetPixels32().SequenceEqual(after),"Switch preserves progress: "+stage);
            game.CloseWorkshop();game.workshopPanel.SetActive(true);game.SelectPainting(stage);Check(Get<int>(game,"paintedRegionCount")==1&&((Texture2D)game.coloringImage.texture).GetPixels32().SequenceEqual(after),"Close/reopen preserves exact progress: "+stage);
            stage++;
        }catch(Exception e){Finish(e);}
    }
    static void ListAudit() {
        var buttons=Get<List<Button>>(game,"generatedPaintingButtons");Check(buttons.Count==game.paintings.Length,"Selector contains every artwork");
        var scroll=game.paintingSelectorPanel.GetComponent<ScrollRect>();Check(scroll!=null&&(scroll.vertical||scroll.horizontal),"Selector scroll configured on existing axis");
        var safe=game.safeAreaRect;var min=safe.anchorMin;var max=safe.anchorMax;var size=safe.sizeDelta;
        foreach(var dimensions in new[]{new Vector2(1600,900),new Vector2(900,1600)}) {
            safe.anchorMin=safe.anchorMax=new Vector2(.5f,.5f);safe.sizeDelta=dimensions;game.TogglePaintingSelector();Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);Canvas.ForceUpdateCanvases();
            Check(scroll.horizontal?scroll.content.rect.width>scroll.viewport.rect.width:scroll.content.rect.height>scroll.viewport.rect.height,"List overflows viewport at "+dimensions);
            if(scroll.horizontal)scroll.horizontalNormalizedPosition=1;else scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
            var viewportBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,scroll.viewport);var lastBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,buttons.Last().transform);
            Check(scroll.horizontal?lastBounds.center.x>=viewportBounds.min.x&&lastBounds.center.x<=viewportBounds.max.x:lastBounds.center.y>=viewportBounds.min.y&&lastBounds.center.y<=viewportBounds.max.y,"Last choice scrolls into view at "+dimensions);
            buttons.Last().onClick.Invoke();Check(Get<int>(game,"selectedPaintingIndex")==game.paintings.Length-1,"Last choice selectable at "+dimensions);
        }
        safe.anchorMin=min;safe.anchorMax=max;safe.sizeDelta=size;game.ClosePaintingSelector();Canvas.ForceUpdateCanvases();
    }
    static void Finish(Exception e) {
        EditorApplication.update-=Tick;SessionState.SetBool(Flag,false);string scene=SessionState.GetString(Flag+".scene","Unknown");rows.Add(e==null?"RESULT PASS":"RESULT FAIL "+e);Directory.CreateDirectory(BatchSourceArtwork.OutputRoot);File.WriteAllLines(BatchSourceArtwork.OutputRoot+"/Play-"+scene+".txt",rows);if(e!=null)Debug.LogException(e);EditorApplication.Exit(e==null?0:1);
    }
}
