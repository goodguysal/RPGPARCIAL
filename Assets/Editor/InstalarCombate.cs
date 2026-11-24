using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class InstalarCombate
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string SolicitudUnicaRelativa = "../Temp/restore/InstalarEnemigo.once";

    private static readonly Color Ink = new Color32(17, 19, 22, 242);
    private static readonly Color Card = new Color32(37, 39, 42, 245);
    private static readonly Color CardInner = new Color32(49, 51, 54, 255);
    private static readonly Color MainText = new Color32(239, 234, 222, 255);
    private static readonly Color SubText = new Color32(174, 169, 158, 255);
    private static readonly Color Gold = new Color32(217, 185, 122, 255);
    private static readonly Color Red = new Color32(193, 91, 83, 255);
    private static readonly Color Blue = new Color32(113, 164, 192, 255);
    private static readonly Color Green = new Color32(164, 183, 139, 255);

    [InitializeOnLoadMethod]
    private static void EjecutarSolicitudUnica()
    {
        EditorApplication.delayCall += () =>
        {
            string solicitud = ObtenerRutaSolicitud();
            if (!File.Exists(solicitud)) return;

            try
            {
                InstalarEnEscena();
                File.Delete(solicitud);
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        };
    }

    [MenuItem("Tools/RPG/Crear o actualizar UI de combate")]
    public static void PrepararCombate()
    {
        InstalarEnEscena();
        string solicitud = ObtenerRutaSolicitud();
        if (File.Exists(solicitud)) File.Delete(solicitud);
    }

    private static void InstalarEnEscena()
    {
        Scene scene = SceneManager.GetSceneByPath(RutaEscena);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);

        if (TMP_Settings.defaultFontAsset == null)
            TMP_PackageResourceImporter.ImportResources(true, false, false);

        GameObject jugador = BuscarObjeto(scene, "Personaje");
        if (jugador == null)
            throw new InvalidOperationException("No se encontró Personaje en Assets/Scenes/Combate.unity.");

        PersonajeCombate datos = jugador.GetComponent<PersonajeCombate>();
        if (datos == null)
            datos = Undo.AddComponent<PersonajeCombate>(jugador);

        GameObject enemigo = BuscarObjeto(scene, "Enemigo");
        if (enemigo == null)
            throw new InvalidOperationException("No se encontró Enemigo en Assets/Scenes/Combate.unity.");
        if (enemigo.GetComponent<EnemigoCombate>() == null)
            Undo.AddComponent<EnemigoCombate>(enemigo);

        GameObject antiguo = BuscarObjeto(scene, "CombateUI");
        GameObject canvas = BuscarObjeto(scene, "CombateCanvas");
        if (canvas == null)
        {
            canvas = new GameObject("CombateCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvas, scene);
            Undo.RegisterCreatedObjectUndo(canvas, "Crear Canvas de combate");
        }

        PrepararCanvas(canvas);
        PrepararEventSystem(scene);
        CrearJerarquiaUI(canvas.transform, datos);

        CombateUIController controlador = canvas.GetComponent<CombateUIController>();
        if (controlador == null)
            controlador = Undo.AddComponent<CombateUIController>(canvas);

        EditorUtility.SetDirty(controlador);
        if (antiguo != null && antiguo != canvas)
            Undo.DestroyObjectImmediate(antiguo);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Unity no pudo guardar Assets/Scenes/Combate.unity.");
    }

    private static void PrepararCanvas(GameObject canvasObject)
    {
        canvasObject.name = "CombateCanvas";
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null) canvas = Undo.AddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            Undo.AddComponent<GraphicRaycaster>(canvasObject);
    }

    private static void PrepararEventSystem(Scene scene)
    {
        EventSystem eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventObject, scene);
            Undo.RegisterCreatedObjectUndo(eventObject, "Crear EventSystem");
        }
        else if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
        }
    }

    private static void CrearJerarquiaUI(Transform canvas, PersonajeCombate personaje)
    {
        EliminarHijo(canvas, "Backdrop");
        EliminarHijo(canvas, "LocationLabel");
        EliminarHijo(canvas, "EncounterLabel");
        EliminarHijo(canvas, "EnemyCard");
        EliminarHijo(canvas, "BattlePanel");
        EliminarHijo(canvas, "EndPanel");

        RectTransform screen = (RectTransform)canvas;
        CreatePanel("Backdrop", screen, new Color(0.035f, 0.04f, 0.05f, 0.12f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        CreateText("LocationLabel", screen, "LA SENDA HUNDIDA", 19, Gold, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -24), new Vector2(420, 28), true);
        CreateText("EncounterLabel", screen, "ENCUENTRO 01   ·   UNA FIGURA BLOQUEA EL CAMINO", 13, SubText, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -53), new Vector2(620, 22));

        BuildEnemyCard(screen);
        BuildBattlePanel(screen, personaje);
        BuildEndPanel(screen);
    }

    private static void BuildEnemyCard(RectTransform screen)
    {
        RectTransform card = CreatePanel("EnemyCard", screen, Ink,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -22), new Vector2(350, 90));
        CreateText("EnemyName", card, "ACECHADOR DE TURBA", 17, MainText, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -9), new Vector2(-28, 23), true);
        CreateText("EnemyType", card, "CRIATURA ERRANTE", 12, SubText, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -33), new Vector2(-28, 18));
        CreateBar("EnemyBar", card, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -56), new Vector2(-28, 7), Red);
        CreateText("EnemyHP", card, "42 / 42 PV", 12, MainText, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -64), new Vector2(140, 18));
        CreateText("TurnText", card, "TURNO 01", 12, Gold, TextAlignmentOptions.Right,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -64), new Vector2(120, 18));
    }

    private static void BuildBattlePanel(RectTransform screen, PersonajeCombate personaje)
    {
        RectTransform panel = CreatePanel("BattlePanel", screen, new Color32(17, 18, 20, 248),
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 22), new Vector2(-48, 252));

        RectTransform party = CreatePanel("PartyCards", panel, new Color(0, 0, 0, 0),
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(14, 12), new Vector2(812, -24));
        BuildPartyCard(party, "CharacterSlot1", "ALIADO 01", 0, personaje);
        BuildPartyCard(party, "CharacterSlot2", "ALIADO 02", 272, null);
        BuildPartyCard(party, "CharacterSlot3", "ALIADO 03", 544, null);

        RectTransform actionPanel = CreatePanel("ActionPanel", panel, new Color32(24, 26, 29, 255),
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        actionPanel.offsetMin = new Vector2(838, 12);
        actionPanel.offsetMax = new Vector2(-12, -12);

        CreateText("CommandTitle", actionPanel, "¿QUÉ HARÁ IREN?", 15, Gold, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -10), new Vector2(-32, 22), true);
        CreateText("CombatLog", actionPanel, "El acechador bloquea el camino.", 12, SubText, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -35), new Vector2(-32, 22));

        RectTransform commands = CreatePanel("Commands", actionPanel, new Color(0, 0, 0, 0),
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        BuildCommands(commands);

        RectTransform attacks = CreatePanel("Attacks", actionPanel, new Color(0, 0, 0, 0),
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        BuildAttacks(attacks);
        attacks.gameObject.SetActive(false);

        RectTransform inventory = CreatePanel("Inventory", actionPanel, new Color(0, 0, 0, 0),
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        BuildInventory(inventory);
        inventory.gameObject.SetActive(false);
    }

    private static void BuildPartyCard(RectTransform parent, string cardName, string placeholder, float x, PersonajeCombate personaje)
    {
        RectTransform card = CreatePanel(cardName, parent, Card,
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(x, 0), new Vector2(260, -2));
        CreatePanel("Accent", card, personaje != null ? Gold : new Color32(82, 82, 80, 255),
            new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(4, 0));

        PersonajeCardUI view = card.gameObject.AddComponent<PersonajeCardUI>();
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("personaje").objectReferenceValue = personaje;
        serializedView.FindProperty("nombreVacio").stringValue = placeholder;

        TMP_Text name = CreateText("Name", card, personaje != null ? personaje.Nombre.ToUpperInvariant() : placeholder,
            15, MainText, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -12), new Vector2(156, 22), true);
        TMP_Text initiative = CreateText("Initiative", card, personaje != null ? $"INI {personaje.Iniciativa:00}" : "INI  —",
            12, Gold, TextAlignmentOptions.Right, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -13), new Vector2(82, 20), true);

        TMP_Text hp = CreateText("HP", card, personaje != null ? $"PV   {personaje.VidaInicial} / {personaje.VidaMaxima}" : "PV   — / —",
            12, MainText, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -52), new Vector2(-28, 18));
        Image hpFill = CreateBar("HPBar", card, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -73), new Vector2(-28, 6), Red);

        TMP_Text mp = CreateText("MP", card, personaje != null ? $"PM   {personaje.ManaInicial} / {personaje.ManaMaximo}" : "PM   — / —",
            12, MainText, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -91), new Vector2(-28, 18));
        Image mpFill = CreateBar("MPBar", card, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -112), new Vector2(-28, 6), Blue);

        CreateText("Role", card, personaje != null ? "ACERO   ·   MANÁ" : "SIN ASIGNAR",
            11, SubText, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -139), new Vector2(-28, 18));

        serializedView.FindProperty("nombreTexto").objectReferenceValue = name;
        serializedView.FindProperty("vidaTexto").objectReferenceValue = hp;
        serializedView.FindProperty("manaTexto").objectReferenceValue = mp;
        serializedView.FindProperty("iniciativaTexto").objectReferenceValue = initiative;
        serializedView.FindProperty("vidaRelleno").objectReferenceValue = hpFill;
        serializedView.FindProperty("manaRelleno").objectReferenceValue = mpFill;
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);
    }

    private static void BuildCommands(RectTransform root)
    {
        CreateButton("BtnAttack", root, "ATACAR", "Ataques normales y de maná", 16, 82, new Vector2(300, 74), true);
        CreateButton("BtnInventory", root, "INVENTARIO", "Consumibles disponibles", 328, 82, new Vector2(300, 74), true);
        CreateButton("BtnGuard", root, "DEFENDERSE", "Mitiga el próximo golpe", 640, 82, new Vector2(300, 74), true);
    }

    private static void BuildAttacks(RectTransform root)
    {
        CreateText("AttackTitle", root, "ATACAR", 14, Gold, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -9), new Vector2(180, 22), true);
        CreateButton("BtnBackAttacks", root, "VOLVER", "", 833, 7, new Vector2(128, 30), true, true);
        CreateText("NormalHeader", root, "NORMALES   ·   0 PM", 12, Green, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -55), new Vector2(430, 18), true);
        CreateText("ManaHeader", root, "ESPECIALES   ·   GASTAN PM", 12, Blue, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(492, -55), new Vector2(450, 18), true);
        CreateButton("BtnTajo", root, "TAJO", "7–10 daño   ·   0 PM", 16, 78, new Vector2(455, 48), true);
        CreateButton("BtnEstocada", root, "ESTOCADA", "11–15 daño   ·   0 PM", 16, 132, new Vector2(455, 48), true);
        CreateButton("BtnChispa", root, "CHISPA UMBRÍA", "15–19 daño   ·   6 PM", 492, 78, new Vector2(455, 48), true);
        CreateButton("BtnPulso", root, "PULSO ARCANO", "22–27 daño   ·   10 PM", 492, 132, new Vector2(455, 48), true);
    }

    private static void BuildInventory(RectTransform root)
    {
        CreateText("InventoryTitle", root, "INVENTARIO   ·   CONSUMIBLES", 14, Gold, TextAlignmentOptions.Left,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -9), new Vector2(500, 22), true);
        CreateButton("BtnBackInventory", root, "VOLVER", "", 833, 7, new Vector2(128, 30), true, true);
        CreateButton("BtnVenda", root, "VENDA AMARGA", "Recupera 14 PV   ·   QUEDA 1", 16, 86, new Vector2(455, 68), true);
        CreateButton("BtnTonico", root, "TÓNICO DE LUCIÉRNAGA", "Recupera 8 PM   ·   QUEDA 1", 492, 86, new Vector2(455, 68), true);
    }

    private static void BuildEndPanel(RectTransform screen)
    {
        RectTransform end = CreatePanel("EndPanel", screen, new Color(0.035f, 0.04f, 0.05f, 0.88f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        CreateText("EndTitle", end, "VICTORIA", 34, Gold, TextAlignmentOptions.Center,
            new Vector2(0.15f, 0.56f), new Vector2(0.85f, 0.68f), Vector2.zero, Vector2.zero, true);
        CreateText("EndMessage", end, "El acechador cae entre los juncos.", 16, MainText, TextAlignmentOptions.Center,
            new Vector2(0.15f, 0.43f), new Vector2(0.85f, 0.53f), Vector2.zero, Vector2.zero);
        CreateButton("BtnRestart", end, "VOLVER A JUGAR", "", -155, -104, new Vector2(310, 58), true, false);
        RectTransform restart = (RectTransform)end.Find("BtnRestart");
        restart.anchorMin = restart.anchorMax = new Vector2(0.5f, 0.34f);
        restart.pivot = new Vector2(0.5f, 0.5f);
        restart.anchoredPosition = Vector2.zero;
        end.gameObject.SetActive(false);
    }

    private static Button CreateButton(string name, RectTransform parent, string title, string detail,
        float x, float y, Vector2 size, bool enabled, bool compact = false)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = CardInner;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.interactable = enabled;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = CardInner;
        colors.highlightedColor = new Color32(83, 70, 48, 255);
        colors.selectedColor = new Color32(75, 63, 43, 255);
        colors.pressedColor = new Color32(117, 92, 52, 255);
        colors.disabledColor = new Color32(45, 46, 47, 210);
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        Color labelColor = enabled ? MainText : new Color32(125, 124, 120, 255);
        if (compact)
        {
            CreateText("Label", rect, title, 11, Gold, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
        }
        else
        {
            CreateText("Label", rect, title, 14, labelColor, TextAlignmentOptions.Left,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -8), new Vector2(-28, 24), true);
            if (!string.IsNullOrEmpty(detail))
                CreateText("Detail", rect, detail, 11, SubText, TextAlignmentOptions.Left,
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(14, 7), new Vector2(-28, 18));
        }

        CreatePanel("Accent", rect, enabled ? Gold : new Color32(80, 80, 78, 255),
            new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(3, 0));
        return button;
    }

    private static RectTransform CreatePanel(string name, RectTransform parent, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin == Vector2.zero && anchorMax == Vector2.one ? new Vector2(0.5f, 0.5f) : anchorMin;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static TMP_Text CreateText(string name, RectTransform parent, string value, float fontSize, Color color,
        TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, bool bold = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin == Vector2.zero && anchorMax == Vector2.one ? new Vector2(0.5f, 0.5f) : anchorMin;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateBar(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 size, Color color)
    {
        RectTransform background = CreatePanel(name, parent, new Color32(65, 63, 59, 255), anchorMin, anchorMax, position, size);
        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(background, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        Image fill = fillObject.GetComponent<Image>();
        fill.color = color;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        return fill;
    }

    private static void EliminarHijo(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            Undo.DestroyObjectImmediate(child.gameObject);
    }

    private static GameObject BuscarObjeto(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName) return root;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child.gameObject;
        }
        return null;
    }

    private static string ObtenerRutaSolicitud()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, SolicitudUnicaRelativa));
    }
}
