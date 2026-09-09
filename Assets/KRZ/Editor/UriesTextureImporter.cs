using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies the artist's import settings to the Uries frames. Adapted from the
/// UriesSpriteImporter.cs shipped with the art package — same settings, pointed at
/// where the frames live in this project.
///
/// Runs automatically whenever it finds frames that are not yet configured, so it
/// is self-healing rather than relying on a "have I run before" flag.
/// </summary>
public static class UriesTextureImporter
{
    const string Root = "Assets/KRZ/Resources/Uries";

    [InitializeOnLoadMethod]
    static void AutoConfigure()
    {
        EditorApplication.delayCall += () =>
        {
            if (!AssetDatabase.IsValidFolder(Root)) return;
            if (CountUnconfigured() == 0) return;
            Configure();
        };
    }

    [MenuItem("KRZ/Configure Uries Textures")]
    static void ConfigureFromMenu() => Configure();

    /// <summary>Cheap probe so the automatic pass costs nothing once settings are right.</summary>
    static int CountUnconfigured()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root });
        int n = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is TextureImporter t && NeedsWork(t)) n++;
        }
        return n;
    }

    static bool NeedsWork(TextureImporter t) =>
        t.textureType != TextureImporterType.Sprite ||
        !Mathf.Approximately(t.spritePixelsPerUnit, 128f) ||
        !t.alphaIsTransparency ||
        t.textureCompression != TextureImporterCompression.Uncompressed;

    static void Configure()
    {
        if (!AssetDatabase.IsValidFolder(Root))
        {
            Debug.LogWarning($"KRZ: {Root} not found.");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root });
        int changed = 0;

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                if (!NeedsWork(importer)) continue;

                if (EditorUtility.DisplayCancelableProgressBar(
                        "Configuring Uries frames",
                        $"{i + 1} / {guids.Length}   {System.IO.Path.GetFileName(path)}",
                        (i + 1) / (float)guids.Length))
                    break;

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

                // SaveAndReimport is what actually writes the .meta and rebuilds the
                // asset. SetDirty alone leaves the settings unapplied — which is what
                // silently left every frame as a plain texture the first time round.
                importer.SaveAndReimport();
                changed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"KRZ: configured {changed} Uries textures at 128 PPU, pivot (0.5, 0.12).");
    }
}
