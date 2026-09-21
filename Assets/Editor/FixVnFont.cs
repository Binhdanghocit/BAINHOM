using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TMPro;

// One-shot utility: bake Vietnamese glyphs into LiberationSans SDF.
// Run headless: Unity.exe -batchmode -nographics -projectPath <proj>
//   -executeMethod FixVnFont.AddVietnameseGlyphs -quit -logFile <file>
public static class FixVnFont
{
    private const string FontAssetPath =
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    // Full Vietnamese alphabet with all tones + a few common punctuation marks.
    private const string VietnameseChars =
        "ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝàáâãèéêìíòóôõùúý" +
        "ĂăĐđĨĩŨũƠơƯư" +
        "ẠạẢảẤấẦầẨẩẪẫẬậẮắẰằẲẳẴẵẶặ" +
        "ẸẹẺẻẼẽẾếỀềỂểỄễỆệỈỉỊị" +
        "ỌọỎỏỐốỒồỔổỖỗỘộỚớỜờỞởỠỡỢợ" +
        "ỤụỦủỨứỪừỬửỮữỰựỲỳỴỵỶỷỸỹ" +
        "’“”…" + "–—";

    [MenuItem("Tools/Fix Vietnamese Font (LiberationSans)")]
    public static void AddVietnameseGlyphs()
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null)
        {
            Debug.LogError("[FixVnFont] Font asset not found at " + FontAssetPath);
            return;
        }

        // The asset is currently Static, and TryAddCharacters refuses Static
        // assets. Switching to Dynamic also restores the (currently null)
        // m_SourceFontFile reference from the stored editor reference.
        if (fontAsset.atlasPopulationMode == AtlasPopulationMode.Static)
        {
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            Debug.Log("[FixVnFont] Switched AtlasPopulationMode Static -> Dynamic.");
        }

        fontAsset.isMultiAtlasTexturesEnabled = true;

        var unicodes = new List<uint>();
        foreach (char c in VietnameseChars)
        {
            uint id = c;
            if (!unicodes.Contains(id))
                unicodes.Add(id);
        }

        Debug.Log($"[FixVnFont] Requesting {unicodes.Count} Vietnamese glyphs " +
                  $"(atlas count before: {fontAsset.atlasTextureCount}).");

        bool ok = false;
        uint[] missing = System.Array.Empty<uint>();
        try
        {
            ok = fontAsset.TryAddCharacters(unicodes.ToArray(), out uint[] m);
            if (m != null)
                missing = m;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[FixVnFont] Bake failed: " + e.Message);
            return;
        }

        Debug.Log($"[FixVnFont] TryAddCharacters ok={ok}, missing={missing.Length}, " +
                  $"atlas count after: {fontAsset.atlasTextureCount}.");
        foreach (uint m in missing)
            Debug.LogWarning($"[FixVnFont] Missing glyph U+{m:X4} not in source font.");

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        Debug.Log("[FixVnFont] Done.");
    }
}
