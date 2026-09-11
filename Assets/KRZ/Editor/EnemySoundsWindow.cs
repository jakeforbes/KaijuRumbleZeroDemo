using UnityEditor;
using UnityEngine;

public sealed class EnemySoundsWindow : EditorWindow
{
    Vector2 scroll;

    [MenuItem("KRZ/Audio/Enemy Sounds by Type")]
    static void Open() => GetWindow<EnemySoundsWindow>("Enemy Sounds by Type");

    void OnGUI()
    {
        var tuning = Resources.Load<Tuning>("Tuning");
        if (tuning == null) return;
        EditorGUILayout.HelpBox("Each unit has independent sound settings. Select a unit to edit its clips and effects in the Inspector. Enemy Attack includes missile volleys; Enemy Deploy is used by troop carriers. Restart Play after editing templates.", MessageType.Info);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var type in tuning.enemyTypes)
        {
            if (type == null) continue;
            var sound = type.sounds != null ? type.sounds
                : Resources.Load<SoundPlayer>("Enemy Sounds/" + type.name);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(type.name);
            if (sound != null)
            {
                if (GUILayout.Button("Edit sounds"))
                {
                    Selection.activeObject = sound.gameObject;
                    EditorGUIUtility.PingObject(sound.gameObject);
                }
            }
            else if (GUILayout.Button("Create separate sounds"))
            {
                // Runtime resolves this exact name; never serialize the enemy array
                // into Tuning.asset just to assign a sound-bank reference.
                string path = "Assets/KRZ/Resources/Enemy Sounds/" + type.name + ".prefab";
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    Debug.LogWarning("An asset already exists at " + path + ". Add a SoundPlayer to it instead of overwriting it.");
                    Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
                    EditorGUILayout.EndHorizontal();
                    continue;
                }
                var go = new GameObject(type.name + " Sounds");
                try
                {
                    var copy = go.AddComponent<SoundPlayer>();
                    if (tuning.enemySounds != null) copy.CopyFrom(tuning.enemySounds);
                    var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                    // SaveAsPrefabAsset saves only this prefab. SaveAssets would
                    // also flush unrelated dirty assets, including live tuning.
                    Selection.activeObject = prefab;
                }
                finally { DestroyImmediate(go); }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }
}
