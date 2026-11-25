using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Guarda en la escena el aviso visible de daño por veneno y mejora la legibilidad de sus etiquetas.</summary>
public static class InstalarVenenoLegible
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string Solicitud = "InstalarVenenoLegible.once";

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
                Debug.Log("Aviso del daño por veneno, textos legibles y limpieza de etiquetas guardados en Combate.unity.");
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

    [MenuItem("Tools/RPG/Mejorar avisos de veneno y legibilidad")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play Mode antes de guardar los cambios en Combate.unity.");

        Scene escena = SceneManager.GetSceneByPath(RutaEscena);
        bool abiertaAqui = !escena.IsValid() || !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
        try
        {
            foreach (GameObject raiz in escena.GetRootGameObjects())
            {
                foreach (TMP_Text texto in raiz.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (texto.fontSize <= 0f || texto.fontSize >= 30f) continue;
                    texto.fontSize = Mathf.Min(28f, Mathf.Max(13f, Mathf.Round(texto.fontSize * 1.2f * 2f) / 2f));
                    EditorUtility.SetDirty(texto);
                }
            }

            Transform inventario = Buscar(escena, "Inventory");
            Transform vial = inventario != null ? inventario.Find("BtnVial") : null;
            if (vial != null) UnityEngine.Object.DestroyImmediate(vial.gameObject);

            foreach (GameObject raiz in escena.GetRootGameObjects())
                foreach (Transform hijo in raiz.GetComponentsInChildren<Transform>(true))
                    if (hijo.name == "Role")
                    {
                        TMP_Text rol = hijo.GetComponent<TMP_Text>();
                        if (rol != null) rol.text = string.Empty;
                        hijo.gameObject.SetActive(false);
                        EditorUtility.SetDirty(hijo.gameObject);
                    }

            Transform enemigo = Buscar(escena, "EnemyCard");
            if (enemigo == null) throw new InvalidOperationException("No se encontró EnemyCard en el Canvas.");
            RectTransform tarjeta = enemigo.GetComponent<RectTransform>();
            if (tarjeta != null) tarjeta.sizeDelta = new Vector2(tarjeta.sizeDelta.x, 154f);
            Reposicionar(enemigo, "DebuffText", new Vector2(14f, -94f));
            Redimensionar(enemigo, "DebuffText", new Vector2(-28f, 19f));
            Reposicionar(enemigo, "ATBBar", new Vector2(14f, -113f));
            Reposicionar(enemigo, "ATBEffectText", new Vector2(14f, -130f));

            TMP_Text hp = enemigo.Find("EnemyHP")?.GetComponent<TMP_Text>();
            TMP_Text aviso = CrearTexto(enemigo, "PoisonDamageText", hp,
                new Vector2(14f, -76f), new Vector2(-28f, 18f));
            aviso.fontSize = 16f;
            aviso.fontStyle = FontStyles.Bold;
            aviso.color = new Color32(255, 141, 112, 255);
            aviso.alignment = TextAlignmentOptions.Center;
            aviso.text = string.Empty;
            aviso.gameObject.SetActive(false);
            EditorUtility.SetDirty(aviso);

            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena))
                throw new InvalidOperationException("No se pudo guardar Combate.unity.");
        }
        finally { if (abiertaAqui) EditorSceneManager.CloseScene(escena, true); }
    }

    private static TMP_Text CrearTexto(Transform padre, string nombre, TMP_Text referencia, Vector2 posicion, Vector2 tamanio)
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
        texto.raycastTarget = false;
        texto.overflowMode = TextOverflowModes.Ellipsis;
        return texto;
    }

    private static void Reposicionar(Transform raiz, string nombre, Vector2 posicion)
    {
        RectTransform rect = raiz.Find(nombre) as RectTransform;
        if (rect != null) rect.anchoredPosition = posicion;
    }

    private static void Redimensionar(Transform raiz, string nombre, Vector2 tamanio)
    {
        RectTransform rect = raiz.Find(nombre) as RectTransform;
        if (rect != null) rect.sizeDelta = tamanio;
    }

    private static Transform Buscar(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform hijo in raiz.GetComponentsInChildren<Transform>(true))
                if (hijo.name == nombre) return hijo;
        return null;
    }
}
