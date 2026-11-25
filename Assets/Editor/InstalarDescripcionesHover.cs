using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Reserva una franja legible bajo las opciones de combate para las descripciones al pasar el mouse.</summary>
public static class InstalarDescripcionesHover
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string Solicitud = "InstalarDescripcionesHover.once";

    [InitializeOnLoadMethod]
    private static void EjecutarSolicitudAlRecargar()
    {
        EditorApplication.delayCall += () =>
        {
            string marker = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "restore", Solicitud);
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
                Debug.Log("Panel de descripciones al pasar el mouse guardado en el Canvas de Combate.");
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

    [MenuItem("Tools/RPG/Instalar descripciones al pasar el mouse")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play Mode antes de guardar el panel de descripciones.");

        Scene escena = SceneManager.GetSceneByPath(RutaEscena);
        bool abiertaAqui = !escena.IsValid() || !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
        try
        {
            Transform battlePanel = Buscar(escena, "BattlePanel");
            Transform actionPanel = Buscar(escena, "ActionPanel");
            if (battlePanel == null || actionPanel == null)
                throw new InvalidOperationException("No se encontró BattlePanel/ActionPanel en el Canvas.");

            RectTransform battleRect = battlePanel.GetComponent<RectTransform>();
            if (battleRect != null) battleRect.sizeDelta = new Vector2(battleRect.sizeDelta.x, 360f);

            GameObject panel = ObtenerOCrear(actionPanel, "ActionHoverDescription", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(Outline));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(16f, -236f);
            panelRect.sizeDelta = new Vector2(-32f, 92f);

            Image fondo = panel.GetComponent<Image>();
            fondo.color = new Color32(25, 31, 40, 248);
            fondo.raycastTarget = false;
            CanvasGroup grupo = panel.GetComponent<CanvasGroup>();
            grupo.interactable = false;
            grupo.blocksRaycasts = false;
            Outline borde = panel.GetComponent<Outline>();
            borde.effectColor = new Color32(206, 164, 79, 220);
            borde.effectDistance = new Vector2(1f, -1f);
            borde.useGraphicAlpha = true;

            TMP_Text referencia = ObtenerTexto(escena, "BattlePanel/ActionPanel/Commands/BtnAttack/Label");
            TMP_Text titulo = CrearTexto(panel.transform, "Title", referencia, new Vector2(14f, -8f), new Vector2(-28f, 24f));
            titulo.text = "DETALLE DE ACCIÓN";
            titulo.fontSize = 17f;
            titulo.fontStyle = FontStyles.Bold;
            titulo.color = new Color32(255, 211, 123, 255);
            titulo.alignment = TextAlignmentOptions.Left;
            titulo.raycastTarget = false;

            TMP_Text cuerpo = CrearTexto(panel.transform, "Body", referencia, new Vector2(14f, -34f), new Vector2(-28f, 50f));
            cuerpo.text = "Pasa el mouse sobre una acción u objeto para ver qué hace.";
            cuerpo.fontSize = 15.5f;
            cuerpo.fontStyle = FontStyles.Normal;
            cuerpo.color = new Color32(245, 245, 239, 255);
            cuerpo.alignment = TextAlignmentOptions.TopLeft;
            cuerpo.raycastTarget = false;
            cuerpo.overflowMode = TextOverflowModes.Ellipsis;

            panel.SetActive(false);
            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena))
                throw new InvalidOperationException("No se pudo guardar Combate.unity.");
        }
        finally { if (abiertaAqui) EditorSceneManager.CloseScene(escena, true); }
    }

    private static TMP_Text CrearTexto(Transform padre, string nombre, TMP_Text referencia, Vector2 posicion, Vector2 tamanio)
    {
        GameObject objeto = ObtenerOCrear(padre, nombre, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = objeto.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = posicion;
        rect.sizeDelta = tamanio;
        TMP_Text texto = objeto.GetComponent<TMP_Text>();
        if (referencia != null)
        {
            texto.font = referencia.font;
            texto.fontSharedMaterial = referencia.fontSharedMaterial;
        }
        texto.raycastTarget = false;
        texto.enableAutoSizing = false;
        texto.overflowMode = TextOverflowModes.Ellipsis;
        EditorUtility.SetDirty(objeto);
        return texto;
    }

    private static GameObject ObtenerOCrear(Transform padre, string nombre, params Type[] componentes)
    {
        Transform existente = padre.Find(nombre);
        if (existente != null) return existente.gameObject;
        GameObject objeto = new GameObject(nombre, componentes);
        objeto.transform.SetParent(padre, false);
        return objeto;
    }

    private static TMP_Text ObtenerTexto(Scene escena, string ruta)
    {
        Transform texto = Buscar(escena, ruta);
        return texto != null ? texto.GetComponent<TMP_Text>() : null;
    }

    private static Transform Buscar(Scene escena, string ruta)
    {
        string[] partes = ruta.Split('/');
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            foreach (Transform candidato in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (candidato.name != partes[0]) continue;
                Transform actual = candidato;
                for (int i = 1; i < partes.Length && actual != null; i++) actual = actual.Find(partes[i]);
                if (actual != null) return actual;
            }
        }
        return null;
    }
}
