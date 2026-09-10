using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SoundPlayer))]
public class SoundPlayerEditor : Editor
{
    [MenuItem("KRZ/Audio/Player Sounds")]
    static void SelectPlayer() => SelectTemplate("Player Sounds");
    [MenuItem("KRZ/Audio/Enemy Sounds")]
    static void SelectEnemy() => SelectTemplate("Enemy Sounds");
    [MenuItem("KRZ/Audio/Building Sounds")]
    static void SelectBuilding() => SelectTemplate("Building Sounds");
    [MenuItem("KRZ/Audio/Food Sounds")]
    static void SelectFood() => SelectTemplate("Food Sounds");

    static void SelectTemplate(string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KRZ/Audio/" + name + ".prefab");
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    void Sync()
    {
        var player = (SoundPlayer)target;
        bool missing = player.DetectActions().Exists(id => !System.Linq.Enumerable.Any(player.Actions, x => x.action == id));
        if (!missing && !System.Linq.Enumerable.Any(player.Actions, x => (int)x.action == 23)) return;
        Undo.RecordObject(player, "Detect sound actions");
        player.RefreshActions();
        EditorUtility.SetDirty(player);
        PrefabUtility.RecordPrefabInstancePropertyModifications(player);
    }

    public override void OnInspectorGUI()
    {
        Sync();
        serializedObject.Update();
        EditorGUILayout.HelpBox("Actions are detected from gameplay components on this object. Multiple clips per action are chosen randomly. New scripts need SoundActions and an AudioEvents.Play call.", MessageType.Info);
        foreach (var name in new[] { "masterVolume", "output", "spatialBlend", "minDistance", "maxDistance", "maxVoices", "footstepInterval" })
            EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
        EditorGUILayout.Space();
        var actions = serializedObject.FindProperty("actions");
        var detected = ((SoundPlayer)target).DetectActions();
        if (detected.Count == 0 && actions.arraySize > 0)
            EditorGUILayout.HelpBox("This sound template is edited here, before Play. The generated objects copy these settings when they spawn. Assign clips below, then restart Play to hear them in the game.", MessageType.Info);
        else if (detected.Count == 0)
            EditorGUILayout.HelpBox("No supported gameplay component found. Add one, or add a manual action and trigger PlayAction from a UnityEvent or animation event.", MessageType.Info);
        for (int i = 0; i < actions.arraySize; i++)
        {
            var item = actions.GetArrayElementAtIndex(i);
            var id = item.FindPropertyRelative("action");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            item.isExpanded = EditorGUILayout.Foldout(item.isExpanded, ObjectNames.NicifyVariableName(id.enumNames[id.enumValueIndex]), true);
            if (item.isExpanded)
            {
                using (new EditorGUI.DisabledScope(detected.Contains((Sfx)id.intValue)))
                    EditorGUILayout.PropertyField(id);
                for (int j = 0; j < i; j++)
                    if (actions.GetArrayElementAtIndex(j).FindPropertyRelative("action").enumValueIndex == id.enumValueIndex)
                        EditorGUILayout.HelpBox("Duplicate action: only the first row is used. Choose another action or remove this row.", MessageType.Warning);
                if (!detected.Contains((Sfx)id.intValue)) EditorGUILayout.LabelField("Manual / previously detected action", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Default settings", EditorStyles.boldLabel);
                DrawPlaybackSettings(item);
                DrawSizeSettings(item, i);
                using (new EditorGUI.DisabledScope(!Application.isPlaying || EditorUtility.IsPersistent(target)))
                    if (GUILayout.Button("Test sound (Play mode)"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        ((SoundPlayer)target).TryPlay((Sfx)id.intValue);
                    }

                using (new EditorGUI.DisabledScope(detected.Contains((Sfx)id.intValue)))
                    if (GUILayout.Button("Remove manual action"))
                    {
                        actions.DeleteArrayElementAtIndex(i);
                        EditorGUILayout.EndVertical();
                        break;
                    }
            }
            EditorGUILayout.EndVertical();
        }
        if (GUILayout.Button("Add manual action"))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(target, "Add sound action");
            ((SoundPlayer)target).AddManualAction();
            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            serializedObject.Update();
        }
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();
        if (GUILayout.Button("Save sound template...")) SaveTemplate();
        EditorGUILayout.HelpBox("Generated objects: save a template and assign it on the Tuning asset. Play-mode changes to scene objects are temporary.", MessageType.Info);
    }

    void DrawSizeSettings(SerializedProperty item, int actionIndex)
    {
        var useSizes = item.FindPropertyRelative("useSizeSettings");
        EditorGUILayout.PropertyField(useSizes, new GUIContent("Use Size Settings"));
        if (!useSizes.boolValue) return;
        var levels = item.FindPropertyRelative("sizeSettings");
        if (levels.arraySize != 5)
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(target, "Initialize size sound settings");
            ((SoundPlayer)target).Actions[actionIndex].InitializeSizeSettings();
            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            serializedObject.Update();
            levels = serializedObject.FindProperty("actions").GetArrayElementAtIndex(actionIndex).FindPropertyRelative("sizeSettings");
        }
        EditorGUILayout.HelpBox("Uses this player's current Size 1–5. Enable an override to customize it; otherwise Default settings apply. New entries start as copies of the defaults. Objects without Player Progress use defaults.", MessageType.Info);
        for (int i = 0; i < 5; i++)
        {
            var level = levels.GetArrayElementAtIndex(i);
            level.isExpanded = EditorGUILayout.Foldout(level.isExpanded, "Size " + (i + 1), true);
            if (!level.isExpanded) continue;
            EditorGUI.indentLevel++;
            var useOverride = level.FindPropertyRelative("overrideSettings");
            EditorGUILayout.PropertyField(useOverride, new GUIContent("Override Settings"));
            if (useOverride.boolValue) DrawPlaybackSettings(level.FindPropertyRelative("settings"));
            else EditorGUILayout.LabelField("Using default settings", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }
    }

    void DrawPlaybackSettings(SerializedProperty item)
    {
        Draw(item, "enabled"); Draw(item, "clips"); Draw(item, "volume"); Draw(item, "randomizePitch");
        if (item.FindPropertyRelative("randomizePitch").boolValue)
        {
            var range = item.FindPropertyRelative("pitchRange");
            var v = EditorGUILayout.Vector2Field("Pitch range (min / max)", range.vector2Value);
            range.vector2Value = new Vector2(Mathf.Clamp(Mathf.Min(v.x, v.y), 0.1f, 3), Mathf.Clamp(Mathf.Max(v.x, v.y), 0.1f, 3));
        }
        else Draw(item, "pitch");
        var size = item.FindPropertyRelative("size");
        size.floatValue = EditorGUILayout.Slider("Sound size", size.floatValue, -1, 1);
        EditorGUILayout.LabelField("Smaller / tinny       Natural       Larger / bassier", EditorStyles.centeredGreyMiniLabel);
        Draw(item, "echo");
        if (item.FindPropertyRelative("echo").boolValue)
        {
            Draw(item, "echoDelay"); Draw(item, "echoDecay"); Draw(item, "echoWet");
        }
        Draw(item, "reverb");
        if (item.FindPropertyRelative("reverb").boolValue) Draw(item, "reverbPreset");
        Draw(item, "cooldown");
        if (GUILayout.Button("Add clips from Sounds...")) ShowSounds(item.FindPropertyRelative("clips").propertyPath);
    }

    static void Draw(SerializedProperty item, string name) => EditorGUILayout.PropertyField(item.FindPropertyRelative(name), true);

    void ShowSounds(string clipsPath)
    {
        var menu = new GenericMenu();
        var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Sounds" });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            menu.AddItem(new GUIContent(path.Substring("Assets/Sounds/".Length)), false, () =>
            {
                if (target == null) return;
                serializedObject.Update();
                var clips = serializedObject.FindProperty(clipsPath);
                if (clips == null) return;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                for (int j = 0; j < clips.arraySize; j++) if (clips.GetArrayElementAtIndex(j).objectReferenceValue == clip) return;
                clips.InsertArrayElementAtIndex(clips.arraySize);
                clips.GetArrayElementAtIndex(clips.arraySize - 1).objectReferenceValue = clip;
                serializedObject.ApplyModifiedProperties();
            });
        }
        if (guids.Length == 0) menu.AddDisabledItem(new GUIContent("No clips in Assets/Sounds"));
        menu.ShowAsContext();
    }

    void SaveTemplate()
    {
        string path = EditorUtility.SaveFilePanelInProject("Save sound template", target.name + " Sounds", "prefab", "Save this sound setup for generated objects.");
        if (string.IsNullOrEmpty(path)) return;
        var go = new GameObject("Sound Template");
        try
        {
            var copy = go.AddComponent<SoundPlayer>();
            copy.CopyFrom((SoundPlayer)target);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            EditorGUIUtility.PingObject(prefab);
        }
        finally { DestroyImmediate(go); }
    }
}
