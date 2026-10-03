using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TestDoorMenuFogSuite
{
    private const string Flag = "BAINHOM.DoorFogTest";
    private static readonly List<string> results = new List<string>();
    private static readonly string[] names = { "Btn_Minigame", "Btn_O_Lai", "Btn_Thoat" };
    private static readonly string[] texts = { "Vào workshop tô màu", "Tiếp tục tham quan", "Về menu chính" };
    private static readonly string[] methods = { "OpenMinigame", "StayInGallery", "GoToMainMenu" };
    private static int assertions, stage;
    private static double nextStep, started;
    private static Camera renderCamera;
    private static DoorMenuTrigger door;
    private static int callbackCount;

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        assertions++; results.Add("PASS: " + message);
    }
    private static DoorMenuTrigger MainDoor() => UnityEngine.Object.FindObjectsByType<DoorMenuTrigger>(FindObjectsInactive.Include).First(d=>d.gameObject.name == "cua");

    public static void SetupAndExport()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            Check(!RenderSettings.fog, "Saved gallery fog is disabled");
            Check(SetupDoorMenuTool.SetupDoorMenuInScene(), "First setup completes");
            var d = MainDoor();
            var panel = d.doorMenuUI;
            var instances = new HashSet<Transform>(panel.GetComponentsInChildren<Transform>(true));
            for (int pass=0;pass<3;pass++)
            {
                Check(SetupDoorMenuTool.SetupDoorMenuInScene(), "Repeated setup completes " + pass);
                Check(!panel.activeSelf, "Setup leaves menu closed");
                Check(instances.SetEquals(panel.GetComponentsInChildren<Transform>(true)), "Setup preserves hierarchy identity");
                Check(panel.GetComponents<DoorMenuLayout>().Length == 1, "One responsive layout component");
                Check(panel.GetComponentsInChildren<Button>(true).Length == 3, "Exactly three buttons");
                foreach(var label in panel.GetComponentsInChildren<TMP_Text>(true))
                {
                    Check(label.font != null && label.font.HasCharacters(label.text, out uint[] missing, true, true), "Vietnamese glyph coverage: " + label.text);
                    Check(label.font.atlasPopulationMode == AtlasPopulationMode.Dynamic, "Font includes source for dynamic Vietnamese glyphs");
                }
                for(int i=0;i<3;i++)
                {
                    var b=panel.transform.Find(names[i]).GetComponent<Button>();
                    Check(b.onClick.GetPersistentEventCount()==1 && b.onClick.GetPersistentMethodName(0)==methods[i] && b.onClick.GetPersistentTarget(0)==d, "Single correct persistent callback " + names[i]);
                    Check(b.GetComponentInChildren<TMP_Text>(true).text==texts[i], "Saved button text " + names[i]);
                    Check(b.colors.normalColor==UiTheme.BtnAccent && b.GetComponent<Image>().color==Color.white, "Consistent button color with one tint application");
                }
            }
            EditorSceneManager.SaveOpenScenes();
            var ids = new HashSet<ulong>();
            foreach(var t in panel.GetComponentsInChildren<Transform>(true))
            {
                ids.Add(GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).targetObjectId);
                foreach(var c in t.GetComponents<Component>()) ids.Add(GlobalObjectId.GetGlobalObjectIdSlow(c).targetObjectId);
            }
            ids.Add(GlobalObjectId.GetGlobalObjectIdSlow(panel.GetComponentInParent<Canvas>()).targetObjectId);
            ids.Add(GlobalObjectId.GetGlobalObjectIdSlow(d).targetObjectId);
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/DoorMenuAllowedSceneIds.txt", ids.OrderBy(x=>x).Select(x=>x.ToString()));
            File.WriteAllText("Logs/DoorMenuSceneAfterSetup.unity", File.ReadAllText("Assets/Scenes/SampleScene.unity"));
            File.WriteAllLines("Logs/DoorMenuSetupResults.txt",results.Concat(new[]{"RESULT: PASS; assertions="+assertions}));
            EditorApplication.Exit(0);
        }
        catch(Exception error) { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/DoorMenuSetupResults.txt",string.Join("\n",results)+"\nFAIL: "+error); Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Flag,true); Resume(); EditorApplication.EnterPlaymode();
    }
    public static void BeginWithRegression()
    {
        TestGameplayRegressionSuite.RunReviewChecks();
        Begin();
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Flag,false)) return;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if(!EditorApplication.isPlaying) return;
        double now=EditorApplication.timeSinceStartup;
        if(started==0) { started=now; nextStep=now+2; }
        if(now-started>180) { Finish(new TimeoutException("Door menu test timed out")); return; }
        if(now<nextStep) return;
        nextStep=now+0.6;
        try
        {
            if(stage==0) { door=MainDoor(); Check(!RenderSettings.fog,"Play Mode gallery fog disabled"); Check(!door.doorMenuUI.activeSelf,"Menu starts closed"); RenderMenu(new Vector2Int(1920,1080),new Rect(40,20,1840,1040)); }
            else if(stage==1) { RenderMenu(new Vector2Int(720,1600),new Rect(36,100,648,1460)); }
            else if(stage==2) { Check(door.doorMenuUI.activeSelf && DoorMenuTrigger.IsAnyOpen,"Door state registered when opened"); Check(!door.playerController.enabled,"Menu locks movement"); var follow=UnityEngine.Object.FindAnyObjectByType<ThirdPersonCamera>(); Check(!follow.enabled,"Menu locks camera input"); ClickButton(1); Check(callbackCount==1,"Continue callback dispatched once"); Check(!door.doorMenuUI.activeSelf && !DoorMenuTrigger.IsAnyOpen,"Continue closes shared door menu state"); Check(door.playerController.enabled && follow.enabled,"Continue restores movement and camera"); }
            else if(stage==3) { door.ToggleDoorMenu(); PrepareCamera(new Vector2Int(720,1600)); ApplyLayout(new Rect(36,100,648,1400),new Vector2(720,1600)); ClickButton(0); Check(callbackCount==1,"Workshop callback dispatched once"); Check(!door.doorMenuUI.activeSelf && door.minigameUI.activeSelf && MinigameTrigger.IsAnyOpen,"Workshop opens and door menu closes"); Check(!door.playerController.enabled,"Workshop retains movement lock"); }
            else if(stage==4) { door.CloseMinigame(); Check(!door.minigameUI.activeSelf && !MinigameTrigger.IsAnyOpen,"Workshop closes through existing owner"); Check(door.playerController.enabled,"Workshop close restores movement"); }
            else if(stage==5) { door.ToggleDoorMenu(); PrepareCamera(new Vector2Int(1920,1080)); ApplyLayout(new Rect(40,20,1840,1040),new Vector2(1920,1080)); ClickButton(2); Check(callbackCount==1,"Main menu callback dispatched once"); }
            else if(stage==6) { Check(SceneManager.GetActiveScene().name=="MainMenu","Real navigation reaches MainMenu"); Check(!DoorMenuTrigger.IsAnyOpen && !MinigameTrigger.IsAnyOpen,"Scene transition clears modal state"); Finish(null); return; }
            stage++;
        }
        catch(Exception error) { Finish(error); }
    }

    private static void PrepareCamera(Vector2Int size)
    {
        if(renderCamera != null) { renderCamera.targetTexture.Release(); UnityEngine.Object.Destroy(renderCamera.targetTexture); UnityEngine.Object.Destroy(renderCamera.gameObject); }
        var canvas=door.doorMenuUI.GetComponentInParent<Canvas>();
        renderCamera=new GameObject("Door menu render camera",typeof(Camera)).GetComponent<Camera>();
        renderCamera.transform.position=new Vector3(0,0,-100);
        renderCamera.cullingMask=1<<5;
        renderCamera.clearFlags=CameraClearFlags.SolidColor;
        renderCamera.backgroundColor=new Color(.12f,.14f,.18f);
        renderCamera.targetTexture=new RenderTexture(size.x,size.y,24,RenderTextureFormat.ARGB32);
        canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=renderCamera; canvas.planeDistance=10;
        door.doorMenuUI.GetComponent<DoorMenuLayout>().enabled=false;
        Canvas.ForceUpdateCanvases(); renderCamera.Render(); Canvas.ForceUpdateCanvases();
    }
    private static void ApplyLayout(Rect safe,Vector2 size)
    {
        door.doorMenuUI.GetComponent<DoorMenuLayout>().ApplyLayout(safe,size);
        Canvas.ForceUpdateCanvases(); renderCamera.Render();
    }
    private static void RenderMenu(Vector2Int size,Rect safe)
    {
        if(!door.doorMenuUI.activeSelf) door.ToggleDoorMenu();
        PrepareCamera(size); ApplyLayout(safe,size);
        foreach(var rect in door.doorMenuUI.GetComponentsInChildren<RectTransform>())
        {
            var corners=new Vector3[4]; rect.GetWorldCorners(corners);
            Check(corners.All(c=>{var p=RectTransformUtility.WorldToScreenPoint(renderCamera,c);return p.x>=safe.xMin-1 && p.x<=safe.xMax+1 && p.y>=safe.yMin-1 && p.y<=safe.yMax+1;}),rect.name+" inside safe area at "+size);
        }
        foreach(var label in door.doorMenuUI.GetComponentsInChildren<TMP_Text>())
        {
            label.ForceMeshUpdate();
            Check(label.textInfo.characterInfo.Take(label.textInfo.characterCount).All(c=>!char.IsLetter(c.character) || c.textElement.unicode==c.character),"Rendered Vietnamese glyphs have no substitutions: "+label.text);
            Check(!label.isTextOverflowing && label.textInfo.lineCount==1,"Label fits without wrapping: "+label.text);
        }
        var buttons=names.Select(n=>door.doorMenuUI.transform.Find(n).GetComponent<RectTransform>()).ToArray();
        for(int i=0;i<2;i++) { var a=new Vector3[4];var b=new Vector3[4];buttons[i].GetWorldCorners(a);buttons[i+1].GetWorldCorners(b);Check(a[0].y>b[1].y,"Buttons do not overlap"); }
        Rect changedSafe = new Rect(safe.x+8,safe.y+12,safe.width-24,safe.height-40);
        ApplyLayout(changedSafe,size);
        foreach(var rect in door.doorMenuUI.GetComponentsInChildren<RectTransform>())
        {
            var corners=new Vector3[4]; rect.GetWorldCorners(corners);
            Check(corners.All(c=>{var p=RectTransformUtility.WorldToScreenPoint(renderCamera,c);return p.x>=changedSafe.xMin-1 && p.x<=changedSafe.xMax+1 && p.y>=changedSafe.yMin-1 && p.y<=changedSafe.yMax+1;}),rect.name+" reflows inside asymmetric safe inset without viewport resize");
        }
        ApplyLayout(safe,size);
        Directory.CreateDirectory("Logs/DoorMenuImages");
        var previous=RenderTexture.active; RenderTexture.active=renderCamera.targetTexture;
        var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();
        File.WriteAllBytes("Logs/DoorMenuImages/Menu-"+size.x+"x"+size.y+".png",image.EncodeToPNG());
        RenderTexture.active=previous;UnityEngine.Object.Destroy(image);
        for(int i=0;i<3;i++) RaycastButton(i);
    }
    private static PointerEventData RaycastButton(int index)
    {
        var button=door.doorMenuUI.transform.Find(names[index]).GetComponent<Button>();
        var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(renderCamera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"Top raycast is "+names[index]);
        pointer.pointerCurrentRaycast=pointer.pointerPressRaycast=hits[0]; return pointer;
    }
    private static void ClickButton(int index)
    {
        var button=door.doorMenuUI.transform.Find(names[index]).GetComponent<Button>();
        var pointer=RaycastButton(index);
        callbackCount=0;button.onClick.AddListener(()=>callbackCount++);
        pointer.pressPosition=pointer.position;pointer.eligibleForClick=true;
        pointer.pointerPress=ExecuteEvents.ExecuteHierarchy(pointer.pointerCurrentRaycast.gameObject,pointer,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(pointer.pointerPress,pointer,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update-=Tick;SessionState.SetBool(Flag,false);
        Directory.CreateDirectory("Logs");
        results.Add(error==null?"RESULT: PASS; assertions="+assertions+"; native GPU images; simulated pointer input and safe area; no physical device":"FAIL stage="+stage+": "+error);
        File.WriteAllLines("Logs/DoorMenuPlayModeResults.txt",results);
        if(error!=null)Debug.LogException(error);
        EditorApplication.Exit(error==null?0:1);
    }
}
