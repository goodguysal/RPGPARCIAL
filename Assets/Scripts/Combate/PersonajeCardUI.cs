using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Vista reutilizable con la información de un integrante del grupo.</summary>
public sealed class PersonajeCardUI : MonoBehaviour
{
    [SerializeField] private PersonajeCombate personaje;
    [SerializeField] private string nombreVacio = "ALIADO";
    [SerializeField] private TMP_Text nombreTexto;
    [SerializeField] private TMP_Text vidaTexto;
    [SerializeField] private TMP_Text manaTexto;
    [SerializeField] private TMP_Text iniciativaTexto;
    [SerializeField] private Image vidaRelleno;
    [SerializeField] private Image manaRelleno;

    public PersonajeCombate Personaje => personaje;

    private void Start() => Actualizar();

    private void OnValidate() => Actualizar();

    public void Vincular(PersonajeCombate nuevoPersonaje)
    {
        personaje = nuevoPersonaje;
        Actualizar();
    }

    public void Actualizar()
    {
        if (personaje == null)
        {
            Asignar(nombreTexto, nombreVacio);
            Asignar(vidaTexto, "PV   — / —");
            Asignar(manaTexto, "PM   — / —");
            Asignar(iniciativaTexto, "INI   —");
            if (vidaRelleno != null) vidaRelleno.fillAmount = 0f;
            if (manaRelleno != null) manaRelleno.fillAmount = 0f;
            return;
        }

        int vida = Application.isPlaying ? personaje.Vida : personaje.VidaInicial;
        int mana = Application.isPlaying ? personaje.Mana : personaje.ManaInicial;
        Asignar(nombreTexto, personaje.Nombre.ToUpperInvariant());
        Asignar(vidaTexto, $"PV   {vida} / {personaje.VidaMaxima}");
        Asignar(manaTexto, $"PM   {mana} / {personaje.ManaMaximo}");
        Asignar(iniciativaTexto, $"INI   {personaje.Iniciativa:00}");

        if (vidaRelleno != null)
            vidaRelleno.fillAmount = personaje.VidaMaxima <= 0 ? 0f : (float)vida / personaje.VidaMaxima;
        if (manaRelleno != null)
            manaRelleno.fillAmount = personaje.ManaMaximo <= 0 ? 0f : (float)mana / personaje.ManaMaximo;
    }

    private static void Asignar(TMP_Text texto, string valor)
    {
        if (texto != null) texto.text = valor;
    }
}
