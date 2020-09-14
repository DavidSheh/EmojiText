using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EmojiAsset))]
public class EmojiAssetEditor : Editor
{
    SerializedProperty m_spriteAtlas_prop;
    SerializedProperty m_material_prop;
    SerializedProperty m_spriteInfoList_prop;

    readonly float minSpeed = 0;
    readonly float maxSpeed = 10;
    static float speed = 3;

    public static bool spriteListPanel;
    int m_page;
    GUIStyle boldFoldout = null;

    private void OnEnable()
    {
        m_spriteAtlas_prop = serializedObject.FindProperty("spriteSheet");
        m_material_prop = serializedObject.FindProperty("material");
        m_spriteInfoList_prop = serializedObject.FindProperty("spriteInfoList");

        Material mat = m_material_prop.objectReferenceValue as Material;
        if (mat != null)
        {
            mat.SetFloat("_EmojiSpeed", speed);
        }

        boldFoldout = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
    }

    public override void OnInspectorGUI()
    {
        Event currentEvent = Event.current;
        string evt_cmd = currentEvent.commandName; // Get Current Event CommandName to check for Undo Events

        serializedObject.Update();

        // Sprite Info
        GUILayout.Label("Sprite Info", EditorStyles.boldLabel);
        EditorGUI.indentLevel = 1;

        GUI.enabled = false;
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(m_spriteAtlas_prop, new GUIContent("Sprite Atlas"));
        if (EditorGUI.EndChangeCheck())
        {
            // Assign the new sprite atlas texture to the current material
            Texture2D tex = m_spriteAtlas_prop.objectReferenceValue as Texture2D;
            if (tex != null)
            {
                Material mat = m_material_prop.objectReferenceValue as Material;
                if (mat != null)
                {
                    mat.mainTexture = tex;
                }
            }
        }

        EditorGUILayout.PropertyField(m_material_prop, new GUIContent("Default Material"));
        EditorGUILayout.PropertyField(m_spriteInfoList_prop, true);
        GUI.enabled = true;
        EditorGUILayout.Space();

        EditorGUI.indentLevel = 0;
        EditorGUI.BeginChangeCheck();
        speed = EditorGUILayout.Slider(new GUIContent("Emoji Speed"), speed, minSpeed, maxSpeed);
        if (EditorGUI.EndChangeCheck())
        {
            Material mat = m_material_prop.objectReferenceValue as Material;
            if (mat != null)
            {
                mat.SetFloat("_EmojiSpeed", speed);
            }
        }
    }
}
