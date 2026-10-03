using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

// Runs before XRI locomotion providers (-210). Tracking and UI input remain enabled.
[DefaultExecutionOrder(-211)]
public sealed class VRModalLocomotionLock : MonoBehaviour
{
    private static VRModalLocomotionLock instance;
    private readonly Dictionary<LocomotionProvider, bool> originalStates = new Dictionary<LocomotionProvider, bool>();
    private readonly List<LocomotionProvider> destroyedProviders = new List<LocomotionProvider>();
    private bool pauseRequested;
    private bool applyingStates;

    public static void Pause()
    {
        if (instance == null)
            instance = new GameObject("VR Modal Locomotion Lock").AddComponent<VRModalLocomotionLock>();
        instance.pauseRequested = true;
        instance.LockRigProviders();
    }

    // The caller must first check all modal owners through ViewModeController.
    internal static void Resume()
    {
        if (instance != null) instance.RestoreProviders();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            enabled = false;
            if (Application.isPlaying) Destroy(gameObject);
            return;
        }
        instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void Update()
    {
        if (!pauseRequested) return;
        if (ViewModeController.IsBlockingModalOpen()) LockRigProviders();
        else RestoreProviders();
    }

    private void LockRigProviders()
    {
        if (applyingStates) return;
        PruneDestroyedProviders();
        var modes = FindObjectsByType<ViewModeController>(FindObjectsInactive.Include);
        foreach (var provider in FindObjectsByType<LocomotionProvider>(FindObjectsInactive.Include))
        {
            bool belongsToRig = provider.GetComponentInParent<XROrigin>(true) != null;
            if (!belongsToRig)
            {
                foreach (var mode in modes)
                {
                    if (mode.vrRig != null && provider.transform.IsChildOf(mode.vrRig.transform))
                    {
                        belongsToRig = true;
                        break;
                    }
                }
            }
            if (!belongsToRig) continue;
            // Include inactive rigs so activating/switching them cannot overwrite the snapshot.
            if (!originalStates.ContainsKey(provider)) originalStates.Add(provider, provider.enabled);
            provider.enabled = false;
        }
    }

    private void RestoreProviders()
    {
        if (applyingStates) return;
        applyingStates = true;
        pauseRequested = false;
        try
        {
            foreach (var state in originalStates)
                if (state.Key != null) state.Key.enabled = state.Value;
            originalStates.Clear();
        }
        finally { applyingStates = false; }
    }

    private void PruneDestroyedProviders()
    {
        destroyedProviders.Clear();
        foreach (var state in originalStates)
            if (state.Key == null) destroyedProviders.Add(state.Key);
        foreach (var provider in destroyedProviders) originalStates.Remove(provider);
        destroyedProviders.Clear();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // End the previous scene's lock, including any rig surviving via DontDestroyOnLoad.
        if (mode == LoadSceneMode.Single) RestoreProviders();
        if (ViewModeController.IsBlockingModalOpen())
        {
            pauseRequested = true;
            LockRigProviders();
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        PruneDestroyedProviders();
        if (pauseRequested && !ViewModeController.IsBlockingModalOpen()) RestoreProviders();
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        RestoreProviders();
        instance = null;
    }
}
