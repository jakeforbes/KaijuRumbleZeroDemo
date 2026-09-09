using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies the artist's import settings to the Uries frames. Adapted from the
/// UriesSpriteImporter.cs shipped with the art package — same settings, pointed at
/// where the frames live in this project.
///
/// Runs once automatically after the frames are first imported, and can be re-run
/// from the menu if settings ever drift.
/// </summary>
public static class UriesTextureImporter
{
    const string Root = "Assets/KRZ/Resources/Uries";
    const string DoneKey = "KRZ_UriesTexturesConfigured";

    [InitializeOnLoadMethod]
    static void AutoConfigureOnce()
    {
        if (SessionState.GetBool(DoneKey, false)) return;
        EditorApplication.delayCall += () =>
        {
            if (!AssetDatabase.IsValidFolder(Root)) return;
            SessionState.SetBool(DoneKey, true);
            if (Configure(silent: true) > 0)
                Debug.Log("KRZ: Uries frames configured on first import.");
        };
    }

    [MenuItem("KRZ/Configure Uries Textures")]
    static void ConfigureFromMenu() =>
        Debug.Log($"KRZ: configured {Configure(silent: false)} Uries textures.");

    static int Configure(bool silent)
    {
        if (!AssetDatabase.IsValidFolder(Root))
        {
            if (!silent) Debug.LogWarning($"KRZ: {Root} not found.");
            return 0;
        }

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root });
        int changed = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                // Already correct: skip, so re-running is cheap.
                if (importer.textureType == TextureImporterType.Sprite &&
                    Mathf.Approximately(importer.spritePixelsPerUnit, 128f) &&
                    importer.alphaIsTransparency &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 128f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 512;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, 0.12f);
                settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);

                EditorUtility.SetDirty(importer);
                changed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        return changed;
    }
}
