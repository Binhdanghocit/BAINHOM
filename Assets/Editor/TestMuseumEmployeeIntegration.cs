using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Creates temporary scene instances and may exit the Editor. Run on a project copy.
public static class TestMuseumEmployeeIntegration
{
    private const string Folder = "Assets/Characters/BusinessMan";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string GuidePath = "Assets/Prefabs/GalleryGuide.prefab";
    private static readonly List<string> Results = new List<string>();
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        Results.Add("PASS: " + message);
    }

    public static void Begin()
    {
        try
        {
            CheckAssetsAndSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            TestGameplayRegressionSuite.RunAllTests();
            TestGalleryNPCPlayMode.Begin();
        }
        catch (Exception error)
        {
            Directory.CreateDirectory("Logs/MuseumEmployee");
            Results.Add("RESULT: FAIL\n" + error);
            File.WriteAllLines("Logs/MuseumEmployee/IntegrationResults.txt", Results);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    private static void CheckAssetsAndSetup()
    {
        Results.Clear(); Directory.CreateDirectory("Logs/MuseumEmployee");
        var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "/source/Business_Man.fbx");
        var assets = AssetDatabase.LoadAllAssetsAtPath(importer.assetPath);
        var avatar = assets.OfType<Avatar>().Single();
        var clips = assets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        Check(importer.animationType == ModelImporterAnimationType.Human && avatar.isValid && avatar.isHuman,
            "Business Man imports with a valid Humanoid skeleton/avatar");
        Check(clips.Length == 26 && clips.Any(c => c.name == "Rig|cycle_talking"), "All 26 actual source clips remain available, including talking");
        var idle = clips.Single(c => c.name == "Rig|idle");
        Check(idle.isHumanMotion && idle.isLooping && Mathf.Abs(idle.length - 8f) < 0.01f, "The existing eight-second Humanoid idle loops");
        Results.Add("CLIPS: " + string.Join(", ", clips.Select(c => c.name)));
        Check(!clips.Any(c => c.name.ToLowerInvariant().Contains("greet") || c.name.ToLowerInvariant().Contains("wave")),
            "No greeting/waving clip is assumed to exist");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/BusinessMan.mat");
        Check(material != null && material.shader.name == "Universal Render Pipeline/Lit" && material.shader.isSupported,
            "Employee material uses the supported project URP shader");
        foreach (string property in new[] { "_BaseMap", "_BumpMap", "_OcclusionMap", "_SpecGlossMap" })
            Check(material.GetTexture(property) != null, "Material texture is assigned: " + property);
        var normal = (TextureImporter)AssetImporter.GetAtPath(Folder + "/textures/business_man_normal.png");
        Check(normal.textureType == TextureImporterType.NormalMap && !normal.sRGBTexture, "Normal map imports as linear tangent-space normal data");
        var spec = (TextureImporter)AssetImporter.GetAtPath(Folder + "/textures/business_man_spec.png");
        var ao = (TextureImporter)AssetImporter.GetAtPath(Folder + "/textures/business_man_ao.png");
        Check(spec.sRGBTexture && !ao.sRGBTexture, "Specular colour imports as sRGB while occlusion remains linear data");

        byte[] visitor = File.ReadAllBytes("Assets/Prefabs/GalleryVisitor.prefab");
        byte[] player = File.ReadAllBytes("Assets/low_poly_girl  (unlit shader).prefab");
        byte[] sceneBytes = File.ReadAllBytes(ScenePath), guideBytes = File.ReadAllBytes(GuidePath);
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var guide = GameObject.Find("GalleryGuide").GetComponent<NPCInteractable>();
        Vector3 position = guide.transform.position; Quaternion rotation = guide.transform.rotation;
        var workshop = guide.workshopTrigger;
        string name = guide.npcName; string[] lines = (string[])guide.dialogueLines.Clone();
        var body = guide.interactionCollider;
        for (int run = 0; run < 2; run++)
        {
            SetupGalleryNPCTool.ConfigureNPCs();
            Check(guide.npcName == name && guide.dialogueLines.SequenceEqual(lines) && guide.interactionCollider == body,
                "Repeated setup preserves guide dialogue and interaction collider: " + run);
            Check(guide.workshopTrigger == workshop && guide.transform.position == position && guide.transform.rotation == rotation,
                "Repeated setup preserves existing workshop and scene placement: " + run);
            Check(guide.transform.Cast<Transform>().Count(t => t.name == "Character Visual") == 1,
                "Repeated setup keeps exactly one guide visual: " + run);
            Check(File.ReadAllBytes(GuidePath).SequenceEqual(guideBytes), "Repeated setup does not rewrite a correct guide prefab: " + run);
            Check(File.ReadAllBytes("Assets/Prefabs/GalleryVisitor.prefab").SequenceEqual(visitor)
                && File.ReadAllBytes("Assets/low_poly_girl  (unlit shader).prefab").SequenceEqual(player),
                "Visitor and player prefabs are unchanged: " + run);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuidePath);
        CheckLocalID(prefab, 4241061625216791681L, "Guide root");
        CheckLocalID(prefab.GetComponent<CapsuleCollider>(), 7459503948550711644L, "Interaction body collider");
        CheckLocalID(prefab.GetComponent<NPCInteractable>(), 8317915467303946996L, "NPC component/workshop override target");
        var expected = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/BusinessManVisual.prefab");
        Check(PrefabUtility.GetCorrespondingObjectFromSource(prefab.transform.Find("Character Visual").gameObject) == expected,
            "Saved guide references the distinct Business Man visual prefab");
        Check(guide.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 3,
            "All three Business Man skinned meshes display in SampleScene");
        Check(guide.GetComponentsInChildren<Collider>(true).Length == 2 && body == guide.GetComponent<CapsuleCollider>(),
            "Only the original body/proximity colliders remain; visual adds no blockers");
        Check(guide.GetComponentsInChildren<PlayerController>(true).Length == 0
            && guide.GetComponentsInChildren<PlayerInteraction>(true).Length == 0, "Employee visual has no player behaviour");
        foreach (var renderer in guide.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            Check(renderer.sharedMaterials.All(m => m == material), "Saved mesh uses reviewed URP material: " + renderer.name);
        var animator = guide.GetComponentInChildren<Animator>();
        Check(animator.avatar == avatar && !animator.applyRootMotion && animator.runtimeAnimatorController.animationClips.Length == 1
            && animator.runtimeAnimatorController.animationClips[0] == idle, "Guide uses its own idle controller/avatar with root motion disabled");
        CheckAnimationAndGround(guide, animator);

        // Recreate an absent scene instance, then discard the fixture by reloading the saved scene.
        UnityEngine.Object.DestroyImmediate(guide.gameObject);
        SetupGalleryNPCTool.ConfigureNPCs();
        var fresh = GameObject.Find("GalleryGuide").GetComponent<NPCInteractable>();
        Check(fresh.GetComponentInChildren<Animator>().avatar == avatar && fresh.dialogueData != null && fresh.workshopTrigger != null,
            "Setup creates a missing guide instance with Business Man and workshop link");
        Check(File.ReadAllBytes(ScenePath).SequenceEqual(sceneBytes), "Validation never writes the saved scene or current bake");
        EditorSceneManager.OpenScene(ScenePath);
        Results.Add("RESULT: PASS; " + Results.Count(r => r.StartsWith("PASS:")) + " assertions; imported assets, repeated setup, saved IDs, two idle cycles and floor clearance");
        File.WriteAllLines("Logs/MuseumEmployee/IntegrationResults.txt", Results);
    }

    private static void CheckLocalID(UnityEngine.Object value, long expected, string label)
    {
        Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id)
            && guid == "fb176beed1f64144ea9db5e03bd2c904" && id == expected, label + " retains its saved GUID/fileID");
    }
    private static Bounds MeshBounds(GameObject root)
    {
        bool hasPoint = false; Bounds bounds = default;
        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = new Mesh();
            try
            {
                renderer.BakeMesh(mesh, true);
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 point = renderer.transform.TransformPoint(vertex);
                    if (!hasPoint) { bounds = new Bounds(point, Vector3.zero); hasPoint = true; }
                    else bounds.Encapsulate(point);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        return bounds;
    }
    private static void CheckAnimationAndGround(NPCInteractable guide, Animator animator)
    {
        animator.Rebind(); animator.Play("Idle", 0, 0); animator.Update(0);
        Physics.SyncTransforms();
        Bounds initial = MeshBounds(guide.gameObject);
        Check(Mathf.Abs(initial.size.y - 1.8f) < 0.02f, "Actual idle mesh fits 1.8m body height after FBX unit conversion");
        var hits = Physics.RaycastAll(guide.transform.position + Vector3.up * 0.4f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => !h.collider.transform.IsChildOf(guide.transform)).OrderBy(h => h.distance).ToArray();
        Check(hits.Length > 0, "A real gallery floor collider exists below the guide");
        float floor = hits[0].point.y;
        Check(Mathf.Abs(initial.min.y - floor) < 0.03f, "Boot soles rest within 3cm of the gallery floor");
        Vector3 root = guide.transform.position;
        var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        var right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        Vector3 leftStart = left.position, rightStart = right.position;
        float drift = 0, minimum = initial.min.y, maximum = initial.min.y;
        for (int frame = 0; frame < 480; frame++)
        {
            animator.Update(1f / 30);
            foreach (Vector3 difference in new[] { left.position - leftStart, right.position - rightStart })
                drift = Mathf.Max(drift, new Vector2(difference.x, difference.z).magnitude);
            if (frame % 30 == 0) { float bottom = MeshBounds(guide.gameObject).min.y; minimum = Mathf.Min(minimum, bottom); maximum = Mathf.Max(maximum, bottom); }
        }
        Check(Vector3.Distance(root, guide.transform.position) < 0.00001f, "Two idle loops never translate the guide root");
        Check(drift < 0.02f, "Idle foot horizontal drift stays below 2cm over two cycles");
        Check(minimum > floor - 0.03f && maximum < floor + 0.03f, "Animated boot soles retain floor clearance across sampled idle poses");
        Results.Add("GEOMETRY: height=" + initial.size.y + "; floor=" + floor + "; sole range=" + minimum + ".." + maximum + "; max foot XZ drift=" + drift);
    }
}
