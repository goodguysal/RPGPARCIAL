using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Crea los perfiles VFX vacíos y añade el consumible de celeridad al Canvas.</summary>
public static class InstalarConsumibleATByVFX
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string CarpetaVFX = "Assets/ScriptableObjects/VFXCombate";
    private const string ArchivoSolicitud = "InstalarConsumibleATByVFX.once";

    [InitializeOnLoadMethod]
    private static void RevisarSolicitudPendiente()
    {
        EditorApplication.delayCall += () =>
        {
            string marcador = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "restore", ArchivoSolicitud);
            if (!File.Exists(marcador)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= ReintentarAlSalirDePlay;
                EditorApplication.playModeStateChanged += ReintentarAlSalirDePlay;
                return;
            }
            try
            {
                Instalar();
                File.Delete(marcador);
                Debug.Log("Elixir de celeridad y perfiles VFX añadidos a la escena.");
            }
            catch (Exception excepcion) { Debug.LogException(excepcion); }
        };
    }

    private static void ReintentarAlSalirDePlay(PlayModeStateChange estado)
    {
        if (estado != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= ReintentarAlSalirDePlay;
        EditorApplication.delayCall += RevisarSolicitudPendiente;
    }

    [MenuItem("Tools/RPG/Instalar Elixir ATB y perfiles VFX")]
    public static void Instalar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play Mode antes de cambiar el Canvas y asignar los perfiles VFX.");

        AsegurarCarpeta("Assets/ScriptableObjects");
        AsegurarCarpeta(CarpetaVFX);
        ConfiguracionVFXCombate doctorVFX = ObtenerOCrearPerfil("DoctorVFX", "DOCTOR", new[]
        {
            AccionVFXCombate.AtaqueBasicoDoctor, AccionVFXCombate.AtaquePesadoDoctor,
            AccionVFXCombate.GolpeImpulsorDoctor, AccionVFXCombate.SanacionDoctor,
            AccionVFXCombate.Defenderse, AccionVFXCombate.Venda, AccionVFXCombate.Tonico,
            AccionVFXCombate.SalesDeCombate, AccionVFXCombate.Veneno,
            AccionVFXCombate.EsenciaBestia, AccionVFXCombate.AceleradorATB
        });
        ConfiguracionVFXCombate curaVFX = ObtenerOCrearPerfil("CuraVFX", "CURA", new[]
        {
            AccionVFXCombate.GolpeBaculoCura, AccionVFXCombate.RayoSagradoCura,
            AccionVFXCombate.RegenerarManaCura, AccionVFXCombate.Defenderse,
            AccionVFXCombate.Venda, AccionVFXCombate.Tonico, AccionVFXCombate.SalesDeCombate,
            AccionVFXCombate.Veneno, AccionVFXCombate.EsenciaBestia, AccionVFXCombate.AceleradorATB
        });
        ConfiguracionVFXCombate esqueletoVFX = ObtenerOCrearPerfil("EsqueletoVFX", "ESQUELETO", new[]
        {
            AccionVFXCombate.AtaqueBasicoEsqueleto, AccionVFXCombate.AtaquePesadoEsqueleto,
            AccionVFXCombate.GuardiaReflejaEsqueleto, AccionVFXCombate.Defenderse,
            AccionVFXCombate.Venda, AccionVFXCombate.Tonico, AccionVFXCombate.SalesDeCombate,
            AccionVFXCombate.Veneno, AccionVFXCombate.EsenciaBestia, AccionVFXCombate.AceleradorATB
        });
        ConfiguracionVFXCombate golemVFX = ObtenerOCrearPerfil("GolemVFX", "GOLEM", new[] { AccionVFXCombate.GolpeGolem });
        VFXAccionCombate golpeGolem = golemVFX.Buscar(AccionVFXCombate.GolpeGolem);
        GameObject piedras = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Stones hit.prefab");
        if (golpeGolem == null || piedras == null) throw new InvalidOperationException("No se encontró la entrada VFX GolpeGolem o el prefab Stones hit.");
        golpeGolem.prefab = piedras;
        golpeGolem.usarObjetivo = true;
        golpeGolem.seguirPuntoDeAparicion = false;
        golpeGolem.desplazamiento = new Vector3(0f, 0.75f, 0f);
        golpeGolem.duracionSegundos = 3f;
        EditorUtility.SetDirty(golemVFX);

        Scene escena = SceneManager.GetSceneByPath(RutaEscena);
        bool abiertaAqui = !escena.IsValid() || !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
        try
        {
            Transform inventario = Buscar(escena, "Inventory");
            if (inventario == null) throw new InvalidOperationException("No se encontró el panel Inventory en el Canvas.");
            Transform plantilla = inventario.Find("BtnEsenciaBestia") ?? inventario.Find("BtnTonico");
            if (plantilla == null) throw new InvalidOperationException("No se encontró un botón de inventario para crear el nuevo consumible.");
            ConfigurarBoton(inventario, plantilla.gameObject);

            int doctor = 0, cura = 0, esqueleto = 0;
            int enemigos = 0;
            foreach (GameObject raiz in escena.GetRootGameObjects())
            {
                foreach (PersonajeCombate personaje in raiz.GetComponentsInChildren<PersonajeCombate>(true))
                {
                    ConfiguracionVFXCombate perfil = personaje.EsDoctor ? doctorVFX : personaje.EsCura ? curaVFX : esqueletoVFX;
                    SerializedObject datos = new SerializedObject(personaje);
                    SerializedProperty campo = datos.FindProperty("configuracionVFX");
                    SerializedProperty cantidad = datos.FindProperty("aceleradoresATBIniciales");
                    if (campo == null || cantidad == null) throw new MissingFieldException("PersonajeCombate no tiene los campos de VFX y aceleradores ATB.");
                    campo.objectReferenceValue = perfil;
                    cantidad.intValue = 1;
                    datos.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(personaje);
                    if (personaje.EsDoctor) doctor++;
                    else if (personaje.EsCura) cura++;
                    else esqueleto++;
                }
                foreach (EnemigoCombate enemigo in raiz.GetComponentsInChildren<EnemigoCombate>(true))
                {
                    SerializedObject datos = new SerializedObject(enemigo);
                    SerializedProperty perfil = datos.FindProperty("configuracionVFX");
                    if (perfil == null) throw new MissingFieldException("EnemigoCombate no tiene el campo configuracionVFX.");
                    perfil.objectReferenceValue = golemVFX;
                    datos.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(enemigo);
                    enemigos++;
                }
            }
            if (doctor != 1 || cura != 1 || esqueleto != 1)
                throw new InvalidOperationException($"Se esperaba 1 Doctor, 1 Cura y 1 Esqueleto en la escena (detectados: {doctor}, {cura}, {esqueleto}).");
            if (enemigos != 1) throw new InvalidOperationException($"Se esperaba 1 GOLEM en la escena (detectados: {enemigos}).");

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena)) throw new InvalidOperationException("No se pudo guardar Combate.unity.");
        }
        finally { if (abiertaAqui) EditorSceneManager.CloseScene(escena, true); }
    }

    private static ConfiguracionVFXCombate ObtenerOCrearPerfil(string archivo, string nombre, AccionVFXCombate[] acciones)
    {
        string ruta = CarpetaVFX + "/" + archivo + ".asset";
        ConfiguracionVFXCombate perfil = AssetDatabase.LoadAssetAtPath<ConfiguracionVFXCombate>(ruta);
        if (perfil == null)
        {
            perfil = ScriptableObject.CreateInstance<ConfiguracionVFXCombate>();
            perfil.personaje = nombre;
            perfil.acciones = acciones.Select(accion => new VFXAccionCombate { accion = accion }).ToList();
            AssetDatabase.CreateAsset(perfil, ruta);
        }
        else
        {
            perfil.personaje = nombre;
            foreach (AccionVFXCombate accion in acciones)
                if (!perfil.acciones.Any(entrada => entrada != null && entrada.accion == accion))
                    perfil.acciones.Add(new VFXAccionCombate { accion = accion });
            EditorUtility.SetDirty(perfil);
        }
        return perfil;
    }

    private static void AsegurarCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        string nombre = Path.GetFileName(ruta);
        if (!AssetDatabase.IsValidFolder(padre)) AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, nombre);
    }

    private static void ConfigurarBoton(Transform inventario, GameObject plantilla)
    {
        Transform existente = inventario.Find("BtnAceleradorATB");
        GameObject objeto = existente != null ? existente.gameObject : UnityEngine.Object.Instantiate(plantilla, inventario, false);
        objeto.name = "BtnAceleradorATB";
        RectTransform rect = objeto.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(492f, -234f);
        rect.sizeDelta = new Vector2(455f, 68f);
        Button boton = objeto.GetComponent<Button>();
        if (boton != null) boton.interactable = true;
        TMP_Text titulo = objeto.transform.Find("Label")?.GetComponent<TMP_Text>();
        TMP_Text detalle = objeto.transform.Find("Detail")?.GetComponent<TMP_Text>();
        if (titulo != null) titulo.text = "ELIXIR DE CELERIDAD (1)";
        if (detalle != null) detalle.text = "Aumenta la velocidad ATB de un aliado durante 2 partidas";
        EditorUtility.SetDirty(objeto);
    }

    private static Transform Buscar(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform hijo in raiz.GetComponentsInChildren<Transform>(true))
                if (hijo.name == nombre) return hijo;
        return null;
    }
}
