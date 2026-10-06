using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Creates temporary fixture assets and an unsaved test scene; run only on a project copy.
public static class TestColorBoundaryAuthoring
{
 const string Flag="BAINHOM.ColorBoundaryAuthoring";
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 [Serializable] sealed class ExportRecord {public string line,mask,reference,region;}
 [Serializable] sealed class ExportManifest {public ExportRecord[] items;}
 static readonly List<ExportRecord> exports=new List<ExportRecord>();
 static List<string> rows=new List<string>();static int assertions,stage;static double next;static ColoringPageMinigame game;
 static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
 static void Set(object o,string name,object v)=>o.GetType().GetField(name,Private).SetValue(o,v);
 static void Check(bool value,string message){if(!value)throw new Exception(message);assertions++;rows.Add("PASS: "+message);}
 static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
 static void PNG(string path,Color32[] pixels,int w,int h){var t=new Texture2D(w,h,TextureFormat.RGBA32,false);t.SetPixels32(pixels);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);}
 static int[] Validate(ColoringRegionSpanItem[] regions,Color32[] lines,Color32[] mask,int w,int h)
 {
  var grid=new int[w*h];foreach(var r in regions){int count=0;foreach(var s in r.spans){if(s.start<0||s.length<=0||s.start+s.length>grid.Length||s.start/w!=(s.start+s.length-1)/w)throw new Exception("Invalid span");for(int p=s.start;p<s.start+s.length;p++){if(grid[p]!=0||lines[p].r!=255||mask[p].r!=255)throw new Exception("Span overlap or grid disagreement");grid[p]=r.id;count++;}}if(count!=r.pixelCount)throw new Exception("pixelCount mismatch");}
  for(int p=0;p<grid.Length;p++){
   if((grid[p]>0)!=(mask[p].r==255))throw new Exception("Mask mismatch");int x=p%w,y=p/w;
   if(grid[p]>0&&(x+1<w&&grid[p+1]>0&&grid[p+1]!=grid[p]||y+1<h&&grid[p+w]>0&&grid[p+w]!=grid[p]))throw new Exception("Invisible join between different exported regions");
  }
  Check(true,"Spans, white interiors and mask agree; no invisible joins");return grid;
 }
 public static void RunChecks()
 {
  rows.Clear();assertions=0;const int w=160,h=96;var src=Enumerable.Repeat(new Color32(220,180,80,255),w*h).ToArray();
  for(int y=20;y<76;y++)for(int x=20;x<65;x++)src[y*w+x]=new Color32(20,20,20,255);
  for(int y=20;y<76;y++)for(int x=95;x<140;x++)src[y*w+x]=new Color32(20,20,20,255);
  var a=ColorArtworkSegmentation.Analyze(src,w,h,new ColorArtworkSegmentation.Options());var o=a.BuildOutput(new HashSet<int>(),1);var grid=Validate(o.Regions.ToArray(),o.Lines,o.Mask,w,h);
  Check(!a.Ink.Any(x=>x),"Default colour mode never interprets darkness as ink");Check(o.Lines[40*w+40].r==255&&grid[40*w+40]>0,"Wide black fill has a white paintable interior");
  Check(grid[40*w+40]!=grid[40*w+110]&&grid[40*w+110]>0,"Disconnected black fills export distinct IDs");
  var close=Enumerable.Repeat(new Color32(220,160,100,255),w*h).ToArray();
  for(int y=0;y<h;y++)for(int x=80;x<w;x++)close[y*w+x]=new Color32(220,120,100,255);
  var near=ColorArtworkSegmentation.Analyze(close,w,h,new ColorArtworkSegmentation.Options{MergeDistance=24,KeepEdges=1,Denoise=0});
  Check(near.Labels[48*w+40]!=near.Labels[48*w+120],"A clear edge separates close colours even inside one palette tolerance");
  // Manual separator changes only output geometry, not colour labels or source.
  var labels=(int[])a.Labels.Clone();a.DrawManualBoundary(42,20,42,75,0,false);o=a.BuildOutput(new HashSet<int>(),1);grid=Validate(o.Regions.ToArray(),o.Lines,o.Mask,w,h);
  Check(grid[40*w+30]!=grid[40*w+55]&&grid[40*w+30]>0&&grid[40*w+55]>0,"Hand-drawn separator splits one black colour region into paintable interiors");Check(a.Labels.SequenceEqual(labels),"Drawing does not re-segment colour labels");
  a.DrawManualBoundary(42,20,42,75,0,true);o=a.BuildOutput(new HashSet<int>(),1);grid=Validate(o.Regions.ToArray(),o.Lines,o.Mask,w,h);Check(grid[40*w+30]==grid[40*w+55],"Erasing hand-drawn separator rejoins the original region");
  var small=Enumerable.Repeat(new Color32(230,150,80,255),w*h).ToArray();for(int y=40;y<42;y++)for(int x=60;x<62;x++)small[y*w+x]=new Color32(10,10,10,255);
  a=ColorArtworkSegmentation.Analyze(small,w,h,new ColorArtworkSegmentation.Options{MinimumPixels=200,Denoise=0});o=a.BuildOutput(new HashSet<int>(),1);grid=Validate(o.Regions.ToArray(),o.Lines,o.Mask,w,h);Check(grid[40*w+60]>0,"High-contrast four-pixel dark detail is protected despite min area 200");
  o=a.BuildOutput(new HashSet<int>(),8);Check(o.LostRegions>0&&o.LostRegionIds.Count>0,"Thick outline reports consumed details explicitly");
  using(var window=new Owner()){Check(!Get<bool>(window.Value,"preserveColorInk"),"New window defaults to colour boundaries");for(int preset=0;preset<3;preset++){window.Value.ApplyColorPreset(preset);Check(!Get<bool>(window.Value,"preserveColorInk"),"Preset leaves native ink opt-in: "+preset);}}
  // Native-ink opt-in still needs drawn boundaries at nearby colour changes.
  var ink=Enumerable.Repeat(new Color32(235,180,90,255),w*h).ToArray();for(int y=0;y<h;y++)for(int x=80;x<w;x++)ink[y*w+x]=new Color32(80,150,220,255);ink[48*w+79]=new Color32(0,0,0,255);
  a=ColorArtworkSegmentation.Analyze(ink,w,h,new ColorArtworkSegmentation.Options{PreserveInk=true,MinimumPixels=1,Denoise=0,MergeDistance=2});o=a.BuildOutput(new HashSet<int>(),1);Validate(o.Regions.ToArray(),o.Lines,o.Mask,w,h);Check(o.Lines[48*w+79].r==0,"Native ink is preserved only when explicitly enabled");
  Directory.CreateDirectory("Logs/ColorBoundary");File.WriteAllLines("Logs/ColorBoundary/Checks.txt",rows);
 }
 public static void Begin()
 {
  try{RunChecks();TestGameplayRegressionSuite.RunAllTests();TestColoringWorkshopSuite.RunAllTests(true);
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);var canvas=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
   var image=new GameObject("Paint",typeof(RectTransform),typeof(RawImage));image.transform.SetParent(canvas.transform,false);var rect=(RectTransform)image.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(600,420);game=image.AddComponent<ColoringPageMinigame>();game.coloringImage=image.GetComponent<RawImage>();game.paintings=Array.Empty<ColoringArtworkDefinition>();new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
   string folder=AssetDatabase.GenerateUniqueAssetPath("Assets/ColorBoundaryFixture");AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
   const int fw=256,fh=160;var flat=new Color32[fw*fh];for(int y=25;y<135;y++)for(int x=20;x<70;x++)flat[y*fw+x]=new Color32(20,20,20,255);for(int y=25;y<135;y++)for(int x=170;x<220;x++)flat[y*fw+x]=new Color32(20,20,20,255);for(int y=25;y<135;y++)for(int x=95;x<145;x++)flat[y*fw+x]=new Color32(210,70,50,255);string fixture=folder+"/BlackFill.png";PNG(fixture,flat,fw,fh);AssetDatabase.ImportAsset(fixture,ImportAssetOptions.ForceSynchronousImport);
   var ti=(TextureImporter)AssetImporter.GetAtPath(fixture);ti.npotScale=TextureImporterNPOTScale.None;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.mipmapEnabled=false;ti.SaveAndReimport();
   string[] sources={fixture,"Assets/TRAnh/Boi-canh-ra-doi-cua-Tranh-Dong-Ho-Thay-Do-Coc.png","Assets/TRAnh/Chăn trâu thổi sáo.jpg","Assets/TRAnh/Lợn đàn.jpg","Assets/TRAnh/Bà Triệu cưỡi voi .jpg","Assets/TRAnh/Hai Bà Trưng cưỡi voi .jpg","Assets/TRAnh/tranh-dam-cuoi-chuot-c.jpg","Assets/TRAnh/tranhchoitrau.jpg"};
   for(int n=0;n<sources.Length;n++){
    string path=sources[n],hash=Hash(path),meta=Hash(path+".meta");using(var window=new Owner()){
     Set(window.Value,"workshop",game);Set(window.Value,"sourceMode",1);Set(window.Value,"artworkName","Boundary-"+n);Set(window.Value,"originalImage",AssetDatabase.LoadAssetAtPath<Texture2D>(path));window.Value.ApplyColorPreset(n==0?0:n<=4?2:1);
     Check(Get<string>(window.Value,"colorAnalysisError")==null,"Tool analyses original: "+path);
     if(n==0){window.Value.EditColorBoundary(new Vector2Int(45,25),new Vector2Int(45,134),0,false);Check(Get<int>(window.Value,"fillableRegionCount")==4,"Real preview manual pen splits the black fill");}
     else{var analysis=Get<ColorArtworkSegmentation>(window.Value,"colorAnalysis");int[] borders={0,analysis.Width-1,(analysis.Height-1)*analysis.Width,analysis.Width*analysis.Height-1};var seen=new HashSet<int>();foreach(int p in borders){int id=analysis.Labels[p];if(id>0&&seen.Add(id))window.Value.EditColorRegionAt(p%analysis.Width,p/analysis.Width,1);}}
     int lost=Get<int>(window.Value,"lostColorRegions");rows.Add("PREVIEW: "+path+"; lost="+lost+"; preset="+(n==0?"Flat":n<=4?"Photo":"Texture")+"; nativeInk=false");
     if(lost>0){Check(!window.Value.TryAddArtworkToWorkshop(out _,out _),"Lost detail blocks export before explicit review edit");window.Value.ExcludeUnpaintableColorRegions();rows.Add("REVIEW EDIT: explicitly excluded "+lost+" consumed labels; no automatic silent discard");}
     var line=Get<Texture2D>(window.Value,"previewLineArt");int previewWidth=line.width,previewHeight=line.height;var lp=line.GetPixels32();var referencePixels=(Color32[])Get<Color32[]>(window.Value,"colorReferencePixels").Clone();var mp=Get<Texture2D>(window.Value,"previewMask").GetPixels32();var spans=Get<List<ColoringRegionSpanItem>>(window.Value,"calculatedRegions").ToArray();Validate(spans,lp,mp,line.width,line.height);
     Directory.CreateDirectory("Logs/ColorBoundary/Preview"+n);PNG("Logs/ColorBoundary/Preview"+n+"/Lines.png",lp,line.width,line.height);PNG("Logs/ColorBoundary/Preview"+n+"/Regions.png",Get<Texture2D>(window.Value,"previewRegionOverlay").GetPixels32(),line.width,line.height);PNG("Logs/ColorBoundary/Preview"+n+"/Reference.png",Get<Color32[]>(window.Value,"colorReferencePixels"),line.width,line.height);
     Check(window.Value.TryAddArtworkToWorkshop(out var art,out string error),"Real export succeeds: "+error);Check(art.lineArt.GetPixels32().SequenceEqual(lp)&&art.paintMask.GetPixels32().SequenceEqual(mp),"Imported line/mask match preview per pixel");var data=JsonUtility.FromJson<ColoringRegionDataAsset>(art.regionData.text);Check(data.width==art.referenceArt.width&&data.height==art.referenceArt.height&&data.width==art.lineArt.width&&data.height==art.paintMask.height,"Line/reference/mask/JSON retain one grid");Check(JsonUtility.ToJson(data)==JsonUtility.ToJson(new ColoringRegionDataAsset{width=previewWidth,height=previewHeight,regionCount=spans.Length,regions=spans}),"Exported span IDs/counts/coordinates match preview");
     Check(art.referenceArt.GetPixels32().SequenceEqual(referencePixels),"Reference preserves original colour pixels");Check(Hash(path)==hash&&Hash(path+".meta")==meta,"Source and source importer preserved");exports.Add(new ExportRecord{line=AssetDatabase.GetAssetPath(art.lineArt),mask=AssetDatabase.GetAssetPath(art.paintMask),reference=AssetDatabase.GetAssetPath(art.referenceArt),region=AssetDatabase.GetAssetPath(art.regionData)});
    }
   }
   File.WriteAllText("Logs/ColorBoundary/GeneratedAssets.json",JsonUtility.ToJson(new ExportManifest{items=exports.ToArray()},true));SessionState.SetString(Flag+".rows",string.Join("\n",rows));SessionState.SetInt(Flag+".assertions",assertions);SessionState.SetBool(Flag,true);Resume();EditorApplication.EnterPlaymode();
  }catch(Exception e){Finish(e);}
 }
 [InitializeOnLoadMethod]static void Resume(){if(SessionState.GetBool(Flag,false)){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}}
 static void Tick()
 {
  if(!EditorApplication.isPlaying)return;if(next==0){next=EditorApplication.timeSinceStartup+1;rows=new List<string>(SessionState.GetString(Flag+".rows","").Split('\n'));assertions=SessionState.GetInt(Flag+".assertions",0);game=UnityEngine.Object.FindAnyObjectByType<ColoringPageMinigame>();return;}if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.2;
  try{if(stage==game.paintings.Length){Finish(null);return;}game.SelectPainting(stage);game.ResetPainting();var art=game.paintings[stage];var regions=Get<ColoringRegionSpanItem[]>(game,"cachedRegionSpans");Check(regions!=null,"Runtime accepts generated cache without fallback");var before=((Texture2D)game.coloringImage.texture).GetPixels32();var reference=art.referenceArt.GetPixels32();
   var target=regions.OrderByDescending(r=>r.pixelCount).FirstOrDefault(r=>r.spans.Any(s=>Enumerable.Range(s.start,s.length).Any(p=>Math.Max(reference[p].r,Math.Max(reference[p].g,reference[p].b))<110)));Check(target.pixelCount>0,"Original dark fill has a paintable component: "+stage);
   if(stage==0){int left=80*art.lineArt.width+32;target=regions.First(r=>r.spans.Any(s=>left>=s.start&&left<s.start+s.length));}
   int pixel=target.spans.SelectMany(s=>Enumerable.Range(s.start,s.length)).First(p=>Math.Max(reference[p].r,Math.Max(reference[p].g,reference[p].b))<110);Check(before[pixel].r==255,"Chosen originally dark pixel is white before paint");game.SelectPaletteIndex(1);Canvas.ForceUpdateCanvases();var rect=game.coloringImage.rectTransform;int w=art.lineArt.width,h=art.lineArt.height;float u=(pixel%w+.5f)/w,v=(pixel/w+.5f)/h;var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(new Vector3(rect.rect.xMin+u*rect.rect.width,rect.rect.yMin+v*rect.rect.height)))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0&&hits[0].gameObject==game.gameObject,"UI raycast reaches dark-fill interior");pointer.pointerPressRaycast=hits[0];ExecuteEvents.Execute(game.gameObject,pointer,ExecuteEvents.pointerClickHandler);
   var after=((Texture2D)game.coloringImage.texture).GetPixels32();var chosen=new bool[w*h];foreach(var s in target.spans)for(int p=s.start;p<s.start+s.length;p++)chosen[p]=true;for(int p=0;p<after.Length;p++)if(!after[p].Equals(chosen[p]?(Color32)game.palette[1]:before[p]))throw new Exception("Painting escaped selected component");Check(Get<int>(game,"paintedRegionCount")==1,"Only chosen region painted; disconnected same-colour region unchanged");game.SelectPainting((stage+1)%game.paintings.Length);game.SelectPainting(stage);Check(Get<int>(game,"paintedRegionCount")==1&&((Texture2D)game.coloringImage.texture).GetPixels32().SequenceEqual(after),"Switch preserves exact colour/progress");game.gameObject.SetActive(false);game.gameObject.SetActive(true);game.SelectPainting(stage);Check(Get<int>(game,"paintedRegionCount")==1&&((Texture2D)game.coloringImage.texture).GetPixels32().SequenceEqual(after),"Close/reopen preserves colour/progress");rows.Add("PAINT: stage="+stage+"; pixel="+pixel+"; region="+target.id+"; regionPixels="+target.pixelCount+"; grid="+w+"x"+h);stage++;
  }catch(Exception e){Finish(e);}
 }
 static void Finish(Exception e){EditorApplication.update-=Tick;SessionState.SetBool(Flag,false);Directory.CreateDirectory("Logs/ColorBoundary");rows.Add(e==null?"RESULT: PASS; "+assertions+" assertions; 7 selected originals + dark/manual fixture; simulated pointer; unsaved test scene":"RESULT: FAIL\n"+e);File.WriteAllLines("Logs/ColorBoundary/Results.txt",rows);if(e!=null)Debug.LogException(e);EditorApplication.Exit(e==null?0:1);}
 public static void AuditGeneratedAndExit()
 {
  try{rows.Clear();assertions=0;var manifest=JsonUtility.FromJson<ExportManifest>(File.ReadAllText("Logs/ColorBoundary/GeneratedAssets.json"));foreach(var item in manifest.items){
   var line=AssetDatabase.LoadAssetAtPath<Texture2D>(item.line);var mask=AssetDatabase.LoadAssetAtPath<Texture2D>(item.mask);var reference=AssetDatabase.LoadAssetAtPath<Texture2D>(item.reference);var data=JsonUtility.FromJson<ColoringRegionDataAsset>(AssetDatabase.LoadAssetAtPath<TextAsset>(item.region).text);
   Check(line.width==data.width&&line.height==data.height&&mask.width==data.width&&mask.height==data.height&&reference.width==data.width&&reference.height==data.height,"Reimport keeps line/mask/reference/JSON grid: "+item.line);Validate(data.regions,line.GetPixels32(),mask.GetPixels32(),data.width,data.height);
   object[] args={data,data.width,data.height,null,null};Check((bool)typeof(ColoringPageMinigame).GetMethod("TryBuildCachedRegions",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args),"Reimported span cache accepted");
   foreach(string path in new[]{item.line,item.mask,item.reference}){var importer=(TextureImporter)AssetImporter.GetAtPath(path);Check(importer.npotScale==TextureImporterNPOTScale.None&&!importer.mipmapEnabled&&importer.textureCompression==TextureImporterCompression.Uncompressed,"Generated importer keeps grid");foreach(string platform in new[]{"Standalone","Android","iPhone","WebGL"})Check(!importer.GetPlatformTextureSettings(platform).overridden,"Generated platform override absent: "+platform);}
  }rows.Add("RESULT: PASS; "+EditorUserBuildSettings.activeBuildTarget+"; "+manifest.items.Length+" previously exported assets; "+assertions+" assertions");File.WriteAllLines("Logs/ColorBoundary/ReimportResults.txt",rows);EditorApplication.Exit(0);
  }catch(Exception e){Directory.CreateDirectory("Logs/ColorBoundary");File.WriteAllText("Logs/ColorBoundary/ReimportFailure.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
 }
 sealed class Owner:IDisposable{internal readonly AddColoringArtworkWindow Value=ScriptableObject.CreateInstance<AddColoringArtworkWindow>();public void Dispose()=>UnityEngine.Object.DestroyImmediate(Value);}
}
