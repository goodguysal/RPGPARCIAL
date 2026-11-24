using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Convierte los materiales Built-in del ESQUELETO a URP y ordena los textos del combate.</summary>
public static class RepararMaterialEsqueletoURP
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string Solicitud = "RepararMaterialEsqueletoURP.once";
    private const string CarpetaMateriales = "Assets/Materials/Combat";

    [InitializeOnLoadMethod]
    private static void EjecutarSolicitud()
    {
        EditorApplication.delayCall += () =>
        {
            string archivo = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "restore", Solicitud);
            if (!File.Exists(archivo)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= ReintentarAlSalirDePlay;
                EditorApplication.playModeStateChanged += ReintentarAlSalirDePlay;
                return;
            }
            try
            {
                Reparar();
                File.Delete(archivo);
                Debug.Log("Materiales de ESQUELETO migrados a URP y textos de combate reordenados.");
            }
            catch (Exception ex) { Debug.LogException(ex); }
        };
    }

    private static void ReintentarAlSalirDePlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= ReintentarAlSalirDePlay;
        EditorApplication.delayCall += EjecutarSolicitud;
    }

    [MenuItem("Tools/RPG/Reparar materiales y textos de ESQUELETO")]
    public static void Reparar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play Mode antes de reparar materiales y textos.");
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) throw new InvalidOperationException("No se encontró URP/Lit. Comprueba que URP esté cargado.");

        Scene escena = SceneManager.GetSceneByPath(RutaEscena);
        bool abiertaAqui = !escena.IsValid() || !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
        try
        {
            GameObject esqueleto = Buscar(escena, "ESQUELETO");
            if (esqueleto == null) throw new InvalidOperationException("No se encontró ESQUELETO en Combate.unity.");
            int cambios = RepararRenderers(esqueleto, urpLit);
            AjustarNombresYDetalles(escena);
            if (cambios == 0)
                Debug.Log("Los materiales de ESQUELETO ya usan shaders compatibles; se ajustó igualmente la UI.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena)) throw new InvalidOperationException("Unity no pudo guardar Combate.unity.");
        }
        finally { if (abiertaAqui) EditorSceneManager.CloseScene(escena, true); }
    }

    [MenuItem("Tools/RPG/Reparar materiales y textos de ESQUELETO", true)]
    private static bool PuedeReparar() => !EditorApplication.isPlayingOrWillChangePlaymode;

    private static int RepararRenderers(GameObject esqueleto, Shader urpLit)
    {
        AsegurarCarpeta(CarpetaMateriales);
        int asignaciones = 0;
        foreach (Renderer renderer in esqueleto.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materiales = renderer.sharedMaterials;
            bool cambio = false;
            for (int i = 0; i < materiales.Length; i++)
            {
                Material origen = materiales[i];
                if (origen == null || origen.shader == null || origen.shader.name != "Standard") continue;
                Material destino = CrearMaterialURP(origen, urpLit);
                materiales[i] = destino;
                cambio = true;
                asignaciones++;
            }
            if (!cambio) continue;
            Undo.RecordObject(renderer, "Asignar materiales URP a ESQUELETO");
            renderer.sharedMaterials = materiales;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
        }
        if (asignaciones == 0)
            Debug.LogWarning("No se encontraron materiales con shader Standard bajo ESQUELETO; revisa sus Renderer en el Inspector.");
        return asignaciones;
    }

    private static Material CrearMaterialURP(Material origen, Shader urpLit)
    {
        string nombre = "ESQUELETO_" + LimpiarNombre(origen.name) + "_URP";
        string ruta = CarpetaMateriales + "/" + nombre + ".mat";
        Material destino = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (destino == null)
        {
            destino = new Material(urpLit) { name = nombre };
            AssetDatabase.CreateAsset(destino, ruta);
        }
        else
        {
            Undo.RecordObject(destino, "Actualizar material URP de ESQUELETO");
            destino.shader = urpLit;
        }
        CopiarTextura(origen, destino, "_MainTex", "_BaseMap");
        if (destino.HasProperty("_BaseColor"))
            destino.SetColor("_BaseColor", origen.HasProperty("_Color") ? origen.GetColor("_Color") : Color.white);
        CopiarFloat(origen, destino, "_Metallic", "_Metallic", 0f);
        CopiarFloat(origen, destino, "_Glossiness", "_Smoothness", 0.35f);
        CopiarTextura(origen, destino, "_BumpMap", "_BumpMap");
        CopiarTextura(origen, destino, "_MetallicGlossMap", "_MetallicGlossMap");
        CopiarTextura(origen, destino, "_OcclusionMap", "_OcclusionMap");
        CopiarTextura(origen, destino, "_EmissionMap", "_EmissionMap");
        if (origen.HasProperty("_EmissionColor") && destino.HasProperty("_EmissionColor"))
            destino.SetColor("_EmissionColor", origen.GetColor("_EmissionColor"));
        if (origen.HasProperty("_BumpMap") && origen.GetTexture("_BumpMap") != null)
        {
            if (origen.HasProperty("_BumpScale") && destino.HasProperty("_BumpScale"))
                destino.SetFloat("_BumpScale", origen.GetFloat("_BumpScale"));
            destino.EnableKeyword("_NORMALMAP");
        }
        if (origen.HasProperty("_EmissionMap") && origen.GetTexture("_EmissionMap") != null)
            destino.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(destino);
        return destino;
    }

    private static void CopiarTextura(Material origen, Material destino, string origenProp, string destinoProp)
    {
        if (!origen.HasProperty(origenProp) || !destino.HasProperty(destinoProp)) return;
        destino.SetTexture(destinoProp, origen.GetTexture(origenProp));
        destino.SetTextureScale(destinoProp, origen.GetTextureScale(origenProp));
        destino.SetTextureOffset(destinoProp, origen.GetTextureOffset(origenProp));
    }

    private static void CopiarFloat(Material origen, Material destino, string origenProp, string destinoProp, float valorDefecto)
    {
        if (!destino.HasProperty(destinoProp)) return;
        destino.SetFloat(destinoProp, origen.HasProperty(origenProp) ? origen.GetFloat(origenProp) : valorDefecto);
    }

    private static void AjustarNombresYDetalles(Scene escena)
    {
        foreach (string slot in new[] { "CharacterSlot1", "CharacterSlot2", "CharacterSlot3" })
        {
            Transform card = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == slot);
            Transform nombre = card != null ? card.Find("Name") : null;
            Transform iniciativa = card != null ? card.Find("Initiative") : null;
            if (nombre != null && nombre is RectTransform nombreRect)
            {
                nombreRect.sizeDelta = new Vector2(140f, nombreRect.sizeDelta.y);
                EditorUtility.SetDirty(nombreRect);
            }
            if (iniciativa != null && iniciativa is RectTransform iniciativaRect)
                iniciativaRect.anchoredPosition = new Vector2(-12f, -13f);
        }

        GameObject battlePanel = Buscar(escena, "BattlePanel");
        Transform actionPanel = battlePanel != null ? battlePanel.transform.Find("ActionPanel") : null;
        if (actionPanel == null) return;
        foreach (string menuNombre in new[] { "Commands", "Attacks", "Inventory" })
        {
            Transform menu = actionPanel.Find(menuNombre);
            if (menu == null) continue;
            foreach (Transform boton in menu)
            {
                Transform label = boton.Find("Label");
                Transform detail = boton.Find("Detail");
                if (label is RectTransform labelRect)
                {
                    labelRect.anchoredPosition = new Vector2(labelRect.anchoredPosition.x, -12f);
                    labelRect.sizeDelta = new Vector2(labelRect.sizeDelta.x, 20f);
                    EditorUtility.SetDirty(labelRect);
                }
                if (detail is RectTransform detailRect)
                {
                    detailRect.anchoredPosition = new Vector2(detailRect.anchoredPosition.x, 6f);
                    detailRect.sizeDelta = new Vector2(detailRect.sizeDelta.x, 14f);
                    EditorUtility.SetDirty(detailRect);
                }
            }
        }
    }

    private static GameObject Buscar(Scene escena, string nombre)
    {
        foreach (GameObject root in escena.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t.gameObject;
        return null;
    }

    private static void AsegurarCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        int idx = ruta.LastIndexOf('/');
        string padre = ruta.Substring(0, idx);
        string nombre = ruta.Substring(idx + 1);
        if (!AssetDatabase.IsValidFolder(padre)) AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, nombre);
    }

    private static string LimpiarNombre(string nombre) => string.Concat(nombre.Select(c =>
        char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_'));
}
