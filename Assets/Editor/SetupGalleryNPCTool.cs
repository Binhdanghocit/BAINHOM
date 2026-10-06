using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupGalleryNPCTool
{
    private const string GalleryPath = "Assets/Scenes/SampleScene.unity";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string ModelPath = "Assets/Low Poly Girl/FBX/low_poly_girl .fbx";
    private const string PlayerPrefabPath = "Assets/low_poly_girl  (unlit shader).prefab";
    private const string GuideVisualPath = "Assets/Characters/BusinessMan/BusinessManVisual.prefab";

    // Batch entry point edits the saved gallery; it never relies on unsaved Editor state.
    public static void ConfigureSavedGallery()
    {
        Directory.CreateDirectory("Logs");
        if (!File.Exists("Logs/SampleScene.BeforeNPC.unity")) File.Copy(GalleryPath, "Logs/SampleScene.BeforeNPC.unity");
        EditorSceneManager.OpenScene(GalleryPath);
        ConfigureNPCs();
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), GalleryPath))
            throw new System.InvalidOperationException("Could not save NPC configuration.");
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(GalleryPath);
        ValidateSavedGallery();
    }

    [MenuItem("Tools/Setup Gallery NPCs")]
    public static void ConfigureNPCs()
    {
        if (SceneManager.GetActiveScene().path != GalleryPath)
            throw new System.InvalidOperationException("Open SampleScene before configuring NPCs.");
        var coloring = Object.FindAnyObjectByType<ColoringPageMinigame>(FindObjectsInactive.Include);
        if (coloring == null || coloring.workshopPanel == null)
            throw new System.InvalidOperationException("Gallery has no configured coloring workshop panel.");
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets", "Prefabs");
        var workshop = FindWorkshop(coloring.workshopPanel);
        if (workshop == null) workshop = CreateWorkshopTable(coloring.workshopPanel);
        CreateNPCPrefab("GalleryGuide", "Hướng dẫn viên", new[]
        {
            "Chào mừng bạn đến với triển lãm tranh Đông Hồ! Bạn có thể lại gần các bức tranh để xem thông tin.",
            "Bạn muốn qua workshop tô màu hay tiếp tục tham quan? Hãy chọn bên dưới nhé."
        });
        CreateNPCPrefab("GalleryVisitor", "Khách tham quan", new[]
        {
            "Mình đang xem những bức tranh trong triển lãm này. Mỗi bức tranh đều có phần giới thiệu riêng.",
            "Bạn có thể lại gần tranh và tương tác để đọc thông tin. Nhấn Tiếp tục để đọc câu kế tiếp của mình.",
            "Nếu muốn tự tô một bức tranh, hãy nói chuyện với hướng dẫn viên hoặc đến bàn workshop."
        });
        ConfigureInstance("GalleryGuide", new Vector3(-5.6f, 0f, -7.2f), 215f, workshop);
        ConfigureInstance("GalleryVisitor", new Vector3(-6.1f, 0f, -3.2f), 180f, null);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static MinigameTrigger FindWorkshop(GameObject panel)
    {
        foreach (var trigger in Object.FindObjectsByType<MinigameTrigger>(FindObjectsInactive.Include))
            if (trigger.minigameUI == panel) return trigger;
        return null;
    }

    private static MinigameTrigger CreateWorkshopTable(GameObject panel)
    {
        var root = new GameObject("NPC Workshop Table", typeof(BoxCollider), typeof(Rigidbody));
        Undo.RegisterCreatedObjectUndo(root, "Create NPC workshop table");
        root.transform.position = new Vector3(-3.6f, 0f, -7.2f);
        var range = root.GetComponent<BoxCollider>();
        range.isTrigger = true;
        range.center = Vector3.up;
        range.size = new Vector3(2.6f, 2f, 2f);
        var rigidbody = root.GetComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        MakeTablePart(root.transform, "Tabletop", new Vector3(0, 0.85f, 0), new Vector3(1.5f, 0.12f, 0.9f));
        foreach (float x in new[] { -0.6f, 0.6f }) foreach (float z in new[] { -0.3f, 0.3f })
            MakeTablePart(root.transform, "Leg", new Vector3(x, 0.4f, z), new Vector3(0.08f, 0.8f, 0.08f));
        var trigger = Undo.AddComponent<MinigameTrigger>(root);
        trigger.minigameUI = panel;
        trigger.playerController = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        EditorUtility.SetDirty(trigger);
        return trigger;
    }

    private static void MakeTablePart(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
    }

    private static void CreateNPCPrefab(string name, string displayName, string[] lines)
    {
        string path = PrefabFolder + "/" + name + ".prefab";
        bool isGuide = name == "GalleryGuide";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            if (isGuide) UpdateGuideVisual(path);
            return;
        }
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (!isGuide && (model == null || playerPrefab == null)) throw new System.InvalidOperationException("NPC character model is missing.");
        var root = new GameObject(name, typeof(CapsuleCollider), typeof(Rigidbody));
        try
        {
            var body = root.GetComponent<CapsuleCollider>();
            body.height = 1.8f;
            body.radius = 0.35f;
            body.center = new Vector3(0, 0.9f, 0);
            var proximity = root.AddComponent<SphereCollider>();
            proximity.isTrigger = true;
            proximity.radius = 2.4f;
            proximity.center = Vector3.up;
            var rigidbody = root.GetComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.constraints = RigidbodyConstraints.FreezeAll;
            if (isGuide) AddGuideVisual(root.transform);
            else
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Character Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    foreach (var source in playerPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if (renderer.name == source.name) { renderer.sharedMaterials = source.sharedMaterials; break; }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                var animator = visual.GetComponent<Animator>();
                var sourceAnimator = playerPrefab.GetComponent<Animator>();
                if (animator != null && sourceAnimator != null)
                {
                    animator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
                    animator.applyRootMotion = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
            }
            var npc = root.AddComponent<NPCInteractable>();
            npc.npcName = displayName;
            npc.dialogueLines = lines;
            npc.interactionCollider = body;
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                throw new System.InvalidOperationException("Could not save NPC prefab: " + path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void UpdateGuideVisual(string path)
    {
        GameObject expected = AssetDatabase.LoadAssetAtPath<GameObject>(GuideVisualPath);
        if (expected == null) throw new System.InvalidOperationException("Business Man visual is missing: " + GuideVisualPath);
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform previous = contents.transform.Find("Character Visual");
            if (previous != null && PrefabUtility.GetCorrespondingObjectFromSource(previous.gameObject) == expected) return;
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            AddGuideVisual(contents.transform);
            if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                throw new System.InvalidOperationException("Could not update guide visual: " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static void AddGuideVisual(Transform parent)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(GuideVisualPath);
        if (model == null) throw new System.InvalidOperationException("Business Man visual is missing: " + GuideVisualPath);
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
        visual.name = "Character Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        foreach (var animator in visual.GetComponentsInChildren<Animator>(true))
        {
            animator.applyRootMotion = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
    }

    private static void ConfigureInstance(string name, Vector3 position, float yaw, MinigameTrigger workshop)
    {
        GameObject instance = GameObject.Find(name);
        if (instance == null)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/" + name + ".prefab"));
            instance.name = name;
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            Undo.RegisterCreatedObjectUndo(instance, "Add gallery NPC");
        }
        var npc = instance.GetComponent<NPCInteractable>();
        // Visual refresh must preserve existing per-scene workshop assignments.
        if (workshop == null || npc.workshopTrigger != null) return;
        Undo.RecordObject(npc, "Assign NPC workshop");
        npc.workshopTrigger = workshop;
        PrefabUtility.RecordPrefabInstancePropertyModifications(npc);
        EditorUtility.SetDirty(npc);
    }

    public static void ValidateSavedGallery()
    {
        var guide = GameObject.Find("GalleryGuide")?.GetComponent<NPCInteractable>();
        var visitor = GameObject.Find("GalleryVisitor")?.GetComponent<NPCInteractable>();
        if (guide == null || visitor == null || guide.dialogueLines.Length != 2 || visitor.dialogueLines.Length != 3)
            throw new System.InvalidOperationException("Saved NPCs or dialogue lines are missing.");
        if (guide.workshopTrigger == null || guide.workshopTrigger.minigameUI == null)
            throw new System.InvalidOperationException("Saved guide has no workshop reference.");
        foreach (var npc in new[] { guide, visitor })
        {
            if (npc.interactionCollider == null || npc.interactionCollider.isTrigger || npc.GetComponent<SphereCollider>() == null
                || !npc.GetComponent<SphereCollider>().isTrigger || npc.GetComponentsInChildren<SkinnedMeshRenderer>().Length == 0
                || npc.GetComponentsInChildren<PlayerInteraction>(true).Length != 0 || npc.GetComponentsInChildren<PlayerController>(true).Length != 0)
                throw new System.InvalidOperationException("NPC geometry or interaction configuration is invalid: " + npc.name);
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/GalleryNPCConfiguration.txt", "PASS: saved SampleScene reloaded with two visible NPC prefab instances.\nGuide: two lines, linked workshop.\nVisitor: three lines.\nBody collider selects the NPC; separate proximity trigger tracks players.\n");
        Debug.Log("[NPC Setup] Saved gallery configuration validated.");
    }

    public static void InspectGallery()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var report = new StringBuilder();
        foreach (var root in scene.GetRootGameObjects())
        {
            report.AppendLine($"ROOT {root.name}: {root.transform.position}, scale {root.transform.lossyScale}");
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                report.AppendLine($"  CHARACTER {renderer.name}: bounds {renderer.bounds}");
        }
        foreach (var table in Object.FindObjectsByType<MinigameTrigger>(FindObjectsInactive.Include))
            report.AppendLine($"WORKSHOP {table.name}: position {table.transform.position}, bounds {table.GetComponent<Collider>().bounds}, UI {table.minigameUI}");
        foreach (var player in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include))
            report.AppendLine($"PLAYER {player.name}: position {player.transform.position}, scale {player.transform.lossyScale}");
        foreach (var collider in Object.FindObjectsByType<Collider>())
        {
            if (!collider.isTrigger && collider.bounds.size.y < 0.6f && collider.bounds.size.x > 4f)
                report.AppendLine($"FLOOR? {collider.name}: {collider.bounds}");
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/GalleryNPCInspection.txt", report.ToString());
    }
}
