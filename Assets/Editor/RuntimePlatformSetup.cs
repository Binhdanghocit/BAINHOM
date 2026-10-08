using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

// Explicit profiles: ordinary Android must not inherit a Quest-required manifest.
public static class RuntimePlatformSetup
{
    [MenuItem("Tools/Build Setup/Windows + optional PCVR")]
    public static void Windows() => Configure(BuildTargetGroup.Standalone, true, false);

    [MenuItem("Tools/Build Setup/Android Touch")]
    public static void AndroidTouch() => Configure(BuildTargetGroup.Android, false, false);

    [MenuItem("Tools/Build Setup/Quest OpenXR")]
    public static void Quest() => Configure(BuildTargetGroup.Android, true, true);

    public static void Configure(BuildTargetGroup group, bool xr, bool quest)
    {
        // GameplayInput and XRI both use Input System. Changing this can request
        // an Editor restart; save open work before invoking these setup commands.
        var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var input = playerSettings.FindProperty("activeInputHandler");
        if (input == null) throw new InvalidOperationException("Cannot find Active Input Handling setting.");
        input.intValue = 1;
        playerSettings.ApplyModifiedPropertiesWithoutUndo();

        XRGeneralSettings general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
        if (general == null || general.Manager == null)
            throw new InvalidOperationException("XR settings are missing for " + group);
        general.InitManagerOnStart = false;
        general.Manager.automaticLoading = false;
        general.Manager.automaticRunning = false;
        if (!general.Manager.TrySetLoaders(new List<XRLoader>()))
            throw new InvalidOperationException("Could not clear XR loaders for " + group);
        if (xr && !XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group))
            throw new InvalidOperationException("Could not assign OpenXR loader for " + group);

        OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
        if (settings == null) throw new InvalidOperationException("OpenXR settings missing for " + group);
        bool hasQuestSupport = false;
        foreach (var feature in settings.GetFeatures())
        {
            if (feature == null) continue;
            // This gallery needs controller input and Quest support, not AR/MR
            // subsystems or Android XR device extensions on desktop/phone.
            string type = feature.GetType().FullName;
            if (type == "UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature") hasQuestSupport = true;
            feature.enabled = xr && (feature is OculusTouchControllerProfile
                || (quest && type == "UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature"));
            EditorUtility.SetDirty(feature);
        }
        if (quest && !hasQuestSupport) throw new InvalidOperationException("Meta Quest Support feature is missing; refresh OpenXR settings first.");
        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(general.Manager);
        EditorUtility.SetDirty(general);
        if (group == BuildTargetGroup.Android)
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if ((int)PlayerSettings.Android.minSdkVersion < 32)
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[RuntimePlatformSetup] " + group + ": Input System, manual XR lifecycle, OpenXR=" + xr + ", Quest=" + quest);
    }
}
