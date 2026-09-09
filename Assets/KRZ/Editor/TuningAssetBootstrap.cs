using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the Tuning asset automatically on first compile so there is no manual
/// setup step. Never overwrites an existing one, so tuning survives everything.
/// </summary>
[InitializeOnLoad]
public static class TuningAssetBootstrap
{
    const string Dir = "Assets/KRZ/Resources";
    const string Path = Dir + "/Tuning.asset";

    static TuningAssetBootstrap() => EditorApplication.delayCall += Ensure;

    [MenuItem("KRZ/Select Tuning Asset")]
    static void Select() => Selection.activeObject = Ensure();

    static Tuning Ensure()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Tuning>(Path);
        if (existing != null) return existing;

        if (!Directory.Exists(Dir))
        {
            Directory.CreateDirectory(Dir);
            AssetDatabase.Refresh();
        }

        var asset = ScriptableObject.CreateInstance<Tuning>();
        AssetDatabase.CreateAsset(asset, Path);
        AssetDatabase.SaveAssets();
        Debug.Log($"KRZ: created {Path}");
        return asset;
    }
}
