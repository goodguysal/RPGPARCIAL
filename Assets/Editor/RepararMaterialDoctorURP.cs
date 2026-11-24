using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes a URP/Lit copy of the plague doctor's Built-in Standard material and
/// assigns it only to the DoctorModel instance in Combate.unity.
/// </summary>
public static class RepararMaterialDoctorURP
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string RutaMaterialOrigen = "Assets/Polytope Studio/Lowpoly_Characters/Sources/Materials/PT_mat.mat";
    private const string RutaMaterialURP = "Assets/Materials/Combat/DOCTOR_URP.mat";
    private const string RutaSolicitudRelativa = "../Temp/restore/RepararMaterialDoctorURP.once";

    [InitializeOnLoadMethod]
    private static void EjecutarSolicitudUnica()
    {
        EditorApplication.delayCall += () =>
        {
            string solicitud = ObtenerRutaSolicitud();
            if (!File.Exists(solicitud)) return;

            try
            {
                Reparar();
                File.Delete(solicitud);
                AssetDatabase.Refresh();
                Debug.Log("DOCTOR ahora usa un material URP/Lit separado; PT_mat.mat quedó intacto.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        };
    }

    [MenuItem("Tools/RPG/Reparar material URP de DOCTOR")]
    public static void Reparar()
    {
        Material origen = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialOrigen);
        if (origen == null)
            throw new InvalidOperationException("No se encontró el material original PT_mat.mat.");
        if (origen.shader == null || origen.shader.name != "Standard")
            throw new InvalidOperationException($"Se esperaba el shader Standard, pero PT_mat usa '{(origen.shader != null ? origen.shader.name : "NULL")}'. No se modificó ningún material.");
        int modoOrigen = origen.HasProperty("_Mode") ? Mathf.RoundToInt(origen.GetFloat("_Mode")) : 0;
        if (modoOrigen != 0)
            throw new InvalidOperationException("Esta herramienta convierte materiales Standard opacos. PT_mat no está en modo opaco; no se modificó ningún material.");
        if (origen.HasProperty("_SpecGlossMap") && origen.GetTexture("_SpecGlossMap") != null)
            throw new InvalidOperationException("PT_mat usa un mapa especular. Esta herramienta no convierte ese modo automáticamente.");

        Shader shaderLit = Shader.Find("Universal Render Pipeline/Lit");
        if (shaderLit == null)
            throw new InvalidOperationException("No se encontró Universal Render Pipeline/Lit. Comprueba que URP esté cargado en Unity.");

        // Captura todo lo que se va a mapear mientras el material de origen sigue intacto.
        Texture texturaBase = origen.HasProperty("_MainTex") ? origen.GetTexture("_MainTex") : null;
        Vector2 escalaTextura = origen.HasProperty("_MainTex") ? origen.GetTextureScale("_MainTex") : Vector2.one;
        Vector2 desplazamientoTextura = origen.HasProperty("_MainTex") ? origen.GetTextureOffset("_MainTex") : Vector2.zero;
        Color colorBase = origen.HasProperty("_Color") ? origen.GetColor("_Color") : Color.white;
        float metalico = origen.HasProperty("_Metallic") ? origen.GetFloat("_Metallic") : 0f;
        float suavidad = origen.HasProperty("_Glossiness") ? origen.GetFloat("_Glossiness") : 0.5f;
        Texture mapaNormal = origen.HasProperty("_BumpMap") ? origen.GetTexture("_BumpMap") : null;
        Vector2 escalaNormal = origen.HasProperty("_BumpMap") ? origen.GetTextureScale("_BumpMap") : Vector2.one;
        Vector2 desplazamientoNormal = origen.HasProperty("_BumpMap") ? origen.GetTextureOffset("_BumpMap") : Vector2.zero;
        float fuerzaNormal = origen.HasProperty("_BumpScale") ? origen.GetFloat("_BumpScale") : 1f;
        Texture mapaMetalico = origen.HasProperty("_MetallicGlossMap") ? origen.GetTexture("_MetallicGlossMap") : null;
        Texture oclusion = origen.HasProperty("_OcclusionMap") ? origen.GetTexture("_OcclusionMap") : null;
        float fuerzaOclusion = origen.HasProperty("_OcclusionStrength") ? origen.GetFloat("_OcclusionStrength") : 1f;
        Texture emision = origen.HasProperty("_EmissionMap") ? origen.GetTexture("_EmissionMap") : null;
        Color colorEmision = origen.HasProperty("_EmissionColor") ? origen.GetColor("_EmissionColor") : Color.black;

        AsegurarCarpeta("Assets/Materials");
        AsegurarCarpeta("Assets/Materials/Combat");

        Material materialURP = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialURP);
        if (materialURP == null)
        {
            materialURP = new Material(shaderLit);
            materialURP.name = "DOCTOR_URP";
            AssetDatabase.CreateAsset(materialURP, RutaMaterialURP);
        }
        else
        {
            Undo.RecordObject(materialURP, "Actualizar material URP de DOCTOR");
            materialURP.shader = shaderLit;
        }

        if (materialURP.HasProperty("_BaseMap"))
        {
            materialURP.SetTexture("_BaseMap", texturaBase);
            materialURP.SetTextureScale("_BaseMap", escalaTextura);
            materialURP.SetTextureOffset("_BaseMap", desplazamientoTextura);
        }
        if (materialURP.HasProperty("_BaseColor")) materialURP.SetColor("_BaseColor", colorBase);
        if (materialURP.HasProperty("_Metallic")) materialURP.SetFloat("_Metallic", metalico);
        if (materialURP.HasProperty("_Smoothness")) materialURP.SetFloat("_Smoothness", suavidad);
        if (materialURP.HasProperty("_BumpMap"))
        {
            materialURP.SetTexture("_BumpMap", mapaNormal);
            materialURP.SetTextureScale("_BumpMap", escalaNormal);
            materialURP.SetTextureOffset("_BumpMap", desplazamientoNormal);
            if (mapaNormal != null)
            {
                materialURP.SetFloat("_BumpScale", fuerzaNormal);
                materialURP.EnableKeyword("_NORMALMAP");
            }
        }
        if (materialURP.HasProperty("_MetallicGlossMap")) materialURP.SetTexture("_MetallicGlossMap", mapaMetalico);
        if (materialURP.HasProperty("_OcclusionMap"))
        {
            materialURP.SetTexture("_OcclusionMap", oclusion);
            if (materialURP.HasProperty("_OcclusionStrength")) materialURP.SetFloat("_OcclusionStrength", fuerzaOclusion);
        }
        if (materialURP.HasProperty("_EmissionMap")) materialURP.SetTexture("_EmissionMap", emision);
        if (materialURP.HasProperty("_EmissionColor")) materialURP.SetColor("_EmissionColor", colorEmision);
        if (emision != null || colorEmision.maxColorComponent > 0f) materialURP.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(materialURP);
        AssetDatabase.SaveAssets();

        Material recargado = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialURP);
        if (recargado == null || recargado.shader == null || recargado.shader.name != "Universal Render Pipeline/Lit")
            throw new InvalidOperationException("Unity no guardó DOCTOR_URP con el shader URP/Lit esperado.");
        if (texturaBase != null && recargado.GetTexture("_BaseMap") == null)
            throw new InvalidOperationException("La textura _MainTex no llegó a _BaseMap; no se guardó la escena.");

        Scene escena = SceneManager.GetSceneByPath(RutaEscena);
        if (!escena.IsValid() || !escena.isLoaded)
            escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);

        GameObject doctor = BuscarObjeto(escena, "DOCTOR");
        Transform modelo = doctor != null ? doctor.transform.Find("DoctorModel") : null;
        if (modelo == null)
            throw new InvalidOperationException("No se encontró DOCTOR/DoctorModel en Combate.unity. El material URP quedó creado, pero no se asignó a la escena.");

        Renderer[] renderers = modelo.GetComponentsInChildren<Renderer>(true);
        int asignaciones = 0;
        foreach (Renderer renderer in renderers)
        {
            Material[] materiales = renderer.sharedMaterials;
            bool cambio = false;
            for (int i = 0; i < materiales.Length; i++)
            {
                if (materiales[i] == origen || materiales[i] == recargado)
                {
                    materiales[i] = recargado;
                    cambio = true;
                    asignaciones++;
                }
            }

            if (!cambio) continue;
            Undo.RecordObject(renderer, "Asignar material URP a DOCTOR");
            renderer.sharedMaterials = materiales;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
        }

        if (asignaciones == 0)
            throw new InvalidOperationException("DoctorModel no tenía PT_mat asignado; no se alteró la asignación de materiales de la escena.");

        EditorSceneManager.MarkSceneDirty(escena);
        if (!EditorSceneManager.SaveScene(escena))
            throw new InvalidOperationException("Unity no pudo guardar Combate.unity después de asignar el material.");
    }

    private static void AsegurarCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        int separador = ruta.LastIndexOf('/');
        string padre = ruta.Substring(0, separador);
        string nombre = ruta.Substring(separador + 1);
        if (!AssetDatabase.IsValidFolder(padre)) AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, nombre);
    }

    private static GameObject BuscarObjeto(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz.name == nombre) return raiz;
            foreach (Transform hijo in raiz.GetComponentsInChildren<Transform>(true))
                if (hijo.name == nombre) return hijo.gameObject;
        }
        return null;
    }

    private static string ObtenerRutaSolicitud()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, RutaSolicitudRelativa));
    }
}
