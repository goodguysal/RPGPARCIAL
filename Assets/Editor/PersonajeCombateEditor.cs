using UnityEditor;
using UnityEngine;

/// <summary>Muestra y permite editar el perfil VFX dentro del Inspector del personaje.</summary>
[CustomEditor(typeof(PersonajeCombate))]
[CanEditMultipleObjects]
public sealed class PersonajeCombateEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "configuracionVFX");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("VFX POR HABILIDAD", EditorStyles.boldLabel);
        SerializedProperty perfilPropiedad = serializedObject.FindProperty("configuracionVFX");
        EditorGUILayout.PropertyField(perfilPropiedad, new GUIContent("Perfil VFX"));
        serializedObject.ApplyModifiedProperties();

        ConfiguracionVFXCombate perfil = perfilPropiedad.objectReferenceValue as ConfiguracionVFXCombate;
        if (perfil == null)
        {
            EditorGUILayout.HelpBox("Asigna el perfil VFX de este personaje para editar los efectos de cada acción aquí.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField(perfil.personaje, EditorStyles.miniBoldLabel);
        SerializedObject datosPerfil = new SerializedObject(perfil);
        datosPerfil.Update();
        SerializedProperty acciones = datosPerfil.FindProperty("acciones");
        if (acciones == null)
        {
            EditorGUILayout.HelpBox("El perfil no contiene la lista de acciones VFX.", MessageType.Error);
            return;
        }

        for (int i = 0; i < acciones.arraySize; i++)
        {
            SerializedProperty entrada = acciones.GetArrayElementAtIndex(i);
            SerializedProperty accion = entrada.FindPropertyRelative("accion");
            string nombreAccion = accion != null && accion.enumValueIndex >= 0 && accion.enumValueIndex < accion.enumDisplayNames.Length
                ? accion.enumDisplayNames[accion.enumValueIndex]
                : $"Acción {i + 1}";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(nombreAccion, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(accion, new GUIContent("Acción"));
            EditorGUILayout.PropertyField(entrada.FindPropertyRelative("prefab"), new GUIContent("Prefab VFX"));
            EditorGUILayout.PropertyField(entrada.FindPropertyRelative("usarObjetivo"), new GUIContent("Aparecer sobre el objetivo"));
            EditorGUILayout.PropertyField(entrada.FindPropertyRelative("seguirPuntoDeAparicion"), new GUIContent("Seguir al personaje"));
            EditorGUILayout.PropertyField(entrada.FindPropertyRelative("desplazamiento"), new GUIContent("Desplazamiento"));
            EditorGUILayout.PropertyField(entrada.FindPropertyRelative("rotacionEuler"), new GUIContent("Rotación"));
            EditorGUILayout.PropertyField(entrada.FindPropertyRelative("duracionSegundos"), new GUIContent("Duración (segundos)"));
            EditorGUILayout.EndVertical();
        }

        if (datosPerfil.ApplyModifiedProperties())
            EditorUtility.SetDirty(perfil);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Seleccionar perfil en Project", GUILayout.Width(190f)))
            {
                Selection.activeObject = perfil;
                EditorGUIUtility.PingObject(perfil);
            }
        }
    }
}
