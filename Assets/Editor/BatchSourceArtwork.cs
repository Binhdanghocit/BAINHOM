using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Explicit inventory -> previews -> reviewed allow-list. Never silently excludes labels.
public static class BatchSourceArtwork
{
    public const string ManifestRoot = "Assets/Editor/ArtworkBatchData";
    public const string OutputRoot = "Logs/ArtworkBatch";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] public sealed class Item {
        public string key,path,preset,status,reason,sha256,metaHash,pixelHash,duplicateOf;
        public float lab,denoise,keepEdges; public int minimum,thickness,maxSide,width,height,regions,lost,small;
        public bool ink; public string line,mask,reference,region;
    }
    [Serializable] public sealed class Plan { public Item[] items; }
    public static Plan ReadManifest(string name) => ReadPlan(ManifestRoot+"/"+name);
    static Plan ReadPlan(string path) {
        if(!File.Exists(path))throw new FileNotFoundException("Missing artwork manifest: "+path,path);
        var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(path));
        if(plan==null||plan.items==null||plan.items.Length==0)throw new InvalidDataException("Empty artwork manifest: "+path);
        return plan;
    }
    static void Set(object o,string name,object value) => o.GetType().GetField(name,Private).SetValue(o,value);
    static Texture2D Texture(Color32[] p,int w,int h) {var t=new Texture2D(w,h,TextureFormat.RGBA32,false);t.SetPixels32(p);t.Apply();return t;}
    static void PNG(string path,Color32[] p,int w,int h) {var t=Texture(p,w,h);File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);}
    static Color32[] Pixels(Item item,out int w,out int h) {
        var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(item.path));
        float scale=Math.Min(1f,(float)item.maxSide/Math.Max(t.width,t.height));w=Math.Max(1,Mathf.RoundToInt(t.width*scale));h=Math.Max(1,Mathf.RoundToInt(t.height*scale));
        Color32[] p;
        if(w==t.width&&h==t.height)p=t.GetPixels32();
        else {p=new Color32[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)p[y*w+x]=t.GetPixelBilinear((x+.5f)/w,(y+.5f)/h);}
        UnityEngine.Object.DestroyImmediate(t);return p;
    }
    static ColorArtworkSegmentation.Output Analyze(Item item,out Color32[] p,out ColorArtworkSegmentation a) {
        p=Pixels(item,out int w,out int h);item.width=w;item.height=h;
        if(item.preset=="Line-source") {
            a=null;var window=ScriptableObject.CreateInstance<AddColoringArtworkWindow>();var source=Texture(p,w,h);
            try {
                Set(window,"originalImage",source);Set(window,"lineThreshold",180);Set(window,"gapClosureRadius",0);Set(window,"minimumRegionPixels",1);Set(window,"smoothLines",false);
                typeof(AddColoringArtworkWindow).GetMethod("ReanalyzeArtwork",Private).Invoke(window,null);
                var type=typeof(AddColoringArtworkWindow);
                var result=new ColorArtworkSegmentation.Output {
                    Lines=((Texture2D)type.GetField("previewLineArt",Private).GetValue(window)).GetPixels32(),
                    Mask=((Texture2D)type.GetField("previewMask",Private).GetValue(window)).GetPixels32(),
                    Overlay=((Texture2D)type.GetField("previewRegionOverlay",Private).GetValue(window)).GetPixels32(),
                    Regions=new List<ColoringRegionSpanItem>((List<ColoringRegionSpanItem>)type.GetField("calculatedRegions",Private).GetValue(window))};
                item.regions=result.Regions.Count;item.lost=0;item.small=result.Regions.Count(r=>r.pixelCount<20);return result;
            }finally{UnityEngine.Object.DestroyImmediate(window);UnityEngine.Object.DestroyImmediate(source);}
        }
        a=ColorArtworkSegmentation.Analyze(p,w,h,new ColorArtworkSegmentation.Options {
            MergeDistance=item.lab,MinimumPixels=item.minimum,Denoise=item.denoise,KeepEdges=item.keepEdges,
            PreserveInk=item.ink,SmoothBoundaries=true,TimeLimitSeconds=60 });
        var o=a.BuildOutput(new HashSet<int>(),1);item.regions=o.Regions.Count;item.lost=o.LostRegions;
        item.small=o.Regions.Count(r=>r.pixelCount<20);return o;
    }
    public static void Preview() {
        Directory.CreateDirectory(OutputRoot);
        try {
            var plan=ReadManifest("Inventory.json");
            foreach(var item in plan.items) {
                if(item.status=="duplicate")continue;
                string folder=OutputRoot+"/"+item.key;Directory.CreateDirectory(folder);
                try {
                    var o=Analyze(item,out var p,out _);
                    PNG(folder+"/Reference.png",p,item.width,item.height);PNG(folder+"/Lines.png",o.Lines,item.width,item.height);PNG(folder+"/Regions.png",o.Overlay,item.width,item.height);
                    item.reason=item.lost>0?"Có "+item.lost+" vùng mất interior; cần chỉnh hoặc chưa xuất":"Chờ kiểm hình chính và vùng nối/vụn";
                } catch(Exception e) {item.reason=e.Message;item.lost=-1;}
                File.WriteAllText(OutputRoot+"/Previews.json",JsonUtility.ToJson(plan,true));Debug.Log("BATCH PREVIEW "+item.key+" "+item.regions+" lost="+item.lost);
            }
            EditorApplication.Exit(0);
        }catch(Exception e){File.WriteAllText(OutputRoot+"/Failure.txt",e.ToString());EditorApplication.Exit(1);}
    }
    public static void Retry() {
        Directory.CreateDirectory(OutputRoot);
        try {
            var plan=ReadPlan(OutputRoot+"/Previews.json");var retry=ReadManifest("Retry.json");
            foreach(var item in retry.items) {
                string folder=OutputRoot+"/"+item.key;Directory.CreateDirectory(folder+"/First");
                foreach(string name in new[]{"Reference","Lines","Regions"})if(File.Exists(folder+"/"+name+".png"))File.Copy(folder+"/"+name+".png",folder+"/First/"+name+".png",true);
                try{var o=Analyze(item,out var p,out _);PNG(folder+"/Reference.png",p,item.width,item.height);PNG(folder+"/Lines.png",o.Lines,item.width,item.height);PNG(folder+"/Regions.png",o.Overlay,item.width,item.height);item.reason="Kiểm lại preset riêng; lost="+item.lost;}
                catch(Exception e){item.reason=e.Message;item.lost=-1;}
                plan.items[Array.FindIndex(plan.items,i=>i.key==item.key)]=item;
                File.WriteAllText(OutputRoot+"/Previews.json",JsonUtility.ToJson(plan,true));Debug.Log("BATCH RETRY "+item.key+" "+item.regions+" lost="+item.lost);
            }EditorApplication.Exit(0);
        }catch(Exception e){File.WriteAllText(OutputRoot+"/RetryFailure.txt",e.ToString());EditorApplication.Exit(1);}
    }
    static void Folder(string path) {
        if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    public static void Export() {
        Directory.CreateDirectory(OutputRoot);
        try {
            var plan=ReadManifest("Reviewed.json");Folder("Assets/ColoringArtworks/Generated");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);var go=new GameObject("BatchExport");var game=go.AddComponent<ColoringPageMinigame>();game.paintings=Array.Empty<ColoringArtworkDefinition>();
            foreach(var item in plan.items.Where(i=>i.status=="approved")) {
                string folder="Assets/ColoringArtworks/Generated/"+item.key;Folder(folder);
                string manifest=folder+"/Source.json";
                if(File.Exists(manifest)) {var previous=JsonUtility.FromJson<Item>(File.ReadAllText(manifest));if(previous.sha256!=item.sha256)throw new Exception("Source changed: "+item.path);item.line=previous.line;item.mask=previous.mask;item.reference=previous.reference;item.region=previous.region;continue;}
                var o=Analyze(item,out var p,out var a);if(o.LostRegions!=0)throw new Exception("Lost interiors: "+item.key);
                // Separate imported copy is required by the reviewed export API; never edit source importer.
                string copy=folder+"/SourceCopy.png";PNG(copy,p,item.width,item.height);AssetDatabase.ImportAsset(copy,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(copy);importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.isReadable=true;importer.SaveAndReimport();
                var window=ScriptableObject.CreateInstance<AddColoringArtworkWindow>();var line=Texture(o.Lines,item.width,item.height);var mask=Texture(o.Mask,item.width,item.height);
                try {
                    window.outputFolder=folder;Set(window,"workshop",game);Set(window,"sourceMode",a==null?0:1);Set(window,"artworkName",Path.GetFileNameWithoutExtension(item.path).Trim());Set(window,"originalImage",AssetDatabase.LoadAssetAtPath<Texture2D>(copy));Set(window,"colorAnalysis",a);Set(window,"colorReferencePixels",p);Set(window,"calculatedRegions",o.Regions);Set(window,"previewLineArt",line);Set(window,"previewMask",mask);Set(window,"minimumRegionPixels",item.minimum);
                    if(a==null){string reference=folder+"/Reference.png";AssetDatabase.CopyAsset(copy,reference);Set(window,"referenceArt",AssetDatabase.LoadAssetAtPath<Texture2D>(reference));Set(window,"lineThreshold",180);}
                    if(!window.TryAddArtworkToWorkshop(out var added,out string error))throw new Exception(error);
                    item.line=AssetDatabase.GetAssetPath(added.lineArt);item.mask=AssetDatabase.GetAssetPath(added.paintMask);item.reference=AssetDatabase.GetAssetPath(added.referenceArt);item.region=AssetDatabase.GetAssetPath(added.regionData);
                    File.WriteAllText(manifest,JsonUtility.ToJson(item,true));AssetDatabase.ImportAsset(manifest);
                }finally{UnityEngine.Object.DestroyImmediate(window);}
                AssetDatabase.DeleteAsset(copy);
            }
            foreach(string name in new[]{"MainMenu","SampleScene"}) {
                scene=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");
                foreach(var workshop in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ColoringPageMinigame>(true))) {
                    var list=workshop.paintings.ToList();
                    foreach(var item in plan.items.Where(i=>i.status=="approved")) {
                        if(list.Any(a=>AssetDatabase.GetAssetPath(a.lineArt)==item.line))continue;
                        list.Add(new ColoringArtworkDefinition {title=Path.GetFileNameWithoutExtension(item.path).Trim(),lineArt=AssetDatabase.LoadAssetAtPath<Texture2D>(item.line),paintMask=AssetDatabase.LoadAssetAtPath<Texture2D>(item.mask),referenceArt=AssetDatabase.LoadAssetAtPath<Texture2D>(item.reference),regionData=AssetDatabase.LoadAssetAtPath<TextAsset>(item.region),referenceScale=1,minimumRegionPixels=item.minimum,whiteThreshold=180,regions=Array.Empty<ColoringRegionSeed>()});
                    }
                    workshop.paintings=list.ToArray();EditorUtility.SetDirty(workshop);
                }
                EditorSceneManager.SaveScene(scene);
            }
            File.WriteAllText(OutputRoot+"/Exported.json",JsonUtility.ToJson(plan,true));AssetDatabase.SaveAssets();EditorApplication.Exit(0);
        }catch(Exception e){File.WriteAllText(OutputRoot+"/ExportFailure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
