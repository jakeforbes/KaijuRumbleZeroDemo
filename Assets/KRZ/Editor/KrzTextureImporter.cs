using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies the art spec to every frame under Resources as it is imported.
///
/// This exists for one setting in particular. Every package ships straight alpha
/// with black in the transparent pixels, and Unity's default import leaves
/// alphaIsTransparency off, which means no colour is bled outward under the matte.
/// Filtering then samples that black and draws a dark rim around the sprite. The
/// tank hides it because the vehicle is nearly black at its edges anyway; brighter
/// art would not.
///
/// A postprocessor rather than a menu command, because it runs on first import.
/// A fresh clone with no .meta files gets the right settings without anyone
/// remembering to run anything — which is the same reason the sprites themselves
/// are built in code rather than loaded as Sprite assets.
///
/// Deliberately silent about compression: UriesTextureImporter forces the kaiju
/// frames uncompressed, and nothing here should fight it. Everything else stays on
/// Unity's default, because uncompressed 512s across the whole roster is hundreds
/// of megabytes of texture memory for a prototype.
/// </summary>
public class KrzTextureImporter : AssetPostprocessor
{
    const string Root = "Assets/KRZ/Resources/";

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root)) return;

        var t = (TextureImporter)assetImporter;

        t.alphaIsTransparency = true;
        t.alphaSource = TextureImporterAlphaSource.FromInput;
        t.mipmapEnabled = false;
        t.filterMode = FilterMode.Bilinear;
        t.wrapMode = TextureWrapMode.Clamp;

        // Sprite at the project's own PPU, so a frame dragged into a scene by hand
        // lands at the right size. The pivot is left alone on purpose: buildings and
        // characters need different ones, and nothing reads it — Sprite.Create is
        // handed a pivot at runtime.
        t.textureType = TextureImporterType.Sprite;
        t.spriteImportMode = SpriteImportMode.Single;
        t.spritePixelsPerUnit = 128f;
    }
}
