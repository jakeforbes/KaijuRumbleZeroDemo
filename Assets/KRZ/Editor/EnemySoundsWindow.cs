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
                string path = AssetDatabase.GenerateUniqueAssetPath("Assets/KRZ/Resources/Enemy Sounds/" + type.name + ".prefab");
                var go = new GameObject(type.name + " Sounds");
                try
                {
                    var copy = go.AddComponent<SoundPlayer>();
                    if (tuning.enemySounds != null) copy.CopyFrom(tuning.enemySounds);
                    var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                    Undo.RecordObject(tuning, "Assign enemy sounds");
                    type.sounds = prefab.GetComponent<SoundPlayer>();
                    EditorUtility.SetDirty(tuning);
                    AssetDatabase.SaveAssets();
                    Selection.activeObject = prefab;
                }
                finally { DestroyImmediate(go); }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }
}
