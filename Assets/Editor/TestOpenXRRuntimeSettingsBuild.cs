using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.XR.OpenXR;

public static class TestOpenXRRuntimeSettingsBuild
{
    public static void Run()
    {
        if (!Application.isBatchMode || EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            throw new InvalidOperationException("Use a batchmode Windows project copy.");
        var results = new List<string>();
        void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); results.Add("PASS: " + label); }
        var source = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
        var assemblyNames = new HashSet<string>(CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(a => a.name));
        string path = AssetDatabase.GetAssetPath(source);
        byte[] before = File.ReadAllBytes(path);
        string beforeJson = EditorJsonUtility.ToJson(source);
        var originalFeatures = source.GetFeatures();
        var preloaded = PlayerSettings.GetPreloadedAssets();
        try
        {
            // Equivalent to XRBuildHelper's preload registration at order 0.
            PlayerSettings.SetPreloadedAssets(preloaded.Where(a => !(a is OpenXRSettings)).Concat(new UnityEngine.Object[] { source }).ToArray());
            var clone = OpenXRRuntimeSettingsBuild.CreateRuntimeCopy(source, assemblyNames);
            Check(clone != null && clone != source, "Windows creates a separate runtime settings asset");
            var filtered = clone.GetFeatures();
            Check(filtered.Length < originalFeatures.Length, "Unsupported Meta references are actually filtered");
            Check(filtered.All(f => assemblyNames.Contains(f.GetType().Assembly.GetName().Name)), "Every runtime feature type belongs to Windows Player assemblies");
            Check(filtered.All(f => !originalFeatures.Contains(f)), "Supported feature subassets are also copied");
            Check(filtered.Any(f => f.enabled && f.GetType().Name == "OculusTouchControllerProfile"), "Enabled controller profile survives copying");
            Check(PlayerSettings.GetPreloadedAssets().Contains(clone) && !PlayerSettings.GetPreloadedAssets().Contains(source), "Only filtered settings go into runtime preload");
            Check(source.GetFeatures().SequenceEqual(originalFeatures) && EditorJsonUtility.ToJson(source) == beforeJson,
                "Original Editor settings, feature values and array order stay intact");
            Check(File.ReadAllBytes(path).SequenceEqual(before), "Original multi-target settings file is byte-identical");
            string tempPath = AssetDatabase.GetAssetPath(clone);
            OpenXRRuntimeSettingsBuild.Cleanup();
            Check(!File.Exists(tempPath) && !File.Exists(tempPath + ".meta"), "Cleanup removes only generated settings and feature copies");
            Check(!File.Exists("Library/RuntimeOpenXRBuildState.json"), "Cleanup removes recovery state");
            Check(!PlayerSettings.GetPreloadedAssets().Any(a => a != null && AssetDatabase.GetAssetPath(a).StartsWith("Assets/__RuntimeXRBuild/")), "No temporary preload remains");
            PlayerSettings.SetPreloadedAssets(preloaded.Where(a => !(a is OpenXRSettings)).Concat(new UnityEngine.Object[] { source }).ToArray());
            clone = OpenXRRuntimeSettingsBuild.CreateRuntimeCopy(source, assemblyNames);
            // Simulate an aborted build after Prepare, invoking the same public recovery path.
            OpenXRRuntimeSettingsBuild.Cleanup();
            Check(source.GetFeatures().SequenceEqual(originalFeatures) && File.ReadAllBytes(path).SequenceEqual(before), "Abort recovery preserves original settings and references");
            Check(!AssetDatabase.IsValidFolder("Assets/__RuntimeXRBuild"), "Fresh temporary folder is removed after cleanup");
            File.WriteAllLines("Logs/OpenXRRuntimeSettingsResults.txt", results);
            Debug.Log("[OpenXRRuntimeSettings] PASS " + results.Count + " assertions");
        }
        finally
        {
            OpenXRRuntimeSettingsBuild.Cleanup();
            PlayerSettings.SetPreloadedAssets(preloaded);
        }
    }
}
