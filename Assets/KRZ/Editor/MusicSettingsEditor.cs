using UnityEditor;
using UnityEngine;

public static class MusicSettingsEditor
{
    [MenuItem("KRZ/Audio/Background Music")]
    static void Select()
    {
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<MusicSettings>("Assets/KRZ/Resources/Music Settings.asset");
        EditorGUIUtility.PingObject(Selection.activeObject);
    }
}
