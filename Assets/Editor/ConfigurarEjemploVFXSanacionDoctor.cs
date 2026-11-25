using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Ejemplo: Healing Character Aura sobre el aliado curado por DOCTOR.</summary>
public static class ConfigurarEjemploVFXSanacionDoctor
{
    private const string RutaPerfil = "Assets/ScriptableObjects/VFXCombate/DoctorVFX.asset";
    private const string RutaPrefab = "Assets/Hovl Studio/Magic effects pack/Prefabs/Character auras/Healing.prefab";
    private const string ArchivoSolicitud = "ConfigurarEjemploVFXSanacionDoctor.once";

    [InitializeOnLoadMethod]
    private static void RevisarSolicitudPendiente()
    {
        EditorApplication.delayCall += () =>
        {
            string marcador = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "restore", ArchivoSolicitud);
            if (!File.Exists(marcador)) return;
            try
            {
                Aplicar();
                File.Delete(marcador);
                Debug.Log("Healing Character Aura asignada a Sanación alquímica de DOCTOR.");
            }
            catch (Exception excepcion) { Debug.LogException(excepcion); }
        };
    }

    [MenuItem("Tools/RPG/Configurar ejemplo VFX de sanación de DOCTOR")]
    public static void Aplicar()
    {
        ConfiguracionVFXCombate perfil = AssetDatabase.LoadAssetAtPath<ConfiguracionVFXCombate>(RutaPerfil);
        if (perfil == null) throw new FileNotFoundException("No se encontró el perfil ScriptableObject de DOCTOR: " + RutaPerfil);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
        if (prefab == null) throw new FileNotFoundException("No se encontró Healing Character Aura: " + RutaPrefab);

        VFXAccionCombate entrada = perfil.Buscar(AccionVFXCombate.SanacionDoctor);
        if (entrada == null) throw new InvalidOperationException("El perfil de DOCTOR no tiene una acción SanacionDoctor.");
        entrada.prefab = prefab;
        entrada.usarObjetivo = true;
        entrada.seguirPuntoDeAparicion = true;
        entrada.desplazamiento = Vector3.zero;
        entrada.rotacionEuler = Vector3.zero;
        entrada.duracionSegundos = 4f;
        EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();
    }
}
