using UnityEditor;

[CustomEditor(typeof(EmojiText), true)]
[CanEditMultipleObjects]
public class EmojiTextEditor : UnityEditor.UI.TextEditor
{
    #region 属性
    SerializedProperty _emojiAsset;
    #endregion

    protected override void OnEnable()
    {
        base.OnEnable();
        _emojiAsset = serializedObject.FindProperty("emojiAsset");
    }
    
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(_emojiAsset);
        serializedObject.ApplyModifiedProperties();
    }
}
