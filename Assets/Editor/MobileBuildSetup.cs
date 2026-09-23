using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// One-click mobile build setup: chạy 1 lần là ra đủ setting để build Android.
// Menu: Tools/Setup Mobile Build (Android)
public static class MobileBuildSetup
{
    private static readonly string[] RequiredScenes = { "MainMenu", "SampleScene" };

    [MenuItem("Tools/Setup Mobile Build (Android)")]
    public static void SetupAndroidBuild()
    {
        EnsureScenesInBuild();
        ApplyAndroidPlayerSettings();
        SwitchToAndroidTarget();
        LogSummary();
    }

    // Đảm bảo MainMenu (index 0) + Gallery (index 1) có trong Build Settings.
    private static void EnsureScenesInBuild()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        foreach (string sceneName in RequiredScenes)
        {
            string guid = FindSceneGuid(sceneName);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError("[MobileBuild] Không tìm thấy scene: " + sceneName);
                continue;
            }
            scenes.Add(new EditorBuildSettingsScene(AssetDatabase.GUIDToAssetPath(guid), true));
        }
        // Giữ lại scene lạ mà user đã thêm (nếu có), tránh xóa nhầm.
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            bool exists = false;
            foreach (EditorBuildSettingsScene kept in scenes)
            {
                if (kept.path == s.path) { exists = true; break; }
            }
            if (!exists) scenes.Add(s);
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[MobileBuild] Scenes in build: " + string.Join(", ", ScenePaths(scenes)));
    }

    private static string FindSceneGuid(string sceneName)
    {
        string[] guids = AssetDatabase.FindAssets(sceneName + " t:Scene");
        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (path.EndsWith("/" + sceneName + ".unity")) return g;
        }
        return guids.Length > 0 ? guids[0] : null;
    }

    private static string[] ScenePaths(List<EditorBuildSettingsScene> scenes)
    {
        string[] paths = new string[scenes.Count];
        for (int i = 0; i < scenes.Count; i++) paths[i] = scenes[i].path;
        return paths;
    }

    // Setting Android tối thiểu để lên Play Store / máy thật:
    // IL2CPP + ARM64 + minSdk 26. Không đụng orientation, graphics, XR.
    private static void ApplyAndroidPlayerSettings()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        if ((int)PlayerSettings.Android.minSdkVersion < (int)AndroidSdkVersions.AndroidApiLevel26)
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;

        Debug.Log("[MobileBuild] ScriptingBackend=IL2CPP, Arch=ARM64, minSdk="
            + PlayerSettings.Android.minSdkVersion
            + ", bundleId=" + PlayerSettings.applicationIdentifier);
    }

    private static void SwitchToAndroidTarget()
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
        {
            Debug.Log("[MobileBuild] Đã ở Android target, không cần switch.");
            return;
        }
        EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
        Debug.Log("[MobileBuild] Đang chuyển platform sang Android (đợi reimport xong)...");
    }

    private static void LogSummary()
    {
        Debug.Log("[MobileBuild] XONG. Bước tiếp theo: mở Build Profiles (File > Build Profiles), " +
            "chọn Android, bấm Build. FPS mobile mặc định 60, không có 90 (đã xử lý trong SettingsManager).");
    }
}
