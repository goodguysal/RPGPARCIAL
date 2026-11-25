using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Coloca avisos de estados junto a PV y avisos de recuperación junto a las barras ATB.</summary>
public static class InstalarAvisosEstadosATB
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string ArchivoSolicitud = "InstalarAvisosEstadosATB.once";

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
                Debug.Log("Avisos de debuff y ATB añadidos al Canvas de Combate.");
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

    [MenuItem("Tools/RPG/Instalar avisos de estados y ATB")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play Mode antes de modificar los avisos del Canvas.");

        Scene escena = SceneManager.GetSceneByPath(RutaEscena);
        bool abiertaAqui = !escena.IsValid() || !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
        try
        {
            PersonajeCardUI[] fichas = ObtenerRaices(escena);
            foreach (PersonajeCardUI ficha in fichas)
            {
                Transform card = ficha.transform;
                Reposicionar(card, "MP", new Vector2(14f, -101f));
                Reposicionar(card, "MPBar", new Vector2(14f, -122f));
                Reposicionar(card, "Role", new Vector2(14f, -151f));
                CrearTexto(card, "DebuffText", BuscarTexto(card, "HP"), new Vector2(14f, -83f),
                    new Vector2(-28f, 14f), 8.5f, new Color32(255, 151, 116, 255));
            }

            Transform enemigo = Buscar(escena, "EnemyCard");
            if (enemigo == null) throw new InvalidOperationException("No se encontró EnemyCard en el Canvas.");
            RectTransform tarjeta = enemigo.GetComponent<RectTransform>();
            if (tarjeta != null) tarjeta.sizeDelta = new Vector2(tarjeta.sizeDelta.x, 132f);
            Reposicionar(enemigo, "EnemyType", new Vector2(14f, -31f));
            Reposicionar(enemigo, "EnemyBar", new Vector2(14f, -51f));
            Reposicionar(enemigo, "EnemyHP", new Vector2(14f, -60f));
            Reposicionar(enemigo, "TurnText", new Vector2(-14f, -60f));
            Reposicionar(enemigo, "ATBBar", new Vector2(14f, -93f));
            CrearTexto(enemigo, "DebuffText", BuscarTexto(enemigo, "EnemyHP"), new Vector2(14f, -76f),
                new Vector2(-28f, 13f), 9f, new Color32(255, 151, 116, 255));
            TMP_Text avisoATB = CrearTexto(enemigo, "ATBEffectText", BuscarTexto(enemigo, "EnemyName"), new Vector2(14f, -107f),
                new Vector2(-28f, 14f), 9f, new Color32(255, 211, 123, 255));
            if (avisoATB != null) avisoATB.alignment = TextAlignmentOptions.Center;

            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena))
                throw new InvalidOperationException("No se pudo guardar Combate.unity.");
        }
        finally { if (abiertaAqui) EditorSceneManager.CloseScene(escena, true); }
    }

    private static PersonajeCardUI[] ObtenerRaices(Scene escena)
    {
        var lista = new System.Collections.Generic.List<PersonajeCardUI>();
        foreach (GameObject raiz in escena.GetRootGameObjects())
            lista.AddRange(raiz.GetComponentsInChildren<PersonajeCardUI>(true));
        if (lista.Count == 0) throw new InvalidOperationException("No se encontraron las fichas PersonajeCardUI.");
        return lista.ToArray();
    }

    private static TMP_Text CrearTexto(Transform padre, string nombre, TMP_Text referencia, Vector2 posicion,
        Vector2 tamanio, float fuente, Color color)
    {
        Transform existente = padre.Find(nombre);
        GameObject objeto = existente != null ? existente.gameObject : new GameObject(nombre, typeof(RectTransform), typeof(TextMeshProUGUI));
        if (existente == null) objeto.transform.SetParent(padre, false);
        RectTransform rect = objeto.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = posicion;
        rect.sizeDelta = tamanio;
        TMP_Text texto = objeto.GetComponent<TMP_Text>();
        if (referencia != null)
        {
            texto.font = referencia.font;
            texto.fontSharedMaterial = referencia.fontSharedMaterial;
        }
        texto.text = string.Empty;
        texto.fontSize = fuente;
        texto.color = color;
        texto.alignment = TextAlignmentOptions.Left;
        texto.raycastTarget = false;
        texto.overflowMode = TextOverflowModes.Ellipsis;
        EditorUtility.SetDirty(objeto);
        return texto;
    }

    private static TMP_Text BuscarTexto(Transform raiz, string nombre) => raiz.Find(nombre)?.GetComponent<TMP_Text>();

    private static void Reposicionar(Transform raiz, string nombre, Vector2 posicion)
    {
        RectTransform rect = raiz.Find(nombre) as RectTransform;
        if (rect != null) rect.anchoredPosition = posicion;
    }

    private static Transform Buscar(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform hijo in raiz.GetComponentsInChildren<Transform>(true))
                if (hijo.name == nombre) return hijo;
        return null;
    }
}
