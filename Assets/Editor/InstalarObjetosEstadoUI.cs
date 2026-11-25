using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Añade los consumibles de estado al menú Canvas existente.</summary>
public static class InstalarObjetosEstadoUI
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string ArchivoSolicitud = "InstalarObjetosEstadoUI.once";

    [InitializeOnLoadMethod]
    private static void EjecutarSolicitudAlRecargar()
    {
        EditorApplication.delayCall += () =>
        {
            string marker = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "restore", ArchivoSolicitud);
            if (!File.Exists(marker)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= ReintentarAlSalirDePlay;
                EditorApplication.playModeStateChanged += ReintentarAlSalirDePlay;
                return;
            }
            try
            {
                Aplicar();
                File.Delete(marker);
                Debug.Log("Sales de combate guardadas en el Canvas.");
            }
            catch (Exception excepcion) { Debug.LogException(excepcion); }
        };
    }

    private static void ReintentarAlSalirDePlay(PlayModeStateChange estado)
    {
        if (estado != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= ReintentarAlSalirDePlay;
        EditorApplication.delayCall += EjecutarSolicitudAlRecargar;
    }

    [MenuItem("Tools/RPG/Instalar consumibles de estado en Canvas")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play Mode antes de modificar el menú de consumibles.");

        Scene escena = SceneManager.GetSceneByPath(RutaEscena);
        bool abiertaAqui = !escena.IsValid() || !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
        try
        {
            Transform inventario = Buscar(escena, "Inventory");
            if (inventario == null) throw new InvalidOperationException("No se encontró el panel Inventory en el Canvas.");
            Transform tonico = inventario.Find("BtnTonico");
            if (tonico == null) throw new InvalidOperationException("No se encontró BtnTonico para crear los consumibles.");

            ConfigurarBoton(inventario, tonico.gameObject, "BtnSales", new Vector2(16f, -160f),
                "SALES DE COMBATE", "Daño +25% durante 3 ataques");
            ConfigurarBoton(inventario, tonico.gameObject, "BtnVeneno", new Vector2(492f, -160f),
                "VENENO", "-5 PV al final de 2 rondas completas · 0 PM · 0 ATB");
            ConfigurarBoton(inventario, tonico.gameObject, "BtnEsenciaBestia", new Vector2(16f, -234f),
                "ESCENCIA DE BESTIA", "El próximo ataque del GOLEM va al aliado elegido");
            foreach (GameObject raiz in escena.GetRootGameObjects())
            {
                foreach (PersonajeCombate personaje in raiz.GetComponentsInChildren<PersonajeCombate>(true))
                {
                    SerializedObject datos = new SerializedObject(personaje);
                    SerializedProperty sales = datos.FindProperty("salesIniciales");
                    if (sales != null) sales.intValue = 1;
                    SerializedProperty veneno = datos.FindProperty("danoVenenoPorTurno");
                    if (veneno != null) veneno.intValue = 5;
                    SerializedProperty duracion = datos.FindProperty("duracionVenenoTurnos");
                    if (duracion != null) duracion.intValue = 2;
                    SerializedProperty esencia = datos.FindProperty("esenciasBestiaIniciales");
                    if (esencia != null) esencia.intValue = 1;
                    datos.ApplyModifiedPropertiesWithoutUndo();
                }
                EnemigoCombate enemigo = raiz.GetComponentInChildren<EnemigoCombate>(true);
                if (enemigo != null)
                {
                    SerializedObject datos = new SerializedObject(enemigo);
                    SerializedProperty paralisis = datos.FindProperty("probabilidadParalisis");
                    if (paralisis != null) paralisis.floatValue = 0.2f;
                    datos.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena))
                throw new InvalidOperationException("No se pudo guardar Combate.unity.");
        }
        finally { if (abiertaAqui) EditorSceneManager.CloseScene(escena, true); }
    }

    private static void ConfigurarBoton(Transform padre, GameObject plantilla, string nombre, Vector2 posicion,
        string titulo, string detalle)
    {
        Transform existente = padre.Find(nombre);
        GameObject objeto = existente != null ? existente.gameObject : UnityEngine.Object.Instantiate(plantilla, padre, false);
        objeto.name = nombre;
        RectTransform rect = objeto.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = posicion;
        rect.sizeDelta = new Vector2(455f, 68f);
        Button boton = objeto.GetComponent<Button>();
        if (boton != null) boton.interactable = true;
        TMP_Text textoTitulo = objeto.transform.Find("Label")?.GetComponent<TMP_Text>();
        TMP_Text textoDetalle = objeto.transform.Find("Detail")?.GetComponent<TMP_Text>();
        if (textoTitulo != null) textoTitulo.text = titulo;
        if (textoDetalle != null) textoDetalle.text = detalle;
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
