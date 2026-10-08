using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.XR.OpenXR;

// Disabled Editor-only features still serialize references into Player settings.
// Build a persistent runtime copy without types absent from the target assembly set.
// Never remove feature assets or change the original Editor settings array.
public sealed class OpenXRRuntimeSettingsBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    private const string Folder = "Assets/__RuntimeXRBuild";
    private const string StatePath = "Library/RuntimeOpenXRBuildState.json";
    public int callbackOrder => 10000; // XR Management adds its settings at order 0.

    [Serializable]
    private class State { public string assetPath, guid; public bool createdFolder; }

    public void OnPreprocessBuild(BuildReport report)
    {
        Cleanup();
        var source = OpenXRSettings.GetSettingsForBuildTargetGroup(report.summary.platformGroup);
        if (source == null) return;
        var assemblies = new HashSet<string>(CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(a => a.name));
        CreateRuntimeCopy(source, assemblies);
    }

    internal static OpenXRSettings CreateRuntimeCopy(OpenXRSettings source, HashSet<string> assemblies)
    {
        var features = source.GetFeatures();
        var unsupported = features.Where(f => f != null && !assemblies.Contains(f.GetType().Assembly.GetName().Name)).ToArray();
        if (unsupported.Length == 0) return null;
        if (unsupported.Any(f => f.enabled))
            throw new BuildFailedException("Enabled OpenXR feature is not compiled for the active Player target"
                + ": " + string.Join(", ", unsupported.Where(f => f.enabled).Select(f => f.GetType().FullName))
                + ". Select the matching Tools > Build Setup profile first.");

        bool createdFolder = !AssetDatabase.IsValidFolder(Folder);
        if (createdFolder) AssetDatabase.CreateFolder("Assets", "__RuntimeXRBuild");
        var clone = UnityEngine.Object.Instantiate(source);
        clone.name = "Filtered OpenXR runtime settings";
        string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/OpenXRRuntime.asset");
        AssetDatabase.CreateAsset(clone, path);
        File.WriteAllText(StatePath, JsonUtility.ToJson(new State {
            assetPath = path, guid = AssetDatabase.AssetPathToGUID(path), createdFolder = createdFolder
        }));
        EditorApplication.update -= Recover;
        EditorApplication.update += Recover;
        try
        {
            var serialized = new SerializedObject(clone);
            var array = serialized.FindProperty("features");
            var supported = features.Where(f => f != null && assemblies.Contains(f.GetType().Assembly.GetName().Name)).ToArray();
            array.arraySize = supported.Length;
            for (int i = 0; i < supported.Length; i++)
            {
                // Copy feature subassets too, so the build cannot pull unrelated
                // objects from the original multi-target settings asset.
                var feature = UnityEngine.Object.Instantiate(supported[i]);
                feature.name = supported[i].name;
                AssetDatabase.AddObjectToAsset(feature, clone);
                array.GetArrayElementAtIndex(i).objectReferenceValue = feature;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(clone);
            var preload = PlayerSettings.GetPreloadedAssets().Where(a => !(a is OpenXRSettings)).ToList();
            preload.Add(clone);
            PlayerSettings.SetPreloadedAssets(preload.ToArray());
            Debug.Log("[OpenXRRuntimeSettingsBuild] Filtered " + unsupported.Length + " unsupported disabled feature references: "
                + string.Join(", ", unsupported.Select(f => f.GetType().FullName)));
            return clone;
        }
        catch { Cleanup(); throw; }
    }

    public void OnPostprocessBuild(BuildReport report) => Cleanup();

    [InitializeOnLoadMethod]
    private static void ScheduleRecovery()
    {
        if (!File.Exists(StatePath)) return;
        EditorApplication.update -= Recover;
        EditorApplication.update += Recover;
    }

    private static void Recover()
    {
        if (BuildPipeline.isBuildingPlayer) return;
        EditorApplication.update -= Recover;
        Cleanup();
    }

    public static void Cleanup()
    {
        if (!File.Exists(StatePath)) return;
        var state = JsonUtility.FromJson<State>(File.ReadAllText(StatePath));
        // Delete only the asset created by this build, including after domain reload.
        if (state == null || string.IsNullOrEmpty(state.assetPath)
            || Path.GetDirectoryName(Path.GetFullPath(state.assetPath)) != Path.GetFullPath(Folder)
            || !Path.GetFileName(state.assetPath).StartsWith("OpenXRRuntime", StringComparison.Ordinal)
            || Path.GetExtension(state.assetPath) != ".asset"
            || AssetDatabase.AssetPathToGUID(state.assetPath) != state.guid)
            throw new InvalidOperationException("Cannot safely identify the temporary OpenXR build asset. Inspect " + StatePath);
        var preload = PlayerSettings.GetPreloadedAssets().Where(a => a == null || AssetDatabase.GetAssetPath(a) != state.assetPath).ToArray();
        PlayerSettings.SetPreloadedAssets(preload);
        AssetDatabase.DeleteAsset(state.assetPath);
        File.Delete(StatePath);
        if (state.createdFolder && Directory.Exists(Folder) && !Directory.EnumerateFileSystemEntries(Folder).Any())
            AssetDatabase.DeleteAsset(Folder);
    }
}
