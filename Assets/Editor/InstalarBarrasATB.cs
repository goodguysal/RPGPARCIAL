using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Crea medidores ATB persistentes dentro del Canvas de combate.</summary>
public static class InstalarBarrasATB
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string Solicitud = "InstalarBarrasATB.once";

    [InitializeOnLoadMethod]
    private static void EjecutarSolicitudUnica() => EditorApplication.delayCall += EjecutarSolicitudSegura;

    private static void EjecutarSolicitudSegura()
    {
        string marker = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "restore", Solicitud);
        if (!File.Exists(marker)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= ReintentarAlVolverAEdicion;
            EditorApplication.playModeStateChanged += ReintentarAlVolverAEdicion;
            return;
        }
        try
        {
            Instalar();
            File.Delete(marker);
            Debug.Log("Medidores ATB guardados en las fichas y en la tarjeta del GOLEM.");
        }
        catch (Exception excepcion) { Debug.LogException(excepcion); }
    }

    private static void ReintentarAlVolverAEdicion(PlayModeStateChange estado)
    {
        if (estado != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= ReintentarAlVolverAEdicion;
        EditorApplication.delayCall += EjecutarSolicitudSegura;
    }

    [MenuItem("Tools/RPG/Instalar medidores ATB en el Canvas")]
    public static void Instalar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play Mode antes de instalar los medidores ATB.");
        Scene escena = SceneManager.GetSceneByPath(RutaEscena);
        bool abiertaAqui = !escena.IsValid() || !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
        try
        {
            PersonajeCardUI[] fichas = escena.GetRootGameObjects()
                .SelectMany(raiz => raiz.GetComponentsInChildren<PersonajeCardUI>(true))
                .ToArray();
            if (fichas.Length == 0) throw new InvalidOperationException("No se encontraron fichas PersonajeCardUI en Combate.unity.");
            PersonajeCombate[] combatientes = escena.GetRootGameObjects()
                .SelectMany(raiz => raiz.GetComponentsInChildren<PersonajeCombate>(true))
                .ToArray();
            foreach (PersonajeCombate personaje in combatientes)
            {
                SerializedObject datos = new SerializedObject(personaje);
                SerializedProperty velocidad = datos.FindProperty("velocidadATB");
                if (velocidad != null) velocidad.floatValue = personaje.EsEsqueleto ? 14f : personaje.EsCura ? 10f : 12f;
                datos.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (PersonajeCardUI ficha in fichas)
                CrearBarra(ficha.gameObject, BuscarTexto(ficha.transform, "Initiative"), -37f, 10f, 8f);

            Transform tarjetaEnemigo = Buscar(escena, "EnemyCard");
            if (tarjetaEnemigo == null) throw new InvalidOperationException("No se encontró EnemyCard en el Canvas.");
            CrearBarra(tarjetaEnemigo.gameObject, BuscarTexto(tarjetaEnemigo, "EnemyName"), -39f, 10f, 9f);

            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena))
                throw new InvalidOperationException("No se pudo guardar Combate.unity.");
        }
        finally { if (abiertaAqui) EditorSceneManager.CloseScene(escena, true); }
    }

    [MenuItem("Tools/RPG/Instalar medidores ATB en el Canvas", true)]
    private static bool PuedeInstalar() => !EditorApplication.isPlayingOrWillChangePlaymode;

    private static void CrearBarra(GameObject propietario, TMP_Text referencia, float y, float alto, float tamanioTexto)
    {
        Transform previa = propietario.transform.Find("ATBBar");
        if (previa != null) Undo.DestroyObjectImmediate(previa.gameObject);

        GameObject raiz = new GameObject("ATBBar", typeof(RectTransform), typeof(Image), typeof(Slider));
        Undo.RegisterCreatedObjectUndo(raiz, "Crear medidor ATB");
        raiz.transform.SetParent(propietario.transform, false);
        RectTransform rectRaiz = raiz.GetComponent<RectTransform>();
        rectRaiz.anchorMin = new Vector2(0f, 1f);
        rectRaiz.anchorMax = new Vector2(1f, 1f);
        rectRaiz.pivot = new Vector2(0f, 1f);
        rectRaiz.anchoredPosition = new Vector2(14f, y);
        rectRaiz.sizeDelta = new Vector2(-28f, alto);
        Image fondo = raiz.GetComponent<Image>();
        fondo.color = new Color(0.07f, 0.09f, 0.12f, 0.95f);
        fondo.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(raiz.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fill = fillObject.GetComponent<Image>();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;
        fill.color = new Color(0.35f, 0.72f, 0.79f, 1f);
        fill.raycastTarget = false;

        Slider slider = raiz.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.value = 0f;
        slider.wholeNumbers = false;
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.handleRect = null;
        slider.targetGraphic = null;
        slider.fillRect = fillRect;
        fill.type = Image.Type.Simple;

        GameObject textObject = new GameObject("ATBText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(raiz.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        if (referencia != null)
        {
            text.font = referencia.font;
            text.fontSharedMaterial = referencia.fontSharedMaterial;
            text.color = Color.white;
        }
        text.fontSize = tamanioTexto;
        text.alignment = TextAlignmentOptions.Center;
        text.text = "ATB 0%";
        text.raycastTarget = false;
        text.outlineWidth = 0.12f;
        text.outlineColor = new Color(0.02f, 0.02f, 0.03f, 1f);
        EditorUtility.SetDirty(propietario);
    }

    private static TMP_Text BuscarTexto(Transform raiz, string nombre)
    {
        Transform elemento = raiz.Find(nombre);
        return elemento != null ? elemento.GetComponent<TMP_Text>() : null;
    }

    private static Transform Buscar(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform hijo in raiz.GetComponentsInChildren<Transform>(true))
                if (hijo.name == nombre) return hijo;
        return null;
    }
}
