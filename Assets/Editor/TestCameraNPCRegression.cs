using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Real PhysX queries and saved-scene lifecycle. Input dispatch is simulated,
// not a claim of physical mouse/touch/controller or device-rendering coverage.
public static class TestCameraNPCRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Flag = "BAINHOM.Round5.CameraNPC";
    private static readonly List<string> results = new List<string>();
    private static bool renderCameraCases;
    private static object Call(object instance, string name, params object[] args)
        => instance.GetType().GetMethod(name, Private).Invoke(instance, args);
    private static T Get<T>(object instance, string name)
        => (T)instance.GetType().GetField(name, Private).GetValue(instance);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }

    public static void RunCameraPhysics()
    {
        results.Clear();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var fixtures = new GameObject("Round5 camera fixtures");
        try
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            player.name = "Player collider and renderer";
            player.transform.SetParent(fixtures.transform);
            player.tag = "Player";
            var cameraObject = new GameObject("Collision camera", typeof(Camera), typeof(ThirdPersonCamera));
            cameraObject.transform.SetParent(fixtures.transform);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.aspect = 1f;
            var follow = cameraObject.GetComponent<ThirdPersonCamera>();
            follow.target = player.transform;
            follow.offset = Vector3.zero;
            follow.distance = 3;
            follow.collisionRadius = 0.2f;
            follow.collisionPadding = 0.04f;
            follow.characterRenderers = new[] { player.GetComponent<Renderer>() };

            Action tick = () => { Physics.SyncTransforms(); Call(follow, "UpdateCameraPosition", 1f / 60f); };
            Func<float> actual = () => Vector3.Distance(player.transform.position, cameraObject.transform.position);
            Func<string, Vector3, Vector3, BoxCollider> box = (name, position, size) =>
            {
                var collider = new GameObject(name, typeof(BoxCollider)).GetComponent<BoxCollider>();
                collider.transform.SetParent(fixtures.transform);
                collider.transform.position = position;
                collider.size = size;
                return collider;
            };
            Action<Collider, string> clear = (obstacle, label) => Check(
                Vector3.Distance(obstacle.ClosestPoint(cameraObject.transform.position), cameraObject.transform.position) >= 0.199f,
                label + ": camera sphere has clearance from solid geometry");

            tick();
            Check(Mathf.Abs(actual() - 3) < 0.001f, "Initial camera uses chosen zoom and ignores overlapping player collider");
            var wall = box("Wall", new Vector3(0, 0, -1.5f), new Vector3(10, 10, 0.1f));
            tick();
            Check(Mathf.Abs(actual() - 1.21f) < 0.003f, "New wall pulls camera in on the first update to independent surface/radius/padding bound");
            clear(wall, "Wall");
            Check(follow.distance == 3 && !follow.IsFirstPerson, "Collision preserves selected zoom and third-person mode");
            float stopped = actual();
            for (int i = 0; i < 90; i++) tick();
            Check(Mathf.Abs(actual() - stopped) < 0.0001f, "Static wall does not cause distance jitter over 90 frames");
            wall.enabled = false;
            tick();
            Check(actual() > stopped && actual() < 2.99f, "Obstacle removal recovers outward smoothly instead of snapping");
            float previous = actual();
            bool monotonic = true;
            for (int i = 0; i < 180; i++) { tick(); float next = actual(); monotonic &= next >= previous - 0.00001f && next <= 3.00001f; previous = next; }
            Check(monotonic && Mathf.Abs(actual() - 3) < 0.001f, "Recovery reaches original zoom without overshoot");

            wall.enabled = true;
            wall.transform.position = new Vector3(0, 0, -2);
            wall.size = new Vector3(10, 10, 0.001f);
            tick();
            Check(actual() < 1.77f, "Thin 1mm wall constrains the camera immediately");
            clear(wall, "Thin wall");
            follow.distance = 5;
            tick();
            Check(actual() < 1.77f && follow.distance == 5, "Zooming out behind a wall keeps the new user zoom separately");
            follow.distance = 0.5f;
            tick();
            Check(Mathf.Abs(actual() - 0.5f) < 0.001f, "Zooming in inside the unobstructed segment responds immediately");
            follow.distance = 0;
            tick();
            Check(actual() == 0 && follow.IsFirstPerson && !player.GetComponent<Renderer>().enabled,
                "Switching to first person reaches pivot and hides the player mesh");
            follow.distance = 3;
            wall.transform.position = new Vector3(0, 0, -0.5f);
            for (int i = 0; i < 90; i++) tick();
            Check(actual() < ThirdPersonCamera.FirstPersonThreshold && !follow.IsFirstPerson && player.GetComponent<Renderer>().enabled,
                "A close wall does not change chosen view mode or renderer mode");
            clear(wall, "Close wall");

            wall.enabled = false;
            var volume = box("Trigger volume", new Vector3(0, 0, -0.8f), Vector3.one);
            volume.isTrigger = true;
            var playerChild = box("Player child collider", new Vector3(0, 0, -1.5f), Vector3.one * 0.2f);
            playerChild.transform.SetParent(player.transform, true);
            var otherPlayer = new GameObject("Other player collider", typeof(CharacterController));
            otherPlayer.transform.SetParent(fixtures.transform);
            otherPlayer.transform.position = new Vector3(0, -1, -2);
            for (int i = 0; i < 180; i++) tick();
            Check(Mathf.Abs(actual() - 3) < 0.001f, "Triggers, player child colliders and other player controller colliders do not shorten camera distance");

            wall.enabled = true;
            wall.transform.position = new Vector3(0, 0, -2);
            wall.size = new Vector3(10, 10, 0.05f);
            var side = box("Corner side wall", new Vector3(1.5f, 0, 0), new Vector3(0.05f, 10, 10));
            bool cornerClear = true;
            for (int yaw = -80; yaw <= 80; yaw += 5)
            {
                follow.currentX = yaw;
                tick();
                Vector3 position = cameraObject.transform.position;
                cornerClear &= Vector3.Distance(wall.ClosestPoint(position), position) >= 0.199f
                    && Vector3.Distance(side.ClosestPoint(position), position) >= 0.199f;
            }
            Check(cornerClear && follow.distance == 3, "33 orbit angles around a room corner stay clear of both walls and preserve zoom");
            follow.currentX = -45;
            camera.nearClipPlane = 0.3f;
            camera.fieldOfView = 70;
            camera.aspect = 2560f / 1080f;
            tick();
            bool cornersClear = true;
            foreach (Vector2 corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
            {
                Vector3 position = camera.ViewportToWorldPoint(new Vector3(corner.x, corner.y, camera.nearClipPlane));
                cornersClear &= !wall.bounds.Contains(position) && !side.bounds.Contains(position);
            }
            Check(cornersClear && actual() > 0, "Wide 21:9 near-plane corners remain outside room walls");
            wall.enabled = side.enabled = false;
            follow.distance = 4.5f;
            for (int i = 0; i < 180; i++) tick();
            Check(Mathf.Abs(actual() - 4.5f) < 0.001f && follow.distance == 4.5f, "After orbit and zoom changes, clearing obstacles recovers the latest chosen zoom");
            volume.enabled = playerChild.enabled = false;
            otherPlayer.SetActive(false);
            player.transform.localScale = Vector3.one * 0.08f;
            RunOverlapCases(fixtures.transform, camera, follow, tick);
            results.Add("RESULT: PASS, " + results.FindAll(entry => entry.StartsWith("PASS: ")).Count
                + " camera assertions. Actual PhysX queries; deterministic 1/60s camera updates; optional diagnostic GPU renders, no physical input/device assessment.");
        }
        catch (Exception exception)
        {
            results.Add("RESULT: FAIL\n" + exception);
            throw;
        }
        finally
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/CameraCollisionResults.txt", results);
            UnityEngine.Object.DestroyImmediate(fixtures);
        }
    }

    public static void RunCameraPhysicsAndRender()
    {
        renderCameraCases = true;
        try { RunCameraPhysics(); }
        finally { renderCameraCases = false; }
    }

    private static void RunOverlapCases(Transform fixtures, Camera camera, ThirdPersonCamera follow, Action tick)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Overlap wall X";
        wall.transform.SetParent(fixtures);
        wall.transform.position = new Vector3(0.2f, 0, 0);
        wall.transform.localScale = new Vector3(0.1f, 12, 12);
        var side = GameObject.CreatePrimitive(PrimitiveType.Cube);
        side.name = "Overlap wall Z";
        side.transform.SetParent(fixtures);
        side.transform.position = new Vector3(0, 0, 0.2f);
        side.transform.localScale = new Vector3(12, 12, 0.1f);
        side.SetActive(false);
        Collider wallCollider = wall.GetComponent<Collider>(), sideCollider = side.GetComponent<Collider>();
        camera.nearClipPlane = 0.3f;
        camera.fieldOfView = 70;
        follow.distance = 3;
        Physics.SyncTransforms();

        foreach (float aspect in new[] { 1f, 16f / 9f, 2560f / 1080f })
        {
            camera.aspect = aspect;
            float halfHeight = camera.nearClipPlane * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            // Independent bound from projection dimensions, not runtime helper output.
            float nearRadius = Mathf.Sqrt(camera.nearClipPlane * camera.nearClipPlane
                + halfHeight * halfHeight * (1 + aspect * aspect));
            Check(wallCollider.ClosestPoint(Vector3.zero) != Vector3.zero
                && Array.Exists(Physics.OverlapSphere(Vector3.zero, nearRadius, ~0, QueryTriggerInteraction.Ignore), c => c == wallCollider),
                "Pivot outside X wall but starting sphere overlaps, aspect " + aspect);
            foreach (bool corner in new[] { false, true })
            {
                side.SetActive(corner);
                if (corner) Check(sideCollider.ClosestPoint(Vector3.zero) != Vector3.zero,
                    "Corner pivot also lies outside Z wall, aspect " + aspect);
                foreach (int yaw in new[] { 0, 45, -45, 90, -90, 135, -135, 180 })
                {
                    follow.currentX = yaw;
                    CallPitch(follow, yaw == 135 ? 25f : yaw == -135 ? -25f : 0f);
                    tick();
                    string label = (corner ? "Corner" : "Single wall") + " aspect " + aspect + " yaw " + yaw;
                    Check(CameraClear(camera, nearRadius, wallCollider, corner ? sideCollider : null),
                        label + ": sphere clearance and entire near-plane slab are outside actual solid colliders");
                    Check(follow.distance == 3 && !follow.IsFirstPerson, label + ": chosen zoom/view mode preserved");
                    if (yaw == 0)
                    {
                        // Settle the intended recovery from the preceding orbit/zoom
                        // constraint before measuring stationary jitter.
                        for (int i = 0; i < 180; i++) tick();
                        Vector3 stable = camera.transform.position;
                        for (int i = 0; i < 90; i++) tick();
                        if ((camera.transform.position - stable).sqrMagnitude >= 0.000001f)
                            results.Add("DATA: stability start " + stable.ToString("F6")
                                + ", end " + camera.transform.position.ToString("F6"));
                        Check((camera.transform.position - stable).sqrMagnitude < 0.000001f,
                            label + ": overlap correction remains stable for 90 frames");
                    }
                    if (renderCameraCases && aspect > 2f && (yaw == 0 || yaw == 45) && (!corner || yaw == 45))
                        RenderOverlapCase(fixtures, camera, wall, side,
                            (corner ? "Corner" : "Wall") + (yaw == 0 ? "-Parallel" : "-Oblique"));
                }
            }
        }

        // Recovery of the geometry correction must not snap sideways when a wall disappears.
        side.SetActive(false);
        follow.currentX = 0;
        CallPitch(follow, 0);
        for (int i = 0; i < 180; i++) tick();
        Vector3 constrained = camera.transform.position;
        Check(constrained.x < -0.1f, "Parallel overlap case uses a geometrically safe lateral correction rather than zero boom");
        wall.SetActive(false);
        tick();
        Check(camera.transform.position.x > constrained.x && camera.transform.position.x < -0.05f,
            "Removing wall begins smooth lateral recovery instead of snapping to pivot line");
        bool smooth = true;
        float previousX = camera.transform.position.x;
        for (int i = 0; i < 180; i++)
        {
            tick();
            smooth &= camera.transform.position.x >= previousX - 0.0001f && camera.transform.position.x <= 0.0001f;
            previousX = camera.transform.position.x;
        }
        Check(smooth && Vector3.Distance(camera.transform.position, new Vector3(0, 0, -3)) < 0.002f,
            "Overlap correction recovers original orbit/zoom monotonically when clear");
        wall.SetActive(true);
        tick();
        float finalHalfHeight = camera.nearClipPlane * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        float finalRadius = Mathf.Sqrt(camera.nearClipPlane * camera.nearClipPlane
            + finalHalfHeight * finalHalfHeight * (1 + camera.aspect * camera.aspect));
        Check(CameraClear(camera, finalRadius, wallCollider, null), "A wall reappearing over the pivot sphere is corrected in the same update");
    }

    private static void CallPitch(ThirdPersonCamera follow, float pitch)
        => typeof(ThirdPersonCamera).GetField("currentY", Private).SetValue(follow, pitch);

    private static bool CameraClear(Camera camera, float radius, Collider wall, Collider side)
    {
        Vector3 center = camera.transform.position;
        float wallClearance = Vector3.Distance(center, wall.ClosestPoint(center));
        float sideClearance = side != null ? Vector3.Distance(center, side.ClosestPoint(center)) : float.PositiveInfinity;
        if (wallClearance < radius - 0.001f || sideClearance < radius - 0.001f)
        {
            results.Add("DATA: failed sphere clearance: camera " + center.ToString("F5") + ", required radius " + radius
                + ", wall gap " + wallClearance + ", side gap " + sideClearance);
            return false;
        }
        Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0, 0, camera.nearClipPlane));
        Vector3 bottomRight = camera.ViewportToWorldPoint(new Vector3(1, 0, camera.nearClipPlane));
        Vector3 topLeft = camera.ViewportToWorldPoint(new Vector3(0, 1, camera.nearClipPlane));
        Vector3 topRight = camera.ViewportToWorldPoint(new Vector3(1, 1, camera.nearClipPlane));
        var hits = Physics.OverlapBox((bottomLeft + topRight) * 0.5f,
            new Vector3(Vector3.Distance(bottomLeft, bottomRight) * 0.5f,
                Vector3.Distance(bottomLeft, topLeft) * 0.5f, 0.005f),
            camera.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        bool clear = !Array.Exists(hits, c => c == wall || c == side);
        if (!clear) results.Add("DATA: failed near slab: center " + center.ToString("F5")
            + ", corners " + bottomLeft.ToString("F5") + " / " + topRight.ToString("F5"));
        return clear;
    }

    private static void RenderOverlapCase(Transform fixtures, Camera camera, GameObject wall, GameObject side, string name)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var wallMaterial = new Material(shader) { color = new Color(0.44f, 0.58f, 0.72f) };
        wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
        side.GetComponent<Renderer>().sharedMaterial = wallMaterial;
        var diagnostics = new GameObject("Camera clearance diagnostics");
        diagnostics.transform.SetParent(fixtures);
        var markerMaterial = new Material(shader) { color = Color.green };
        var pivotMaterial = new Material(shader) { color = Color.red };
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.transform.SetParent(diagnostics.transform);
        marker.transform.position = camera.transform.position;
        marker.transform.localScale = Vector3.one * 0.16f;
        marker.GetComponent<Collider>().enabled = false;
        marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
        var pivot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pivot.transform.SetParent(diagnostics.transform);
        pivot.transform.position = Vector3.zero;
        pivot.transform.localScale = Vector3.one * 0.14f;
        pivot.GetComponent<Collider>().enabled = false;
        pivot.GetComponent<Renderer>().sharedMaterial = pivotMaterial;
        var lineMaterial = new Material(shader) { color = Color.yellow };
        var line = new GameObject("Near plane perimeter", typeof(LineRenderer)).GetComponent<LineRenderer>();
        line.transform.SetParent(diagnostics.transform);
        line.sharedMaterial = lineMaterial;
        line.startWidth = line.endWidth = 0.02f;
        line.loop = true;
        line.positionCount = 4;
        line.SetPositions(new[]
        {
            camera.ViewportToWorldPoint(new Vector3(0, 0, camera.nearClipPlane)),
            camera.ViewportToWorldPoint(new Vector3(1, 0, camera.nearClipPlane)),
            camera.ViewportToWorldPoint(new Vector3(1, 1, camera.nearClipPlane)),
            camera.ViewportToWorldPoint(new Vector3(0, 1, camera.nearClipPlane))
        });
        var overview = new GameObject("Clearance overview", typeof(Camera)).GetComponent<Camera>();
        overview.transform.SetParent(diagnostics.transform);
        overview.transform.position = new Vector3(-5, 8, -7);
        overview.transform.rotation = Quaternion.LookRotation(new Vector3(-0.4f, 0, -1) - overview.transform.position);
        overview.fieldOfView = 45;
        overview.clearFlags = CameraClearFlags.SolidColor;
        overview.backgroundColor = new Color(0.05f, 0.07f, 0.1f);
        try
        {
            SaveCameraImage(overview, name + "-Overview", 1600, 1000);
            diagnostics.SetActive(false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = overview.backgroundColor;
            SaveCameraImage(camera, name + "-POV", 1280, 540);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(diagnostics);
            wall.GetComponent<Renderer>().sharedMaterial = null;
            side.GetComponent<Renderer>().sharedMaterial = null;
            UnityEngine.Object.DestroyImmediate(wallMaterial);
            UnityEngine.Object.DestroyImmediate(markerMaterial);
            UnityEngine.Object.DestroyImmediate(pivotMaterial);
            UnityEngine.Object.DestroyImmediate(lineMaterial);
        }
    }

    private static void SaveCameraImage(Camera camera, string name, int width, int height)
    {
        var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        float oldAspect = camera.aspect;
        try
        {
            texture.Create();
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs/Round5OverlapImages");
            File.WriteAllBytes("Logs/Round5OverlapImages/" + name + ".png", image.EncodeToPNG());
            results.Add("IMAGE: Logs/Round5OverlapImages/" + name + ".png");
        }
        finally
        {
            camera.targetTexture = null;
            camera.aspect = oldAspect;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static NPCInteractable visitor, guide;
    private static PlayerController player;
    private static PlayerInteraction interaction;
    private static CrosshairReticle reticle;
    private static DialogueUIManager dialogue;
    private static int stage;
    private static double startedAt, nextStepAt;

    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Flag, true);
        ResumeIfRequested();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void ResumeIfRequested()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static InteractableOutline Aim() => (InteractableOutline)Call(reticle, "FindAimedInteractable");
    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (startedAt == 0) { startedAt = EditorApplication.timeSinceStartup; nextStepAt = startedAt + 2; }
        if (EditorApplication.timeSinceStartup - startedAt > 100) { Finish(new TimeoutException("NPC disabled test timed out")); return; }
        if (EditorApplication.timeSinceStartup < nextStepAt) return;
        nextStepAt = EditorApplication.timeSinceStartup + 0.35;
        try
        {
            switch (stage)
            {
                case 0:
                    visitor = GameObject.Find("GalleryVisitor").GetComponent<NPCInteractable>();
                    guide = GameObject.Find("GalleryGuide").GetComponent<NPCInteractable>();
                    player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                    interaction = player.GetComponent<PlayerInteraction>();
                    reticle = UnityEngine.Object.FindAnyObjectByType<CrosshairReticle>();
                    dialogue = DialogueUIManager.Instance;
                    Check(visitor.isActiveAndEnabled && guide.isActiveAndEnabled && dialogue != null && interaction != null && reticle != null,
                        "Saved scene NPCs and runtime interaction initialize");
                    Check(visitor.interactionCollider != null && !visitor.interactionCollider.isTrigger,
                        "Saved visitor uses a solid body distinct from its proximity volume");
                    var controller = player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    Vector3 destination = visitor.transform.position + visitor.transform.forward * 1.4f;
                    player.transform.SetPositionAndRotation(destination, Quaternion.LookRotation(visitor.transform.position - destination));
                    controller.enabled = true;
                    var follow = Camera.main.GetComponent<ThirdPersonCamera>();
                    follow.distance = 0;
                    follow.currentX = player.transform.eulerAngles.y;
                    guide.transform.position = visitor.transform.position - visitor.transform.forward * 0.9f;
                    Physics.SyncTransforms();
                    break;
                case 1:
                    Check(Aim() == visitor.GetComponent<InteractableOutline>(), "Ray/crosshair initially select the enabled front NPC");
                    visitor.enabled = false;
                    // Keep stale proximity deliberately: enabled-state filtering must suffice.
                    visitor.GetComponent<InteractableOutline>().SetProximity(true);
                    guide.GetComponent<InteractableOutline>().SetProximity(true);
                    int lastFrame = Get<int>(visitor, "lastInteractFrame");
                    visitor.TriggerDialogue();
                    Check(!dialogue.IsSpeaking && Get<int>(visitor, "lastInteractFrame") == lastFrame,
                        "Disabled direct entry rejects dialogue without consuming its per-instance frame guard");
                    interaction.TryInteract();
                    Check(!dialogue.IsSpeaking && Aim() == null, "Click dispatch and crosshair reject disabled front NPC and cannot see guide through its solid body");
                    break;
                case 2:
                    Call(interaction, "HandlePrimaryInteraction");
                    Check(!dialogue.IsSpeaking, "E/controller dispatch cannot open the disabled NPC or a guide behind its body");
                    Check(visitor.interactionCollider.enabled && visitor.gameObject.activeInHierarchy,
                        "Disabling only NPC component preserves its solid body collider");
                    visitor.interactionCollider.enabled = false;
                    Physics.SyncTransforms();
                    break;
                case 3:
                    Check(Aim() == guide.GetComponent<InteractableOutline>(), "With solid body removed, disabled NPC trigger volume does not hide enabled guide");
                    interaction.Interact();
                    Check(dialogue.IsConversationWith(guide), "Public mobile/click entry reaches guide through disabled proximity volume");
                    dialogue.EndDialogue();
                    visitor.interactionCollider.enabled = true;
                    visitor.enabled = true;
                    Physics.SyncTransforms();
                    break;
                case 4:
                    interaction.TryInteract();
                    Check(dialogue.IsConversationWith(visitor) && !player.enabled, "Re-enabled NPC opens dialogue normally and locks movement");
                    visitor.enabled = false;
                    Check(!dialogue.IsSpeaking && player.enabled, "Disabling component while speaking immediately cancels its dialogue and resumes movement");
                    break;
                case 5:
                    int previousFrame = Get<int>(visitor, "lastInteractFrame");
                    visitor.TriggerDialogue();
                    Check(!dialogue.IsSpeaking && Get<int>(visitor, "lastInteractFrame") == previousFrame,
                        "Disabled call leaves frame guard unchanged after an earlier conversation");
                    visitor.enabled = true;
                    visitor.TriggerDialogue();
                    Check(dialogue.IsConversationWith(visitor), "Re-enabling and calling entry point in the same frame still opens dialogue");
                    visitor.gameObject.SetActive(false);
                    Check(!dialogue.IsSpeaking, "Inactive GameObject cancels the conversation");
                    previousFrame = Get<int>(visitor, "lastInteractFrame");
                    visitor.TriggerDialogue();
                    Check(!dialogue.IsSpeaking && Get<int>(visitor, "lastInteractFrame") == previousFrame,
                        "Inactive GameObject also rejects direct entry without stamping the frame guard");
                    visitor.gameObject.SetActive(true);
                    break;
                case 6:
                    Call(interaction, "HandlePrimaryInteraction");
                    Check(dialogue.IsConversationWith(visitor), "Restored GameObject can be selected through primary input dispatch");
                    dialogue.EndDialogue();
                    Finish(null);
                    return;
            }
            stage++;
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Flag, false);
        results.Add(error == null ? "RESULT: PASS, 7 saved-scene stages / " + results.Count + " assertions; simulated input dispatch, real lifecycle/physics."
            : "RESULT: FAIL at stage " + stage + "\n" + error);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/NPCDisabledPlayModeResults.txt", results);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
