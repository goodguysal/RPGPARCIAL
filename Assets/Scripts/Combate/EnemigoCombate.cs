using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Estado y decisiones básicas del enemigo del encuentro.</summary>
public sealed class EnemigoCombate : MonoBehaviour
{
    [Header("Enemigo")]
    [SerializeField] private string nombre = "GOLEM";
    [SerializeField, Min(1)] private int vidaMaxima = 180;
    [SerializeField, Min(0)] private int vidaInicial = 180;
    [SerializeField, Min(0)] private int manaMaximo = 24;
    [SerializeField, Min(0)] private int manaInicial = 18;
    [SerializeField, Min(0)] private int costoEmbateMana = 4;
    [SerializeField, Min(1f)] private float velocidadATB = 12f;
    [SerializeField, Range(0f, 1f)] private float probabilidadParalisis = 0.2f;

    [Header("Decisiones")]
    [SerializeField, Range(0f, 1f)] private float probabilidadGolpeFuerte = 0.25f;
    [SerializeField, Range(0f, 1f)] private float probabilidadGolpeFuerteHerido = 0.55f;
    [SerializeField] private AtaqueEnemigo[] ataques =
    {
        new AtaqueEnemigo("Zarpazo", 5, 8),
        new AtaqueEnemigo("Embate de turba", 8, 12)
    };

    public string Nombre => nombre;
    public int VidaMaxima => vidaMaxima;
    public int Vida { get; private set; }
    public int ManaMaximo => manaMaximo;
    public int Mana { get; private set; }
    public float ATB { get; private set; }
    public float VelocidadATB => Mathf.Max(1f, velocidadATB);
    public float PorcentajeATB => Mathf.Clamp01(ATB / 100f);
    public bool ListoParaActuar => Vida > 0 && ATB >= 100f;
    public int RondasVenenoRestantes { get; private set; }
    public int DanoVeneno { get; private set; }
    private PersonajeCombate objetivoForzadoProximoAtaque;
    public bool ATBAcelerado => multiplicadorATBActivo > 1f || multiplicadorATBPreparado > 1f;
    public string AvisoAceleracionATB
    {
        get
        {
            float multiplicador = Mathf.Max(multiplicadorATBActivo, multiplicadorATBPreparado);
            if (multiplicador <= 1f) return string.Empty;
            string momento = multiplicadorATBPreparado > 1f ? "tras su próxima acción" : "en el llenado actual";
            return $"GOLEM: ATB +{(multiplicador - 1f):P0} · {momento}";
        }
    }

    private float multiplicadorATBActivo = 1f;
    private float multiplicadorATBPreparado = 1f;

    private Animator animator;
    private static readonly int AtaqueTrigger = Animator.StringToHash("Attack");
    private static readonly int GolpeTrigger = Animator.StringToHash("Hit");
    private static readonly int EstadoIdle = Animator.StringToHash("Base Layer.Idle");

    private void Awake() => Reiniciar();

    public void Reiniciar()
    {
        Vida = Mathf.Clamp(vidaInicial, 0, vidaMaxima);
        Mana = Mathf.Clamp(manaInicial, 0, manaMaximo);
        ATB = 0f;
        DanoVeneno = 0;
        RondasVenenoRestantes = 0;
        objetivoForzadoProximoAtaque = null;
        PlayerPrefs.DeleteKey("RPG1.GOLEM.Veneno.Dano");
        PlayerPrefs.DeleteKey("RPG1.GOLEM.Veneno.Partidas");
        multiplicadorATBActivo = 1f;
        multiplicadorATBPreparado = 1f;
        ObtenerAnimator();
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.ResetTrigger(AtaqueTrigger);
        animator.ResetTrigger(GolpeTrigger);
        animator.Play(EstadoIdle, 0, 0f);
    }

    public void AvanzarATB(float deltaTiempo)
    {
        if (Vida <= 0 || ATB >= 100f) return;
        ATB = Mathf.Min(100f, ATB + VelocidadATB * multiplicadorATBActivo * Mathf.Max(0f, deltaTiempo));
    }

    public void ConsumirATB(float costoRecuperacion)
    {
        ATB = Mathf.Max(0f, ATB - Mathf.Max(0f, costoRecuperacion));
        multiplicadorATBActivo = multiplicadorATBPreparado;
        multiplicadorATBPreparado = 1f;
    }

    public void AcelerarProximaRecuperacionATB(float incrementoPorcentual)
    {
        float multiplicador = 1f + Mathf.Max(0f, incrementoPorcentual);
        if (ATB >= 100f)
            multiplicadorATBPreparado = Mathf.Max(multiplicadorATBPreparado, multiplicador);
        else
            multiplicadorATBActivo = Mathf.Max(multiplicadorATBActivo, multiplicador);
    }

    public void AplicarVeneno(int danoPorTurno, int turnos)
    {
        DanoVeneno = Mathf.Max(DanoVeneno, Mathf.Max(1, danoPorTurno));
        RondasVenenoRestantes += Mathf.Max(1, turnos);
    }

    public int ProcesarVenenoAlFinalDeRonda()
    {
        if (RondasVenenoRestantes <= 0 || Vida <= 0) return 0;
        int dano = Mathf.Min(Vida, DanoVeneno);
        RecibirDano(dano);
        RondasVenenoRestantes--;
        if (RondasVenenoRestantes == 0) DanoVeneno = 0;
        return dano;
    }

    public void ForzarProximoObjetivo(PersonajeCombate objetivo)
    {
        if (objetivo != null && objetivo.Vida > 0)
            objetivoForzadoProximoAtaque = objetivo;
    }

    public void AcelerarATB(float cantidad)
    {
        if (Vida <= 0) return;
        ATB = Mathf.Min(100f, ATB + Mathf.Max(0f, cantidad));
    }
    public void RecibirDano(int cantidad)
    {
        int dano = Mathf.Max(0, cantidad);
        Vida = Mathf.Max(0, Vida - dano);
        if (dano > 0) ActivarAnimacion(GolpeTrigger);
    }

    /// <summary>Elige ataque y objetivo, pero reserva el daño para el momento del impacto.</summary>
    public AccionEnemigaPendiente PrepararAccion(PersonajeCombate[] posiblesObjetivos, PersonajeCombate objetivoProtegido = null)
    {
        ICollection<PersonajeCombate> protegidos = objetivoProtegido != null
            ? new[] { objetivoProtegido }
            : null;
        return PrepararAccionConProteccion(posiblesObjetivos, protegidos);
    }

    public AccionEnemigaPendiente PrepararAccionConProteccion(PersonajeCombate[] posiblesObjetivos,
        ICollection<PersonajeCombate> objetivosProtegidos)
    {
        List<PersonajeCombate> vivos = new List<PersonajeCombate>();
        if (posiblesObjetivos != null)
            foreach (PersonajeCombate candidato in posiblesObjetivos)
                if (candidato != null && candidato.isActiveAndEnabled && candidato.Vida > 0)
                    vivos.Add(candidato);

        if (vivos.Count == 0 || ataques == null || ataques.Length == 0)
            return default;

        if (objetivoForzadoProximoAtaque != null && !vivos.Contains(objetivoForzadoProximoAtaque))
            objetivoForzadoProximoAtaque = null;
        PersonajeCombate objetivo = objetivoForzadoProximoAtaque
            ?? vivos.Find(personaje => personaje.ContraataquePreparado)
            ?? ElegirObjetivo(vivos);
        AtaqueEnemigo ataque = ElegirAtaque();
        int dano = UnityEngine.Random.Range(ataque.danoMinimo, ataque.danoMaximo + 1);
        bool defendido = EstaProtegido(objetivo, objetivosProtegidos);
        if (defendido) dano = Mathf.CeilToInt(dano * 0.5f);
        int costoMana = ataque.nombre == "Embate de turba" ? costoEmbateMana : 0;
        if (Mana < costoMana)
        {
            ataque = ataques[0];
            costoMana = 0;
        }
        bool paraliza = UnityEngine.Random.value < probabilidadParalisis;
        string nombreAtaque = paraliza ? $"{ataque.nombre} paralizante" : ataque.nombre;
        return new AccionEnemigaPendiente(nombreAtaque, objetivo, dano, defendido, costoMana, paraliza);
    }

    private static bool EstaProtegido(PersonajeCombate objetivo, ICollection<PersonajeCombate> protegidos)
    {
        if (protegidos == null) return false;
        foreach (PersonajeCombate personaje in protegidos)
            if (personaje == objetivo) return true;
        return false;
    }

    public ResultadoAccionEnemiga EjecutarAccion(AccionEnemigaPendiente accion)
    {
        if (accion.objetivo == null || !accion.objetivo.isActiveAndEnabled || accion.objetivo.Vida <= 0)
            return default;
        if (accion.objetivo == objetivoForzadoProximoAtaque)
            objetivoForzadoProximoAtaque = null;
        Mana = Mathf.Max(0, Mana - accion.costoMana);
        ActivarAnimacion(AtaqueTrigger);
        if (accion.objetivo.BloquearYDevolverGolpe(accion.dano, out int danoDevuelto))
        {
            RecibirDano(danoDevuelto);
            return new ResultadoAccionEnemiga(accion.nombreAtaque, accion.objetivo, 0, accion.objetivoDefendido, true, danoDevuelto, false);
        }
        accion.objetivo.RecibirDano(accion.dano);
        if (accion.aplicaParalisis && accion.objetivo.Vida > 0) accion.objetivo.AplicarParalisis(1);
        return new ResultadoAccionEnemiga(accion.nombreAtaque, accion.objetivo, accion.dano, accion.objetivoDefendido, false, 0,
            accion.aplicaParalisis && accion.objetivo.Vida > 0);
    }

    private void ActivarAnimacion(int trigger)
    {
        ObtenerAnimator();
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetTrigger(trigger);
    }

    private void ObtenerAnimator()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    private PersonajeCombate ElegirObjetivo(List<PersonajeCombate> objetivos)
    {
        PersonajeCombate elegido = objetivos[0];
        float mejorPuntaje = float.MinValue;
        foreach (PersonajeCombate objetivo in objetivos)
        {
            float proporcionVida = objetivo.VidaMaxima <= 0 ? 0f : (float)objetivo.Vida / objetivo.VidaMaxima;
            float puntaje = (1f - proporcionVida) * 0.7f + UnityEngine.Random.value * 0.3f;
            if (puntaje > mejorPuntaje) { mejorPuntaje = puntaje; elegido = objetivo; }
        }
        return elegido;
    }

    private AtaqueEnemigo ElegirAtaque()
    {
        if (ataques.Length == 1) return ataques[0];
        float vidaProporcional = vidaMaxima <= 0 ? 1f : (float)Vida / vidaMaxima;
        float probabilidad = vidaProporcional <= 0.5f ? probabilidadGolpeFuerteHerido : probabilidadGolpeFuerte;
        AtaqueEnemigo elegido = UnityEngine.Random.value < probabilidad ? ataques[1] : ataques[0];
        if (elegido.nombre == "Embate de turba" && Mana < costoEmbateMana)
            return ataques[0];
        return elegido;
    }
    [Serializable]
    private struct AtaqueEnemigo
    {
        public string nombre;
        [Min(0)] public int danoMinimo;
        [Min(0)] public int danoMaximo;
        public AtaqueEnemigo(string nombre, int danoMinimo, int danoMaximo)
        { this.nombre = nombre; this.danoMinimo = danoMinimo; this.danoMaximo = danoMaximo; }
    }
}

public readonly struct AccionEnemigaPendiente
{
    public readonly string nombreAtaque;
    public readonly PersonajeCombate objetivo;
    public readonly int dano;
    public readonly bool objetivoDefendido;
    public readonly int costoMana;
    public readonly bool aplicaParalisis;
    public AccionEnemigaPendiente(string nombreAtaque, PersonajeCombate objetivo, int dano, bool objetivoDefendido, int costoMana, bool aplicaParalisis)
    {
        this.nombreAtaque = nombreAtaque;
        this.objetivo = objetivo;
        this.dano = dano;
        this.objetivoDefendido = objetivoDefendido;
        this.costoMana = costoMana;
        this.aplicaParalisis = aplicaParalisis;
    }
}
public readonly struct ResultadoAccionEnemiga
{
    public readonly string nombreAtaque;
    public readonly PersonajeCombate objetivo;
    public readonly int dano;
    public readonly bool objetivoDefendido;
    public readonly bool contraataque;
    public readonly int danoDevuelto;
    public readonly bool paralisisAplicada;
    public ResultadoAccionEnemiga(string nombreAtaque, PersonajeCombate objetivo, int dano, bool objetivoDefendido,
        bool contraataque, int danoDevuelto, bool paralisisAplicada)
    {
        this.nombreAtaque = nombreAtaque;
        this.objetivo = objetivo;
        this.dano = dano;
        this.objetivoDefendido = objetivoDefendido;
        this.contraataque = contraataque;
        this.danoDevuelto = danoDevuelto;
        this.paralisisAplicada = paralisisAplicada;
    }
}
