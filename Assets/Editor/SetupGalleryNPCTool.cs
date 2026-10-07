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
    private const string GuideVisualPath = "Assets/Characters/BusinessMan/BusinessManVisual.prefab";
    private const string VisitorF1VisualPath = "Assets/Characters/npc_casual_set_00/Prefabs/npc_csl_00_character_01m_01.prefab"; // Nam Casual 01
    private const string VisitorF2VisualPath = "Assets/Characters/npc_casual_set_00/Prefabs/npc_csl_00_character_02f_01.prefab"; // Nữ Casual 02
    private const string VisitorMainVisualPath = "Assets/Characters/npc_casual_set_00/Prefabs/npc_csl_00_character_01f_01.prefab"; // Nữ Casual 01
    private const string IdleControllerPath = "Assets/Characters/BusinessMan/BusinessManIdle.controller";
    private const string URPMatFolder = "Assets/Characters/npc_casual_set_00/MaterialsUPR";

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

        // 1. Hướng dẫn viên (Business Man)
        CreateNPCPrefab("GalleryGuide", "Hướng dẫn viên", new[]
        {
            "Chào mừng bạn đến với triển lãm tranh Đông Hồ! Bạn có thể lại gần các bức tranh để xem thông tin.",
            "Bạn muốn qua workshop tô màu hay tiếp tục tham quan? Hãy chọn bên dưới nhé."
        }, GuideVisualPath);

        // 2. Khách tham quan sảnh chính (Nữ Casual 01)
        CreateNPCPrefab("GalleryVisitor", "Khách tham quan", new[]
        {
            "Mình đang xem những bức tranh trong triển lãm này. Mỗi bức tranh đều có phần giới thiệu riêng.",
            "Bạn có thể lại gần tranh và tương tác để đọc thông tin. Nhấn Tiếp tục để đọc câu kế tiếp của mình.",
            "Nếu muốn tự tô một bức tranh, hãy nói chuyện với hướng dẫn viên hoặc đến bàn workshop."
        }, VisitorMainVisualPath);

        // 3. Khách xem tranh Tầng 1 (Nam Casual 01 - Đứng trước tranh Đàn Lợn Âm Dương)
        CreateNPCPrefab("GalleryVisitor_Floor1", "Khách xem tranh (Tầng 1)", new[]
        {
            "Bức tranh 'Đàn lợn âm dương' này sống động thật! Mỗi chú lợn con đều có vòng xoáy âm dương trên mình biểu trưng cho sự sinh sôi nảy nở.",
            "Màu sắc dân gian Đông Hồ trông mộc mạc mà ấm cúng, thể hiện ước nguyện ấm no, sung túc của người nông dân xưa.",
            "Bạn nhớ ghé thăm cả các bức tranh khác ở tầng 1 và lên tầng 2 ngắm bộ tranh lịch sử nữa nhé!"
        }, VisitorF1VisualPath);

        // 4. Khách xem tranh Tầng 2 (Nữ Casual 02 - Đứng ở Tầng 2)
        CreateNPCPrefab("GalleryVisitor_Floor2", "Khách xem tranh (Tầng 2)", new[]
        {
            "Tầng 2 trưng bày rất nhiều tranh đề tài lịch sử và văn hóa dân gian đặc sắc của dân tộc ta!",
            "Bức tranh 'Hai Bà Trưng cưỡi voi' đánh đuổi giặc Đông Hán khí thế thật hào hùng, từng đường nét đều rất dứt khoát và uy phong.",
            "Không gian trên này yên tĩnh và thoáng đãng, ngắm nhìn các bộ tranh tứ bình như Bát Tiên, Thạch Sanh cảm giác thư thái vô cùng."
        }, VisitorF2VisualPath);

        // Cấu hình các Instance sạch trong Scene (loại bỏ khung dây vẽ ảo và cập nhật visual model)
        ConfigureInstance("GalleryGuide", new Vector3(-5.6f, 0f, -7.2f), 215f, workshop);
        ConfigureInstance("GalleryVisitor", new Vector3(-6.1f, 0f, -3.2f), 180f, null);
        ConfigureInstance("GalleryVisitor_Floor1", new Vector3(-4.87f, 0f, 15.5f), 0f, null);
        ConfigureInstance("GalleryVisitor_Floor2", new Vector3(-6.38f, 5.09f, 13.0f), 0f, null);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[NPC Setup] Hoàn tất cài đặt 4 NPC trong phòng triển lãm.");
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

    private static void CreateNPCPrefab(string name, string displayName, string[] lines, string visualPath)
    {
        string path = PrefabFolder + "/" + name + ".prefab";
        bool isGuide = name == "GalleryGuide";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            if (isGuide) UpdateGuideVisual(path);
            else UpdateVisitorVisual(path, visualPath);
            return;
        }
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
            else AddVisitorVisual(root.transform, visualPath);

            var npc = root.AddComponent<NPCInteractable>();
            npc.npcName = displayName;
            npc.dialogueLines = lines;
            npc.interactionCollider = body;

            var outline = root.GetComponent<InteractableOutline>();
            if (outline != null) outline.drawOutline = false; // Tắt khung viền dây ảo trên NPC người

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
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            AddGuideVisual(contents.transform);

            var outline = contents.GetComponent<InteractableOutline>();
            if (outline != null) outline.drawOutline = false;

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

    private static void UpdateVisitorVisual(string path, string visualPath)
    {
        GameObject expected = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
        if (expected == null) throw new System.InvalidOperationException("Visitor visual model is missing: " + visualPath);
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform previous = contents.transform.Find("Character Visual");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            AddVisitorVisual(contents.transform, visualPath);

            var outline = contents.GetComponent<InteractableOutline>();
            if (outline != null) outline.drawOutline = false;

            if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                throw new System.InvalidOperationException("Could not update visitor visual: " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static void AddVisitorVisual(Transform parent, string visualPath)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
        if (model == null) throw new System.InvalidOperationException("Visitor visual model is missing: " + visualPath);
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
        visual.name = "Character Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        // Upgrade materials to URP Lit versions
        foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Material[] mats = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string matName = mats[i].name;
                string urpPath = $"{URPMatFolder}/{matName}.mat";
                Material urpMat = AssetDatabase.LoadAssetAtPath<Material>(urpPath);
                if (urpMat != null && urpMat != mats[i])
                {
                    mats[i] = urpMat;
                    changed = true;
                }
            }
            if (changed)
            {
                renderer.sharedMaterials = mats;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        RuntimeAnimatorController idleController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(IdleControllerPath);
        foreach (var animator in visual.GetComponentsInChildren<Animator>(true))
        {
            animator.applyRootMotion = false;
            if (idleController != null && animator.runtimeAnimatorController == null)
            {
                animator.runtimeAnimatorController = idleController;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
    }

    private static void ConfigureInstance(string name, Vector3 position, float yaw, MinigameTrigger workshop)
    {
        string prefabPath = PrefabFolder + "/" + name + ".prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        GameObject instance = GameObject.Find(name);
        if (instance != null)
        {
            // Cleanly replace instance to remove old broken mesh overrides and ensure complete prefab visual sync
            Undo.DestroyObjectImmediate(instance);
        }

        instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        Undo.RegisterCreatedObjectUndo(instance, "Add gallery NPC: " + name);

        var visual = instance.transform.Find("Character Visual");
        if (visual != null)
        {
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
        }

        var outline = instance.GetComponent<InteractableOutline>();
        if (outline != null)
        {
            outline.drawOutline = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(outline);
            EditorUtility.SetDirty(outline);
        }

        var npc = instance.GetComponent<NPCInteractable>();
        if (npc != null && workshop != null)
        {
            npc.workshopTrigger = workshop;
            PrefabUtility.RecordPrefabInstancePropertyModifications(npc);
            EditorUtility.SetDirty(npc);
        }
    }

    public static void ValidateSavedGallery()
    {
        var guide = GameObject.Find("GalleryGuide")?.GetComponent<NPCInteractable>();
        var visitor = GameObject.Find("GalleryVisitor")?.GetComponent<NPCInteractable>();
        if (guide == null || visitor == null || guide.dialogueLines.Length != 2 || visitor.dialogueLines.Length != 3)
            throw new System.InvalidOperationException("Saved NPCs or dialogue lines are missing.");
        if (guide.workshopTrigger == null || guide.workshopTrigger.minigameUI == null)
            throw new System.InvalidOperationException("Saved guide has no workshop reference.");
        foreach (var npc in Object.FindObjectsByType<NPCInteractable>(FindObjectsInactive.Include))
        {
            if (npc.interactionCollider == null || npc.interactionCollider.isTrigger || npc.GetComponent<SphereCollider>() == null
                || !npc.GetComponent<SphereCollider>().isTrigger || npc.GetComponentsInChildren<SkinnedMeshRenderer>().Length == 0
                || npc.GetComponentsInChildren<PlayerInteraction>(true).Length != 0 || npc.GetComponentsInChildren<PlayerController>(true).Length != 0)
                throw new System.InvalidOperationException("NPC geometry or interaction configuration is invalid: " + npc.name);
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/GalleryNPCConfiguration.txt", "PASS: saved SampleScene reloaded with all visible NPC prefab instances.\n");
        Debug.Log("[NPC Setup] Saved gallery configuration validated.");
    }
}
