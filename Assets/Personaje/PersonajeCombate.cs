using UnityEngine;

/// <summary>Estado y acciones propias del personaje principal de la prueba de combate.</summary>
public sealed class PersonajeCombate : MonoBehaviour
{
    [Header("Personaje")]
    [SerializeField] private string nombre = "DOCTOR";
    [SerializeField, Min(1)] private int vidaMaxima = 36;
    [SerializeField, Min(0)] private int manaMaximo = 24;
    [SerializeField, Min(0)] private int vidaInicial = 36;
    [SerializeField, Min(0)] private int manaInicial = 18;
    [SerializeField, Min(0)] private int iniciativa = 12;
    [SerializeField] private int vendasIniciales = 1;
    [SerializeField] private int tonicosIniciales = 1;
    [SerializeField] private bool esCura;
    [SerializeField, Min(0)] private int costoRegenerarMana = 4;
    [SerializeField, Min(1)] private int cantidadRegenerarMana = 8;

    public string Nombre => nombre;
    public int VidaMaxima => vidaMaxima;
    public int ManaMaximo => manaMaximo;
    public int VidaInicial => Mathf.Clamp(vidaInicial, 0, vidaMaxima);
    public int ManaInicial => Mathf.Clamp(manaInicial, 0, manaMaximo);
    public int Iniciativa => iniciativa;
    public int Vida { get; private set; }
    public int Mana { get; private set; }
    public int Vendas { get; private set; }
    public int Tonicos { get; private set; }
    public bool EsCura => esCura;
    public int CostoRegenerarMana => costoRegenerarMana;
    public int CantidadRegenerarMana => cantidadRegenerarMana;

    private Animator animator;
    private static readonly int AtaqueTrigger = Animator.StringToHash("Attack");
    private static readonly int SmashTrigger = Animator.StringToHash("Smash");
    private static readonly int HealTrigger = Animator.StringToHash("Heal");
    private static readonly int GolpeTrigger = Animator.StringToHash("Hit");
    private static readonly int MuerteTrigger = Animator.StringToHash("Death");
    private static readonly int EstadoIdle = Animator.StringToHash("Base Layer.Idle");

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>(true);
        Reiniciar();
    }

    public void Reiniciar()
    {
        Vida = Mathf.Clamp(vidaInicial, 0, vidaMaxima);
        Mana = Mathf.Clamp(manaInicial, 0, manaMaximo);
        Vendas = Mathf.Max(0, vendasIniciales);
        Tonicos = Mathf.Max(0, tonicosIniciales);
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.ResetTrigger(AtaqueTrigger);
            animator.ResetTrigger(SmashTrigger);
            animator.ResetTrigger(HealTrigger);
            animator.ResetTrigger(GolpeTrigger);
            animator.ResetTrigger(MuerteTrigger);
            animator.Play(EstadoIdle, 0, 0f);
        }
    }

    public bool PuedeUsarAtaque(TipoAtaque tipo)
    {
        if ((tipo == TipoAtaque.GolpeBaculo || tipo == TipoAtaque.RayoSagrado) && !esCura)
            return false;
        AtaqueCombate ataque = ObtenerAtaque(tipo);
        return Mana >= ataque.costoMana;
    }

    public bool UsarAtaque(TipoAtaque tipo, out int dano)
    {
        AtaqueCombate ataque = ObtenerAtaque(tipo);
        dano = 0;
        if ((tipo == TipoAtaque.GolpeBaculo || tipo == TipoAtaque.RayoSagrado) && !esCura)
            return false;
        if (Mana < ataque.costoMana)
            return false;

        Mana -= ataque.costoMana;
        dano = UnityEngine.Random.Range(ataque.danoMinimo, ataque.danoMaximo + 1);
        int triggerAnimacion = !esCura
            ? AtaqueTrigger
            : tipo == TipoAtaque.GolpeBaculo ? SmashTrigger : HealTrigger;
        ActivarAnimacion(triggerAnimacion);
        return true;
    }

    public bool UsarVenda(out int vidaRecuperada)
    {
        vidaRecuperada = 0;
        if (Vendas <= 0 || Vida >= vidaMaxima)
            return false;

        Vendas--;
        int vidaAnterior = Vida;
        Vida = Mathf.Min(vidaMaxima, Vida + 14);
        vidaRecuperada = Vida - vidaAnterior;
        return true;
    }

    public bool UsarTonico(out int manaRecuperado)
    {
        manaRecuperado = 0;
        if (Tonicos <= 0 || Mana >= manaMaximo)
            return false;

        Tonicos--;
        int manaAnterior = Mana;
        Mana = Mathf.Min(manaMaximo, Mana + 8);
        manaRecuperado = Mana - manaAnterior;
        return true;
    }

    public void RecibirDano(int cantidad)
    {
        int dano = Mathf.Max(0, cantidad);
        if (dano == 0 || Vida <= 0) return;

        Vida = Mathf.Max(0, Vida - dano);
        if (Vida == 0)
        {
            if (animator != null) animator.ResetTrigger(GolpeTrigger);
            ActivarAnimacion(MuerteTrigger);
        }
        else
        {
            ActivarAnimacion(GolpeTrigger);
        }
    }

    public bool UsarRegenerarMana(PersonajeCombate objetivo, out int manaRecuperado)
    {
        manaRecuperado = 0;
        if (!esCura || objetivo == null || objetivo.Vida <= 0 || Mana < costoRegenerarMana)
            return false;
        if (objetivo != this && objetivo.Mana >= objetivo.ManaMaximo)
            return false;

        Mana -= costoRegenerarMana;
        manaRecuperado = objetivo.RecuperarMana(cantidadRegenerarMana);
        if (manaRecuperado > 0)
            ActivarAnimacion(HealTrigger);
        return manaRecuperado > 0;
    }

    public int RecuperarMana(int cantidad)
    {
        int manaAnterior = Mana;
        Mana = Mathf.Min(manaMaximo, Mana + Mathf.Max(0, cantidad));
        return Mana - manaAnterior;
    }

    private void ActivarAnimacion(int trigger)
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetTrigger(trigger);
    }

    private static AtaqueCombate ObtenerAtaque(TipoAtaque tipo)
    {
        switch (tipo)
        {
            case TipoAtaque.Estocada:
                return new AtaqueCombate(11, 15, 0);
            case TipoAtaque.ChispaUmbria:
                return new AtaqueCombate(15, 19, 6);
            case TipoAtaque.PulsoArcano:
                return new AtaqueCombate(22, 27, 10);
            case TipoAtaque.GolpeBaculo:
                return new AtaqueCombate(5, 8, 0);
            case TipoAtaque.RayoSagrado:
                return new AtaqueCombate(22, 27, 10);
            default:
                return new AtaqueCombate(7, 10, 0);
        }
    }

    private readonly struct AtaqueCombate
    {
        public readonly int danoMinimo;
        public readonly int danoMaximo;
        public readonly int costoMana;

        public AtaqueCombate(int minimo, int maximo, int costo)
        {
            danoMinimo = minimo;
            danoMaximo = maximo;
            costoMana = costo;
        }
    }
}

public enum TipoAtaque
{
    Tajo,
    Estocada,
    ChispaUmbria,
    PulsoArcano,
    GolpeBaculo,
    RayoSagrado
}
