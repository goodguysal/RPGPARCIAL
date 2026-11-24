using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

/// <summary>Vista reutilizable con la información de un integrante del grupo.</summary>
public sealed class PersonajeCardUI : MonoBehaviour
{
    [SerializeField] private PersonajeCombate personaje;
    [SerializeField] private string nombreVacio = "ALIADO";
    [SerializeField] private TMP_Text nombreTexto;
    [SerializeField] private TMP_Text vidaTexto;
    [SerializeField] private TMP_Text manaTexto;
    [FormerlySerializedAs("iniciativaTexto")]
    [SerializeField] private TMP_Text velocidadATBTexto;
    [SerializeField] private Image vidaRelleno;
    [SerializeField] private Image manaRelleno;

    [SerializeField] private Image atbRelleno;
    private Slider atbSlider;
    [SerializeField] private TMP_Text atbTexto;
    private float atbObjetivo;
    private Slider vidaSlider;
    private Slider manaSlider;
    private TMP_Text indicadorObjetivo;
    private float vidaObjetivo;
    private float manaObjetivo;
    private bool valoresInicializados;
    private const float VelocidadAnimacion = 90f;

    public PersonajeCombate Personaje => personaje;

    private void Awake()
    {
        valoresInicializados = false;
        vidaSlider = AsegurarSlider(vidaRelleno);
        manaSlider = AsegurarSlider(manaRelleno);
        if (atbRelleno == null) atbRelleno = transform.Find("ATBBar/Fill")?.GetComponent<Image>();
        if (atbTexto == null) atbTexto = transform.Find("ATBBar/ATBText")?.GetComponent<TMP_Text>();
        if (atbRelleno != null && atbRelleno.transform.parent != null)
        {
            atbSlider = AsegurarATBSlider(atbRelleno);
        }
        Actualizar();
    }

    private void Update()
    {
        if (!Application.isPlaying || !valoresInicializados) return;
        if (vidaSlider != null)
            vidaSlider.value = Mathf.MoveTowards(vidaSlider.value, vidaObjetivo, VelocidadAnimacion * Time.unscaledDeltaTime);
        if (manaSlider != null)
            manaSlider.value = Mathf.MoveTowards(manaSlider.value, manaObjetivo, VelocidadAnimacion * Time.unscaledDeltaTime);
        if (atbSlider != null)
            atbSlider.value = Mathf.MoveTowards(atbSlider.value, atbObjetivo * 100f, 150f * Time.unscaledDeltaTime);
    }

    private void OnValidate() => Actualizar();

    public void Vincular(PersonajeCombate nuevoPersonaje)
    {
        personaje = nuevoPersonaje;
        Actualizar();
    }

    public void ActualizarATB(float porcentaje, bool listo)
    {
        atbObjetivo = Mathf.Clamp01(porcentaje);
        if (atbTexto != null) atbTexto.text = listo ? "LISTO" : $"ATB {Mathf.RoundToInt(atbObjetivo * 100f)}%";
        if (atbRelleno != null)
        {
            atbRelleno.color = listo
                ? new Color(0.96f, 0.77f, 0.28f, 1f)
                : new Color(0.35f, 0.72f, 0.79f, 1f);
            if (!Application.isPlaying && atbSlider != null) atbSlider.SetValueWithoutNotify(atbObjetivo * 100f);
        }
    }
    public void MostrarIndicadorObjetivo(bool mostrar)
    {
        if (indicadorObjetivo == null && mostrar)
        {
            GameObject flecha = new GameObject("TargetArrow", typeof(RectTransform), typeof(TextMeshProUGUI));
            flecha.transform.SetParent(transform, false);
            indicadorObjetivo = flecha.GetComponent<TextMeshProUGUI>();
            indicadorObjetivo.text = "▼";
            indicadorObjetivo.font = nombreTexto != null ? nombreTexto.font : null;
            indicadorObjetivo.fontSize = 22f;
            indicadorObjetivo.color = new Color(1f, 0.78f, 0.24f, 1f);
            indicadorObjetivo.alignment = TextAlignmentOptions.Center;
            indicadorObjetivo.raycastTarget = false;
            indicadorObjetivo.outlineWidth = 0.18f;
            indicadorObjetivo.outlineColor = new Color(0.12f, 0.07f, 0.03f, 1f);

            RectTransform rect = indicadorObjetivo.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(30f, 26f);
            rect.anchoredPosition = new Vector2(0f, 3f);
        }

        if (indicadorObjetivo != null)
            indicadorObjetivo.gameObject.SetActive(mostrar);
    }

    public void Actualizar()
    {
        if (personaje == null)
        {
            Asignar(nombreTexto, nombreVacio);
            Asignar(vidaTexto, "PV   — / —");
            Asignar(manaTexto, "PM   — / —");
            Asignar(velocidadATBTexto, "VEL ATB   —");
            AsignarObjetivo(vidaSlider, 0f, ref vidaObjetivo);
            AsignarObjetivo(manaSlider, 0f, ref manaObjetivo);
            return;
        }

        int vida = Application.isPlaying ? personaje.Vida : personaje.VidaInicial;
        int mana = Application.isPlaying ? personaje.Mana : personaje.ManaInicial;
        Asignar(nombreTexto, personaje.Nombre.ToUpperInvariant());
        Asignar(vidaTexto, $"PV   {vida} / {personaje.VidaMaxima}");
        Asignar(manaTexto, $"PM   {mana} / {personaje.ManaMaximo}");
        Asignar(velocidadATBTexto, $"VEL ATB   {personaje.VelocidadATB:0}");

        AsignarObjetivo(vidaSlider, Mathf.Clamp(vida, 0, personaje.VidaMaxima), ref vidaObjetivo);
        AsignarObjetivo(manaSlider, Mathf.Clamp(mana, 0, personaje.ManaMaximo), ref manaObjetivo);
    }

    private void AsignarObjetivo(Slider slider, float valor, ref float objetivo)
    {
        objetivo = valor;
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = Mathf.Max(1f, personaje != null
            ? (slider == vidaSlider ? personaje.VidaMaxima : personaje.ManaMaximo)
            : 1f);
        if (!Application.isPlaying || !valoresInicializados)
            slider.SetValueWithoutNotify(valor);
        valoresInicializados = true;
    }

    private static Slider AsegurarATBSlider(Image relleno)
    {
        if (relleno == null || relleno.transform.parent == null) return null;
        Slider slider = relleno.transform.parent.GetComponent<Slider>();
        if (slider == null) slider = relleno.transform.parent.gameObject.AddComponent<Slider>();
        slider.minValue = 0f; slider.maxValue = 100f; slider.wholeNumbers = false;
        slider.transition = Selectable.Transition.None; slider.interactable = false;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.handleRect = null; slider.targetGraphic = null; slider.fillRect = relleno.rectTransform;
        relleno.type = Image.Type.Simple;
        return slider;
    }

    private static Slider AsegurarSlider(Image relleno)
    {
        if (relleno == null) return null;
        Transform barra = relleno.transform.parent;
        if (barra == null) return null;
        Slider slider = barra.GetComponent<Slider>();
        if (slider == null) slider = barra.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.handleRect = null;
        slider.targetGraphic = null;
        slider.fillRect = relleno.rectTransform;
        relleno.type = Image.Type.Simple;
        relleno.fillAmount = 1f;
        return slider;
    }

    private static void Asignar(TMP_Text texto, string valor)
    {
        if (texto != null) texto.text = valor;
    }
}
