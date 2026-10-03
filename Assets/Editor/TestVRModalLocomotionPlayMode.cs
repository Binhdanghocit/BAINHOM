using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// Real saved rig/provider lifecycle in Play Mode, without physical XR input.
public static class TestVRModalLocomotionPlayMode
{
    private const string Flag = "BAINHOM.VRModalPlayMode";
    private const string RigPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> results = new List<string>();
    private static readonly Dictionary<LocomotionProvider, bool> initial = new Dictionary<LocomotionProvider, bool>();
    private static readonly Dictionary<Behaviour, bool> tracking = new Dictionary<Behaviour, bool>();
    private static Dictionary<LocomotionProvider, bool> persistentStates;
    private static ViewModeController mode;
    private static SettingsManager settings;
    private static DialogueUIManager dialogue;
    private static NPCInteractable npc;
    private static MinigameTrigger table;
    private static DoorMenuTrigger door;
    private static GameObject originalRig, replacementRig, persistentRig, separateWorkshop, savedDoorWorkshop;
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

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.Add("PASS: " + message);
    }

    private static VRModalLocomotionLock Guard() => UnityEngine.Object.FindAnyObjectByType<VRModalLocomotionLock>();
    private static Dictionary<LocomotionProvider, bool> Snapshots()
        => (Dictionary<LocomotionProvider, bool>)typeof(VRModalLocomotionLock).GetField("originalStates", Private).GetValue(Guard());
    private static bool PauseRequested()
        => (bool)typeof(VRModalLocomotionLock).GetField("pauseRequested", Private).GetValue(Guard());

    private static void Remember(GameObject rig)
    {
        foreach (var provider in rig.GetComponentsInChildren<LocomotionProvider>(true)) initial.Add(provider, provider.enabled);
        foreach (var component in rig.GetComponentsInChildren<Behaviour>(true))
            if (component is Camera || component.GetType().Name == "TrackedPoseDriver") tracking.Add(component, component.enabled);
    }

    private static void CheckLocked(string label)
    {
        Check(initial.Keys.Where(p => p != null).All(p => !p.enabled), label + ": all rig locomotion providers disabled");
        Check(initial.Where(p => p.Key != null).All(p => Snapshots().TryGetValue(p.Key, out bool value) && value == p.Value),
            label + ": first enabled-state snapshot preserved");
        Check(tracking.Where(p => p.Key != null).All(p => p.Key.enabled == p.Value) && VRUIInputBridge.EnsureInstance().enabled,
            label + ": tracking, HMD cameras and pointer remain enabled as before");
    }

    private static void CheckRestored(string label)
    {
        Check(initial.Where(p => p.Key != null).All(p => p.Key.enabled == p.Value), label + ": original states restored, including disabled providers");
        Check(Snapshots().Count == 0 && !PauseRequested(), label + ": snapshot cleared");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (startedAt == 0) { startedAt = EditorApplication.timeSinceStartup; nextStepAt = startedAt + 2; }
        if (EditorApplication.timeSinceStartup - startedAt > 150) { Finish(new TimeoutException("VR modal test timed out.")); return; }
        if (EditorApplication.timeSinceStartup < nextStepAt) return;
        nextStepAt = EditorApplication.timeSinceStartup + 0.5;
        try
        {
            switch (stage)
            {
                case 0:
                    mode = UnityEngine.Object.FindAnyObjectByType<ViewModeController>();
                    settings = UnityEngine.Object.FindAnyObjectByType<SettingsManager>();
                    dialogue = DialogueUIManager.Instance;
                    npc = GameObject.Find("GalleryVisitor").GetComponent<NPCInteractable>();
                    table = GameObject.Find("GalleryGuide").GetComponent<NPCInteractable>().workshopTrigger;
                    door = UnityEngine.Object.FindObjectsByType<DoorMenuTrigger>().First(d => d.doorMenuUI != null);
                    originalRig = mode.vrRig;
                    var providers = originalRig.GetComponentsInChildren<LocomotionProvider>(true);
                    Check(providers.Length >= 7, "Saved rig contains move/turn/grab/teleport providers: " + string.Join(", ", providers.Select(p => p.GetType().Name)));
                    providers[0].enabled = false;
                    Remember(originalRig);
                    settings.OpenPanel();
                    CheckLocked("Settings");
                    break;
                case 1:
                    dialogue.StartDialogue(npc);
                    settings.ClosePanel();
                    CheckLocked("Settings closed, dialogue still open");
                    Check(!ViewModeController.TryResumeGameplayIfClear(), "Explicit resume rejected while another modal is open");
                    break;
                case 2:
                    dialogue.EndDialogue();
                    CheckRestored("Last dialogue closed");
                    dialogue.StartDialogue(npc);
                    settings.OpenPanel();
                    CheckLocked("Dialogue plus Settings");
                    dialogue.EndDialogue();
                    CheckLocked("Dialogue closed, Settings remains");
                    break;
                case 3:
                    settings.ClosePanel();
                    CheckRestored("Last Settings closed");
                    table.OpenMinigame();
                    Check(table.IsMinigameOpen, "Workshop opens through its saved table owner");
                    CheckLocked("Workshop");
                    settings.OpenPanel();
                    break;
                case 4:
                    settings.ClosePanel();
                    CheckLocked("Settings closed, workshop remains");
                    table.CloseMinigame();
                    CheckRestored("Workshop closed");
                    door.ToggleDoorMenu();
                    Check(door.IsOpen, "Saved door menu opens");
                    CheckLocked("Door menu");
                    settings.OpenPanel();
                    break;
                case 5:
                    door.StayInGallery();
                    CheckLocked("Door menu closed, Settings remains");
                    settings.ClosePanel();
                    CheckRestored("Door plus Settings closed");
                    savedDoorWorkshop = door.minigameUI;
                    separateWorkshop = new GameObject("Separate door workshop test");
                    separateWorkshop.SetActive(false);
                    door.minigameUI = separateWorkshop;
                    door.OpenMinigame();
                    Check(MinigameTrigger.IsAnyOpen, "Door-owned separate workshop registers its modal");
                    CheckLocked("Separate door workshop");
                    break;
                case 6:
                    door.CloseMinigame();
                    door.minigameUI = savedDoorWorkshop;
                    UnityEngine.Object.Destroy(separateWorkshop);
                    CheckRestored("Separate door workshop closed");
                    settings.OpenPanel();
                    originalRig.SetActive(true);
                    break;
                case 7:
                    CheckLocked("Inactive saved rig activated while modal remains");
                    var inactiveParent = new GameObject("Inactive replacement rig parent");
                    inactiveParent.SetActive(false);
                    replacementRig = UnityEngine.Object.Instantiate(originalRig, inactiveParent.transform);
                    var oldProviders = originalRig.GetComponentsInChildren<LocomotionProvider>(true);
                    var newProviders = replacementRig.GetComponentsInChildren<LocomotionProvider>(true);
                    for (int i = 0; i < newProviders.Length; i++) newProviders[i].enabled = initial[oldProviders[i]];
                    Remember(replacementRig);
                    mode.vrRig = replacementRig;
                    replacementRig.transform.SetParent(null, false);
                    replacementRig.SetActive(true);
                    UnityEngine.Object.Destroy(inactiveParent);
                    break;
                case 8:
                    CheckLocked("Replacement rig discovered before provider Update");
                    var mediator = replacementRig.GetComponentInChildren<LocomotionMediator>(true);
                    var lateProviderObject = new GameObject("Late teleport provider");
                    lateProviderObject.transform.SetParent(mediator.transform, false);
                    var lateProvider = lateProviderObject.AddComponent<TeleportationProvider>();
                    Check(lateProvider.enabled, "New teleport provider starts enabled before being discovered");
                    initial.Add(lateProvider, true);
                    break;
                case 9:
                    CheckLocked("Provider added after modal opens");
                    initial.First(p => p.Key != null && !p.Value).Key.enabled = true;
                    break;
                case 10:
                    CheckLocked("External re-enable cannot overwrite first snapshot");
                    settings.ClosePanel();
                    CheckRestored("Rig switch modal closed");
                    originalRig.SetActive(false);
                    settings.OpenPanel();
                    UnityEngine.Object.Destroy(originalRig);
                    break;
                case 11:
                    Check(Snapshots().Keys.All(p => p != null), "Destroyed rig references pruned while modal remains");
                    settings.GoToMainMenu();
                    break;
                case 12:
                    if (SceneManager.GetActiveScene().name != "MainMenu") return;
                    Check(Snapshots().Count == 0 && !PauseRequested(), "Single scene change clears previous scene lock");
                    persistentRig = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
                    UnityEngine.Object.DontDestroyOnLoad(persistentRig);
                    var persistentProviders = persistentRig.GetComponentsInChildren<LocomotionProvider>(true);
                    persistentProviders[0].enabled = false;
                    persistentStates = persistentProviders.ToDictionary(p => p, p => p.enabled);
                    settings = UnityEngine.Object.FindAnyObjectByType<SettingsManager>();
                    settings.OpenPanel();
                    Check(persistentProviders.All(p => !p.enabled), "MainMenu without ViewModeController locks its XR rig");
                    settings.ClosePanel();
                    Check(persistentStates.All(p => p.Key.enabled == p.Value) && Snapshots().Count == 0,
                        "MainMenu Settings close restores original provider states immediately");
                    break;
                case 13:
                    settings.OpenPanel();
                    Check(persistentStates.Keys.All(p => !p.enabled), "Persistent rig locked before next scene load");
                    SceneManager.LoadScene("SampleScene");
                    break;
                case 14:
                    if (SceneManager.GetActiveScene().name != "SampleScene") return;
                    Check(persistentStates.All(p => p.Key.enabled == p.Value) && Snapshots().Count == 0 && !PauseRequested(),
                        "Persistent rig survives scene change without inheriting old modal lock");
                    mode = UnityEngine.Object.FindAnyObjectByType<ViewModeController>();
                    settings = UnityEngine.Object.FindAnyObjectByType<SettingsManager>();
                    var freshStates = mode.vrRig.GetComponentsInChildren<LocomotionProvider>(true).ToDictionary(p => p, p => p.enabled);
                    settings.OpenPanel();
                    Check(freshStates.Keys.All(p => !p.enabled), "New scene rig can acquire a fresh modal lock");
                    settings.ClosePanel();
                    Check(freshStates.All(p => p.Key.enabled == p.Value), "New scene rig restores its own snapshot, not the previous scene's");
                    UnityEngine.Object.Destroy(persistentRig);
                    Finish(null);
                    return;
            }
            stage++;
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Flag, false);
        results.Add(error == null
            ? "RESULT: PASS, 15 Play Mode stages. Saved XRI providers, nested modals, late activation/replacement/provider creation, disabled-state preservation, destroyed references and single-scene/persistent-rig cleanup checked. Enabled-state/lifecycle checks only; no physical/simulated XR move/turn/teleport input or headset rendering."
            : "RESULT: FAIL at stage " + stage + "\n" + error);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/VRModalLocomotionPlayModeResults.txt", results);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
