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

        CreateNPCPrefab("GalleryGuide", "Hướng dẫn viên", GetOrCreateDialogue("GalleryGuide"), GuideVisualPath);
        CreateNPCPrefab("GalleryVisitor", "Khách sảnh chính", GetOrCreateDialogue("GalleryVisitor"), VisitorMainVisualPath);
        CreateNPCPrefab("GalleryVisitor_Floor1", "Khách tầng 1", GetOrCreateDialogue("GalleryVisitor_Floor1"), VisitorF1VisualPath);
        CreateNPCPrefab("GalleryVisitor_Floor2", "Khách tầng 2", GetOrCreateDialogue("GalleryVisitor_Floor2"), VisitorF2VisualPath);

        // Only fill missing data/instances; preserve authored geometry and placement.
        ConfigureInstance("GalleryGuide", new Vector3(-5.6f, 0f, -7.2f), 215f, workshop);
        ConfigureInstance("GalleryVisitor", new Vector3(-6.1f, 0f, -3.2f), 180f, null);
        ConfigureInstance("GalleryVisitor_Floor1", new Vector3(-4.87f, 0f, 15.5f), 0f, null);
        ConfigureInstance("GalleryVisitor_Floor2", new Vector3(-6.38f, 5.09f, 13.0f), 0f, null);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[NPC Setup] Hoàn tất cài đặt 4 NPC trong phòng triển lãm.");
    }

    // Defaults are used only when the asset is absent. Inspector edits are never reset.
    private static NPCDialogueData GetOrCreateDialogue(string name)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Dialogue")) AssetDatabase.CreateFolder("Assets", "Dialogue");
        string path = "Assets/Dialogue/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<NPCDialogueData>(path);
        if (existing != null) return existing;
        var data = ScriptableObject.CreateInstance<NPCDialogueData>();
        data.exitLabel = "Đóng hội thoại";
        switch (name)
        {
            case "GalleryGuide":
                data.opening = "Chào mừng bạn đến với triển lãm tranh Đông Hồ! Bạn muốn bắt đầu tham quan hay thử tô một bức tranh?";
                data.options = new[]
                {
                    new DialogueOption { question = "Tôi nên bắt đầu ở đâu?", answer = "Bạn có thể bắt đầu từ những bức tranh ở sảnh, rồi khám phá tầng 1 và tầng 2. Mỗi bức đều có phần giới thiệu để bạn tìm hiểu thêm.", action = DialogueAction.AskAnother },
                    new DialogueOption { question = "Tôi muốn thử tô tranh", answer = "Mời bạn ghé workshop để tự tô một bức tranh Đông Hồ. Bạn có thể quay lại tham quan sau khi trải nghiệm.", action = DialogueAction.EnterWorkshop },
                    new DialogueOption { question = "Cảm ơn, tôi tham quan tiếp.", answer = "Chúc bạn có một buổi tham quan thú vị!", action = DialogueAction.EndConversation },
                };
                break;
            case "GalleryVisitor":
                data.opening = "Chào bạn! Mình đang ngắm tranh, càng nhìn càng thấy nhiều điều thú vị.";
                data.options = new[]
                {
                    new DialogueOption { question = "Bạn thích bức nào nhất?", answer = "Mình thích bức Đám cưới chuột nhất. Cảnh đoàn chuột nối nhau làm mình cứ muốn nhìn thêm.", action = DialogueAction.AskAnother },
                    new DialogueOption { question = "Điều gì khiến bạn chú ý?", answer = "Mình thích màu sắc và những chi tiết nhỏ trong tranh. Có những nét thoạt nhìn rất đơn giản mà lại khiến mình nhớ lâu.", action = DialogueAction.AskAnother },
                    new DialogueOption { question = "Chúc bạn tham quan vui.", answer = "Cảm ơn bạn, chúc bạn cũng tìm được một bức tranh mình yêu thích!", action = DialogueAction.EndConversation },
                };
                break;
            case "GalleryVisitor_Floor1":
                data.opening = "Bạn cũng đang xem bức Lợn đàn à? Mình thấy cảnh lợn mẹ và đàn con thật gần gũi.";
                data.options = new[]
                {
                    new DialogueOption { question = "Bạn đang xem điều gì?", answer = "Mình đang nhìn đàn lợn con quây quần quanh mẹ. Cảnh ấy khiến mình nghĩ đến một gia đình ấm áp.", action = DialogueAction.AskAnother },
                    new DialogueOption { question = "Bạn thích chi tiết nào?", answer = "Mình thích những xoáy âm dương trên mình lợn. Chi tiết ấy làm bức tranh nổi bật và khiến mình muốn ngắm kỹ hơn.", action = DialogueAction.AskAnother },
                    new DialogueOption { question = "Tôi cũng muốn xem kỹ hơn.", answer = "Ừ, mình cũng muốn nán lại ngắm thêm một lúc. Chúc bạn tìm thấy chi tiết mình thích!", action = DialogueAction.EndConversation },
                };
                break;
            case "GalleryVisitor_Floor2":
                data.opening = "Chào bạn! Những bức tranh ở khu này khiến mình muốn dừng lại lâu hơn.";
                data.options = new[]
                {
                    new DialogueOption { question = "Khu này có gì khác tầng dưới?", answer = "Ở đây, mình chú ý hơn đến những bức tranh về nhân vật lịch sử. Cảm giác khi ngắm chúng hào hùng hơn những cảnh đời thường mình vừa xem.", action = DialogueAction.AskAnother },
                    new DialogueOption { question = "Bạn gợi ý tôi xem bức nào?", answer = "Mình gợi ý bức Hai Bà Trưng cưỡi voi. Hình ảnh Hai Bà xung trận khiến mình cảm nhận rõ khí thế mạnh mẽ của bức tranh.", action = DialogueAction.AskAnother },
                    new DialogueOption { question = "Tôi sẽ khám phá tiếp.", answer = "Chúc bạn tìm được những điều thú vị ở khu này nhé!", action = DialogueAction.EndConversation },
                };
                break;
            default: throw new System.ArgumentException("Unknown gallery NPC: " + name);
        }
        AssetDatabase.CreateAsset(data, path);
        return data;
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

    private static void CreateNPCPrefab(string name, string displayName, NPCDialogueData data, string visualPath)
    {
        string path = PrefabFolder + "/" + name + ".prefab";
        bool isGuide = name == "GalleryGuide";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            GameObject existing = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var npc = existing.GetComponent<NPCInteractable>();
                if (npc != null && npc.dialogueData == null)
                {
                    npc.dialogueData = data;
                    PrefabUtility.SaveAsPrefabAsset(existing, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(existing); }
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
            npc.dialogueData = data;
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
        if (instance == null)
            foreach (var candidate in Object.FindObjectsByType<NPCInteractable>(FindObjectsInactive.Include))
                if (candidate.name == name) { instance = candidate.gameObject; break; }
        if (instance != null)
        {
            var existingNPC = instance.GetComponent<NPCInteractable>();
            if (existingNPC != null)
            {
                Undo.RecordObject(existingNPC, "Assign missing NPC dialogue/workshop");
                bool changed = false;
                if (existingNPC.dialogueData == null)
                {
                    existingNPC.dialogueData = prefab.GetComponent<NPCInteractable>().dialogueData;
                    changed = true;
                }
                if (existingNPC.workshopTrigger == null && workshop != null)
                {
                    existingNPC.workshopTrigger = workshop;
                    changed = true;
                }
                if (changed)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(existingNPC);
                    EditorUtility.SetDirty(existingNPC);
                }
            }
            return;
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
        foreach (string npcName in new[] { "GalleryGuide", "GalleryVisitor", "GalleryVisitor_Floor1", "GalleryVisitor_Floor2" })
        {
            var npc = GameObject.Find(npcName)?.GetComponent<NPCInteractable>();
            if (npc == null || npc.dialogueData == null || string.IsNullOrWhiteSpace(npc.dialogueData.opening)
                || npc.dialogueData.options == null || npc.dialogueData.options.Length == 0)
                throw new System.InvalidOperationException("Saved NPC dialogue is missing: " + npcName);
            foreach (DialogueOption option in npc.dialogueData.options)
                if (option == null || !option.IsValid)
                    throw new System.InvalidOperationException("Empty/invalid NPC question: " + npcName);
        }
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
