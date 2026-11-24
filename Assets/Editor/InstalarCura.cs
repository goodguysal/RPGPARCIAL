using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Reemplaza el modelo visual de CURA por PRIEST sin alterar la escala de DOCTOR.</summary>
public static class InstalarCura
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string RutaPrefab = "Assets/Polytope Studio/Lowpoly_Characters/Prefabs/PT_Priest_StPatrick.prefab";
    private const string RutaMaterialDoctor = "Assets/Materials/Combat/DOCTOR_URP.mat";
    private const string RutaMaterialCura = "Assets/Materials/Combat/CURA_URP.mat";
    private const string ArchivoSolicitud = "InstalarCura.once";

    [InitializeOnLoadMethod]
    private static void EjecutarSolicitud()
    {
        EditorApplication.delayCall += () =>
        {
            string solicitud = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "restore", ArchivoSolicitud);
            if (!File.Exists(solicitud)) return;
            try
            {
                Aplicar();
                File.Delete(solicitud);
                Debug.Log("CURA quedó configurada con PRIEST y vinculada a la tarjeta de aliado.");
            }
            catch (Exception e) { Debug.LogException(e); }
        };
    }

    [MenuItem("Tools/RPG/Instalar CURA como PRIEST")]
    public static void Aplicar()
    {
        Scene scene = SceneManager.GetSceneByPath(RutaEscena);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);

        try
        {
            GameObject cura = Buscar(scene, "CURA");
            if (cura == null) throw new InvalidOperationException("No se encontró CURA en Combate.unity.");
            if (Buscar(scene, "DOCTOR") == null) throw new InvalidOperationException("No se encontró DOCTOR en Combate.unity.");
            PersonajeCombate estado = cura.GetComponent<PersonajeCombate>();
            if (estado == null) throw new InvalidOperationException("CURA necesita el componente PersonajeCombate.");

            GameObject priestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
            if (priestPrefab == null) throw new InvalidOperationException("No se encontró el prefab PRIEST: " + RutaPrefab);
            Material materialCura = ObtenerMaterialCura();
            ReemplazarModelo(cura.transform, priestPrefab, materialCura, scene);

            Undo.RecordObject(estado, "Configurar personaje CURA");
            SerializedObject serialized = new SerializedObject(estado);
            serialized.FindProperty("nombre").stringValue = "CURA";
            serialized.FindProperty("esCura").boolValue = true;
            serialized.FindProperty("iniciativa").intValue = 8;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(estado);

            PersonajeCardUI[] tarjetas = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PersonajeCardUI>(true))
                .OrderBy(card => ((RectTransform)card.transform).anchoredPosition.x)
                .ToArray();
            PersonajeCombate doctor = Buscar(scene, "DOCTOR").GetComponent<PersonajeCombate>();
            if (tarjetas.Length > 0) { Undo.RecordObject(tarjetas[0], "Vincular ficha de DOCTOR"); tarjetas[0].Vincular(doctor); EditorUtility.SetDirty(tarjetas[0]); }
            if (tarjetas.Length > 1) { Undo.RecordObject(tarjetas[1], "Vincular ficha de CURA"); tarjetas[1].Vincular(estado); EditorUtility.SetDirty(tarjetas[1]); }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("No se pudo guardar Combate.unity.");
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static Material ObtenerMaterialCura()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialCura);
        if (material != null) return material;
        Material plantilla = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialDoctor);
        if (plantilla == null) throw new InvalidOperationException("No se encontró el material URP de referencia para duplicar.");
        material = new Material(plantilla) { name = "CURA_URP" };
        AssetDatabase.CreateAsset(material, RutaMaterialCura);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static void ReemplazarModelo(Transform parent, GameObject prefab, Material material, Scene scene)
    {
        Transform anterior = parent.childCount > 0 ? parent.GetChild(0) : null;
        Bounds boundsAnteriores = ObtenerBounds(anterior);
        Quaternion rotacionAnterior = anterior != null ? anterior.rotation : Quaternion.identity;

        GameObject nuevo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        if (nuevo == null) throw new InvalidOperationException("Unity no pudo instanciar el prefab PRIEST.");
        Undo.RegisterCreatedObjectUndo(nuevo, "Añadir modelo PRIEST a CURA");
        nuevo.name = "PriestModel";
        nuevo.transform.SetParent(parent, true);
        nuevo.transform.rotation = rotacionAnterior;

        Renderer[] renderers = nuevo.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("PRIEST no contiene renderers.");
        Bounds boundsNuevos = CombinarBounds(renderers);
        float alturaAnterior = boundsAnteriores.size.y;
        if (alturaAnterior > 0.001f && boundsNuevos.size.y > 0.001f)
            nuevo.transform.localScale *= alturaAnterior / boundsNuevos.size.y;

        AsignarMaterial(renderers, material);
        boundsNuevos = CombinarBounds(renderers);
        Vector3 desplazamiento = new Vector3(
            boundsAnteriores.center.x - boundsNuevos.center.x,
            boundsAnteriores.min.y - boundsNuevos.min.y,
            boundsAnteriores.center.z - boundsNuevos.center.z);
        nuevo.transform.position += desplazamiento;

        if (anterior != null) Undo.DestroyObjectImmediate(anterior.gameObject);
    }

    private static Bounds ObtenerBounds(Transform root)
    {
        if (root == null) return new Bounds(Vector3.zero, Vector3.one);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        return renderers.Length > 0 ? CombinarBounds(renderers) : new Bounds(root.position, Vector3.one);
    }

    private static Bounds CombinarBounds(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void AsignarMaterial(Renderer[] renderers, Material material)
    {
        foreach (Renderer renderer in renderers)
        {
            Material[] materiales = renderer.sharedMaterials;
            for (int i = 0; i < materiales.Length; i++) materiales[i] = material;
            renderer.sharedMaterials = materiales;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static GameObject Buscar(Scene scene, string nombre)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == nombre) return child.gameObject;
        return null;
    }
}
