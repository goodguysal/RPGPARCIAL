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
    [SerializeField, Min(1f)] private float velocidadATB = 12f;
    [SerializeField] private int vendasIniciales = 1;
    [SerializeField] private int tonicosIniciales = 1;
    [SerializeField, Min(0)] private int salesIniciales = 1;
    [SerializeField, Min(0)] private int aceleradoresATBIniciales = 1;
    [SerializeField] private bool esCura;
    [SerializeField] private bool esEsqueleto;
    [SerializeField, Min(0)] private int costoRegenerarMana = 4;
    [SerializeField, Min(1)] private int cantidadRegenerarMana = 8;
    [SerializeField, Min(0)] private int costoCuracionDoctor = 10;
    [SerializeField, Min(1)] private int cantidadCuracionDoctor = 18;
    [SerializeField, Min(0)] private int costoAtaquePesadoEsqueleto = 8;
    [SerializeField, Range(0f, 1f)] private float porcentajeBuffSales = 0.25f;
    [SerializeField, Min(1)] private int duracionSalesAtaques = 3;
    [SerializeField, Min(1)] private int danoVenenoPorTurno = 5;
    [SerializeField, Min(1)] private int duracionVenenoTurnos = 2;
    [SerializeField, Min(0)] private int esenciasBestiaIniciales = 1;
    [SerializeField, Min(1)] private int danoImpulsoMinimo = 24;
    [SerializeField, Min(1)] private int danoImpulsoMaximo = 30;
    [SerializeField, Min(0)] private int costoImpulsoMana = 8;
    [SerializeField, Range(0f, 2f)] private float aceleracionATBImpulso = 0.5f;
    [SerializeField, Range(0f, 2f)] private float aceleracionATBConsumible = 0.5f;
    [SerializeField, Min(1)] private int duracionAceleracionATBPartidas = 2;
    [Header("VFX POR HABILIDAD")]
    [Tooltip("Perfil ScriptableObject con un prefab VFX configurable por cada habilidad y consumible de este personaje.")]
    [InspectorName("Perfil VFX")]
    [SerializeField] private ConfiguracionVFXCombate configuracionVFX;

    [Header("Sonidos")]
    [SerializeField] private AudioClip sonidoGolpe;
    [SerializeField] private AudioClip sonidoRecibirGolpe;
    [SerializeField] private AudioClip sonidoAtaquePM;
    [SerializeField] private AudioClip sonidoMorir;
    [SerializeField] private AudioClip sonidoConsumible;
    [SerializeField] private AudioClip sonidoVenda;
    [SerializeField] private AudioClip sonidoTonico;

    public string Nombre => nombre;
    public int VidaMaxima => vidaMaxima;
    public int ManaMaximo => manaMaximo;
    public int VidaInicial => Mathf.Clamp(vidaInicial, 0, vidaMaxima);
    public int ManaInicial => manaMaximo;

    public int Vida { get; private set; }
    public int Mana { get; private set; }
    public int Vendas { get; private set; }
    public int Tonicos { get; private set; }
    public int Sales { get; private set; }
    public int AceleradoresATB { get; private set; }
    public int PartidasAceleracionATB { get; private set; }
    public int Venenos { get; private set; }
    public int EsenciasBestia { get; private set; }
    public int AtaquesBuffRestantes { get; private set; }
    public int TurnosParalizado { get; private set; }
    public string ResumenEstados
    {
        get
        {
            string resumen = string.Empty;
            if (AtaquesBuffRestantes > 0) resumen = $"ATQ +{PorcentajeBuffSales:P0} · {AtaquesBuffRestantes} usos";
            if (TurnosParalizado > 0) resumen += (resumen.Length > 0 ? " · " : string.Empty) + "PARÁLISIS · NO ACTÚA HASTA FIN DEL COMBATE";
            if (PartidasAceleracionATB > 0) resumen += (resumen.Length > 0 ? " · " : string.Empty) + $"ATB +{aceleracionATBConsumible:P0} · {PartidasAceleracionATB} partidas";
            return resumen;
        }
    }
    public bool EsCura => esCura;
    public bool EsEsqueleto => esEsqueleto;
    public bool EsDoctor => !esCura && !esEsqueleto;
    public int CostoContraataque => 18;
    public bool ContraataquePreparado { get; private set; }
    public int CostoRegenerarMana => costoRegenerarMana;
    public int CantidadRegenerarMana => cantidadRegenerarMana;
    public int CostoCuracionDoctor => costoCuracionDoctor;
    public int CantidadCuracionDoctor => cantidadCuracionDoctor;
    public int CostoAtaquePesadoEsqueleto => costoAtaquePesadoEsqueleto;
    public int DuracionSalesAtaques => duracionSalesAtaques;
    public float PorcentajeBuffSales => porcentajeBuffSales;
    public int DanoVenenoPorTurno => danoVenenoPorTurno;
    public int DuracionVenenoRondas => duracionVenenoTurnos;
    public float AceleracionATBImpulso => aceleracionATBImpulso;
    public int DanoImpulsoMinimo => danoImpulsoMinimo;
    public int DanoImpulsoMaximo => danoImpulsoMaximo;
    public int CostoImpulsoMana => costoImpulsoMana;
    public float ATB { get; private set; }
    public float VelocidadATB => Mathf.Max(1f, velocidadATB * (PartidasAceleracionATB > 0 ? 1f + aceleracionATBConsumible : 1f));
    public float PorcentajeATB => Mathf.Clamp01(ATB / 100f);
    public int PorcentajeAceleracionATBConsumible => Mathf.RoundToInt(aceleracionATBConsumible * 100f);
    public int DuracionAceleracionATBConsumiblePartidas => duracionAceleracionATBPartidas;
    public bool ListoParaActuar => Vida > 0 && ATB >= 100f;

    private Animator animator;
    private AudioSource audioSource;
    private static readonly int AtaqueTrigger = Animator.StringToHash("Attack");
    private static readonly int SmashTrigger = Animator.StringToHash("Smash");
    private static readonly int HealTrigger = Animator.StringToHash("Heal");
    private static readonly int AtaqueMagicoTrigger = Animator.StringToHash("MagicAttack");
    private static readonly int GolpeTrigger = Animator.StringToHash("Hit");
    private static readonly int MuerteTrigger = Animator.StringToHash("Death");
    private static readonly int BloqueoTrigger = Animator.StringToHash("Block");
    private static readonly int DefensaTrigger = Animator.StringToHash("Defend");
    private static readonly int ItemThrowTrigger = Animator.StringToHash("ItemThrow");
    private static readonly int ItemSearchingTrigger = Animator.StringToHash("ItemSearching");
    private const string ClavePartidasAceleracionATB = "RPG1.ATBConsumible.Partidas.";
    private static readonly int EstadoIdle = Animator.StringToHash("Base Layer.Idle");

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>(true);
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        Reiniciar();
    }

    public void Reiniciar()
    {
        Vida = Mathf.Clamp(vidaInicial, 0, vidaMaxima);
        Mana = manaMaximo;
        ContraataquePreparado = false;
        ATB = 0f;
        Vendas = Mathf.Max(0, vendasIniciales);
        Tonicos = Mathf.Max(0, tonicosIniciales);
        Sales = Mathf.Max(0, salesIniciales);
        AceleradoresATB = Mathf.Max(EsCura ? 2 : 1, aceleradoresATBIniciales);
        Venenos = EsDoctor ? 3 : 1;
        EsenciasBestia = Mathf.Max(0, esenciasBestiaIniciales);
        PartidasAceleracionATB = Mathf.Max(0, PlayerPrefs.GetInt(ClavePartidasAceleracionATB + nombre, 0));
        AtaquesBuffRestantes = 0;
        TurnosParalizado = 0;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.ResetTrigger(AtaqueTrigger);
            animator.ResetTrigger(SmashTrigger);
            animator.ResetTrigger(BloqueoTrigger);
            animator.ResetTrigger(DefensaTrigger);
            animator.ResetTrigger(HealTrigger);
            animator.ResetTrigger(AtaqueMagicoTrigger);
            animator.ResetTrigger(GolpeTrigger);
            animator.ResetTrigger(MuerteTrigger);
            animator.ResetTrigger(ItemThrowTrigger);
            animator.ResetTrigger(ItemSearchingTrigger);
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

    public bool UsarAtaque(TipoAtaque tipo, out int dano, Transform objetivoVFX = null)
    {
        AtaqueCombate ataque = ObtenerAtaque(tipo);
        dano = 0;
        if ((tipo == TipoAtaque.GolpeBaculo || tipo == TipoAtaque.RayoSagrado) && !esCura)
            return false;
        if (tipo == TipoAtaque.GolpeImpulsor && !EsDoctor)
            return false;
        if (Mana < ataque.costoMana)
            return false;

        Mana -= ataque.costoMana;
        dano = UnityEngine.Random.Range(ataque.danoMinimo, ataque.danoMaximo + 1);
        if (AtaquesBuffRestantes > 0)
        {
            dano = Mathf.CeilToInt(dano * (1f + porcentajeBuffSales));
            AtaquesBuffRestantes--;
        }
        int triggerAnimacion = !esCura
            ? AtaqueTrigger
            : tipo == TipoAtaque.GolpeBaculo ? SmashTrigger : AtaqueMagicoTrigger;
        ActivarAnimacion(triggerAnimacion);
        ReproducirSonido(ataque.costoMana > 0 ? sonidoAtaquePM : sonidoGolpe);
        ReproducirVFX(ObtenerVFXAtaque(tipo), objetivoVFX);
        return true;
    }

    public bool UsarVenda(out int vidaRecuperada) => UsarVenda(this, out vidaRecuperada);

    public bool UsarVenda(PersonajeCombate objetivo, out int vidaRecuperada)
    {
        vidaRecuperada = 0;
        if (Vendas <= 0 || objetivo == null || objetivo.Vida <= 0 || objetivo.Vida >= objetivo.VidaMaxima)
            return false;

        Vendas--;
        vidaRecuperada = objetivo.RecuperarVida(14);
        ReproducirAnimacionConsumible(true);
        ReproducirVFX(AccionVFXCombate.Venda, objetivo.transform);
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
        ReproducirAnimacionConsumible(false, true);
        ReproducirVFX(AccionVFXCombate.Tonico, transform);
        return true;
    }

    public bool UsarSalesDeCombate()
    {
        if (Sales <= 0 || Vida <= 0) return false;
        Sales--;
        AtaquesBuffRestantes = Mathf.Max(AtaquesBuffRestantes, duracionSalesAtaques);
        ReproducirAnimacionConsumible(false);
        ReproducirVFX(AccionVFXCombate.SalesDeCombate, transform);
        return true;
    }

    public bool UsarVeneno(EnemigoCombate objetivo)
    {
        if (Venenos <= 0 || objetivo == null || objetivo.Vida <= 0) return false;
        Venenos--;
        objetivo.AplicarVeneno(danoVenenoPorTurno, duracionVenenoTurnos);
        ReproducirAnimacionConsumible(false);
        ReproducirVFX(AccionVFXCombate.Veneno, objetivo.transform);
        return true;
    }

    public bool UsarEsenciaBestia(Transform objetivo = null)
    {
        if (EsenciasBestia <= 0 || Vida <= 0) return false;
        EsenciasBestia--;
        ReproducirAnimacionConsumible(false);
        ReproducirVFX(AccionVFXCombate.EsenciaBestia, objetivo != null ? objetivo : transform);
        return true;
    }

    public bool UsarAceleradorATB(PersonajeCombate objetivo)
    {
        if (AceleradoresATB <= 0 || objetivo == null || objetivo.Vida <= 0) return false;
        AceleradoresATB--;
        objetivo.AplicarAceleracionATBConsumible(duracionAceleracionATBPartidas);
        ReproducirAnimacionConsumible(false);
        ReproducirVFX(AccionVFXCombate.AceleradorATB, objetivo.transform);
        return true;
    }

    private void AplicarAceleracionATBConsumible(int partidas)
    {
        PartidasAceleracionATB = Mathf.Max(PartidasAceleracionATB, Mathf.Max(1, partidas));
        PlayerPrefs.SetInt(ClavePartidasAceleracionATB + nombre, PartidasAceleracionATB);
        PlayerPrefs.Save();
    }

    public void ConsumirPartidaAceleracionATB()
    {
        if (PartidasAceleracionATB <= 0) return;
        PartidasAceleracionATB--;
        string clave = ClavePartidasAceleracionATB + nombre;
        if (PartidasAceleracionATB > 0) PlayerPrefs.SetInt(clave, PartidasAceleracionATB);
        else PlayerPrefs.DeleteKey(clave);
        PlayerPrefs.Save();
    }

    public void ReproducirAnimacionConsumible(bool esVenda, bool esTonico = false)
    {
        ActivarAnimacion(esVenda ? ItemSearchingTrigger : ItemThrowTrigger);
        ReproducirSonido(esVenda ? sonidoVenda : esTonico ? sonidoTonico : sonidoConsumible);
    }

    public void AplicarParalisis(int turnos)
    {
        if (Vida > 0) TurnosParalizado = 1;
    }

    public bool ConsumirParalisis()
    {
        return TurnosParalizado > 0;
    }

    public void RecibirDano(int cantidad)
    {
        int dano = Mathf.Max(0, cantidad);
        if (dano == 0 || Vida <= 0) return;

        Vida = Mathf.Max(0, Vida - dano);
        if (Vida == 0)
        {
            ContraataquePreparado = false;
            ATB = 0f;
            if (animator != null) animator.ResetTrigger(GolpeTrigger);
            ActivarAnimacion(MuerteTrigger);
            ReproducirSonido(sonidoMorir != null ? sonidoMorir : sonidoRecibirGolpe);
        }
        else
        {
            ActivarAnimacion(GolpeTrigger);
            ReproducirSonido(sonidoRecibirGolpe);
        }
    }

    public void ReproducirDefensa()
    {
        ActivarAnimacion(DefensaTrigger);
        ReproducirVFX(AccionVFXCombate.Defenderse, transform);
    }

    public bool PrepararContraataque()
    {
        if (!esEsqueleto || Vida <= 0 || Mana < CostoContraataque) return false;
        Mana -= CostoContraataque;
        ContraataquePreparado = true;
        ActivarAnimacion(BloqueoTrigger);
        ReproducirVFX(AccionVFXCombate.GuardiaReflejaEsqueleto, transform);
        return true;
    }

    public bool BloquearYDevolverGolpe(int danoRecibido, out int danoDevuelto)
    {
        danoDevuelto = 0;
        if (!ContraataquePreparado || Vida <= 0) return false;
        ContraataquePreparado = false;
        danoDevuelto = Mathf.Max(0, danoRecibido);
        ActivarAnimacion(BloqueoTrigger);
        return true;
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
        {
            ActivarAnimacion(HealTrigger);
            ReproducirSonido(sonidoAtaquePM);
            ReproducirVFX(AccionVFXCombate.RegenerarManaCura, objetivo.transform);
        }
        return manaRecuperado > 0;
    }

    public int RecuperarMana(int cantidad)
    {
        int manaAnterior = Mana;
        Mana = Mathf.Min(manaMaximo, Mana + Mathf.Max(0, cantidad));
        return Mana - manaAnterior;
    }

    public bool CurarConMana(PersonajeCombate objetivo, out int vidaRecuperada)
    {
        vidaRecuperada = 0;
        if (!EsDoctor || objetivo == null || Vida <= 0 || objetivo.Vida <= 0 ||
            objetivo.Vida >= objetivo.VidaMaxima || Mana < costoCuracionDoctor)
            return false;

        Mana -= costoCuracionDoctor;
        vidaRecuperada = objetivo.RecuperarVida(cantidadCuracionDoctor);
        if (vidaRecuperada > 0)
        {
            ActivarAnimacion(HealTrigger);
            ReproducirSonido(sonidoAtaquePM);
            ReproducirVFX(AccionVFXCombate.SanacionDoctor, objetivo.transform);
        }
        return vidaRecuperada > 0;
    }

    public void AvanzarATB(float deltaTiempo)
    {
        if (Vida <= 0 || ATB >= 100f) return;
        ATB = Mathf.Min(100f, ATB + VelocidadATB * Mathf.Max(0f, deltaTiempo));
    }

    public void ConsumirATB(float costoRecuperacion)
    {
        // Every action spends the ready turn. A recovery cost of zero means no
        // extra delay after that turn is spent, not an infinite ready loop.
        ATB = costoRecuperacion <= 0f ? 0f : Mathf.Max(0f, ATB - costoRecuperacion);
    }

    public void AcelerarATB(float cantidad)
    {
        if (Vida <= 0) return;
        ATB = Mathf.Min(100f, ATB + Mathf.Max(0f, cantidad));
    }
    public int RecuperarVida(int cantidad)
    {
        int vidaAnterior = Vida;
        Vida = Mathf.Min(vidaMaxima, Vida + Mathf.Max(0, cantidad));
        return Vida - vidaAnterior;
    }

    private void ActivarAnimacion(int trigger)
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetTrigger(trigger);
    }

    private void ReproducirSonido(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource == null) audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        audioSource.PlayOneShot(clip);
    }

    private AccionVFXCombate ObtenerVFXAtaque(TipoAtaque tipo)
    {
        if (EsCura)
            return tipo == TipoAtaque.RayoSagrado
                ? AccionVFXCombate.RayoSagradoCura
                : AccionVFXCombate.GolpeBaculoCura;
        if (EsEsqueleto)
            return tipo == TipoAtaque.Estocada
                ? AccionVFXCombate.AtaquePesadoEsqueleto
                : AccionVFXCombate.AtaqueBasicoEsqueleto;
        if (tipo == TipoAtaque.GolpeImpulsor) return AccionVFXCombate.GolpeImpulsorDoctor;
        if (tipo == TipoAtaque.Estocada) return AccionVFXCombate.AtaquePesadoDoctor;
        return AccionVFXCombate.AtaqueBasicoDoctor;
    }

    public void ReproducirVFX(AccionVFXCombate accion, Transform objetivo = null)
    {
        if (configuracionVFX == null) return;
        VFXAccionCombate entrada = configuracionVFX.Buscar(accion);
        if (entrada == null || entrada.prefab == null) return;

        Transform punto = entrada.usarObjetivo && objetivo != null ? objetivo : transform;
        Vector3 posicion = punto.TransformPoint(entrada.desplazamiento);
        Quaternion rotacion = punto.rotation * Quaternion.Euler(entrada.rotacionEuler);
        Transform padre = entrada.seguirPuntoDeAparicion ? punto : null;
        GameObject instancia = Instantiate(entrada.prefab, posicion, rotacion, padre);
        if (padre != null)
        {
            instancia.transform.localPosition = entrada.desplazamiento;
            instancia.transform.localRotation = Quaternion.Euler(entrada.rotacionEuler);
        }
        if (entrada.duracionSegundos > 0f) Destroy(instancia, entrada.duracionSegundos);
    }

    private AtaqueCombate ObtenerAtaque(TipoAtaque tipo)
    {
        switch (tipo)
        {
            case TipoAtaque.Estocada:
                return new AtaqueCombate(11, 15, esEsqueleto ? costoAtaquePesadoEsqueleto : 0);
            case TipoAtaque.ChispaUmbria:
                return new AtaqueCombate(15, 19, 6);
            case TipoAtaque.PulsoArcano:
                return new AtaqueCombate(22, 27, 10);
            case TipoAtaque.GolpeBaculo:
                return new AtaqueCombate(5, 8, 0);
            case TipoAtaque.RayoSagrado:
                return new AtaqueCombate(22, 27, 11);
            case TipoAtaque.GolpeImpulsor:
                return new AtaqueCombate(danoImpulsoMinimo, Mathf.Max(danoImpulsoMinimo, danoImpulsoMaximo), costoImpulsoMana);
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
    RayoSagrado,
    GolpeImpulsor
}
