using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Controla los menús de la escena; la jerarquía Canvas se guarda en Combate.unity.</summary>
public sealed class CombateUIController : MonoBehaviour
{
    [Header("Ritmo de las acciones")]
    [SerializeField, Min(0f)] private float pausaAntesDecisionEnemiga = 0.55f;
    [SerializeField, Min(0f)] private float pausaDePreparacion = 0.9f;
    [SerializeField, Min(0f)] private float pausaTrasImpacto = 0.75f;
    [SerializeField, Min(0.1f)] private float ventanaCancelarAccion = 0.65f;
    [SerializeField, Min(2f)] private float tiempoMaximoResolucion = 8f;
    [SerializeField] private int[] rondasAtaqueDoble = { 3, 5 };
    private enum MenuActivo { Comandos, Ataques, AtaquesNormales, AtaquesMana, Inventario, ObjetivosMana, ObjetivosCuracionDoctor, ObjetivosEsenciaBestia, ObjetivosVenda, ObjetivosAceleradorATB }

    private PersonajeCombate personaje;
    private PersonajeCombate[] aliados = Array.Empty<PersonajeCombate>();
    private EnemigoCombate enemigo;
    private PersonajeCardUI[] fichasPersonaje;
    private TMP_Text nombreEnemigoTexto;
    private TMP_Text vidaEnemigoTexto;
    private TMP_Text registroTexto;
    private TMP_Text turnoTexto;
    private TMP_Text mensajeFinalTexto;
    private TMP_Text tituloFinalTexto;
    private TMP_Text tituloComandosTexto;
    private TMP_Text tituloAtaquesTexto;
    private TMP_Text tituloInventarioTexto;
    private TMP_Text encabezadoNormalesTexto;
    private TMP_Text encabezadoEspecialesTexto;
    private Image vidaEnemigoRelleno;
    private Image atbEnemigoRelleno;
    private Slider atbEnemigoSlider;
    private TMP_Text atbEnemigoTexto;
    private TMP_Text debuffEnemigoTexto;
    private TMP_Text efectoATBEnemigoTexto;
    private TMP_Text avisoDanoVenenoTexto;
    private TMP_Text tituloDescripcionHoverTexto;
    private TMP_Text cuerpoDescripcionHoverTexto;
    private GameObject panelDescripcionHover;
    private string avisoDebuffEnemigo = string.Empty;
    private float avisoDebuffEnemigoHasta;
    private float avisoDanoVenenoHasta;
    private float atbEnemigoObjetivo;
    private Slider vidaEnemigoSlider;
    private float vidaEnemigoObjetivo;
    private bool vidaEnemigoInicializada;
    private GameObject menuComandos;
    private GameObject menuAtaques;
    private GameObject menuInventario;
    private GameObject panelFinal;
    private Button botonAtacar;
    private Button botonInventario;
    private Button botonDefenderse;
    private Button botonTajo;
    private Button botonEstocada;
    private Button botonChispa;
    private Button botonPulso;
    private Button botonVenda;
    private Button botonTonico;
    private Button botonSales;
    private Button botonVeneno;
    private Button botonEsenciaBestia;
    private Button botonAceleradorATB;
    private Button botonVolverAtaques;
    private Button botonVolverInventario;
    private Button botonReiniciar;

    private readonly List<Button> botonesActivos = new List<Button>();
    private readonly List<Button> botonesObjetivoCuracionDoctor = new List<Button>();
    private readonly List<Button> botonesObjetivoVenda = new List<Button>();
    private readonly List<Button> botonesObjetivoAceleradorATB = new List<Button>();
    private MenuActivo menuActual = MenuActivo.Comandos;
    private int turno = 1;
    private int indiceAliadoActual;
    private int indiceSeleccionado;
    private bool turnoDeIren = true;
    private bool combateTerminado;
    private bool resolviendoAcciones;
    private bool accionCancelable;
    private int idResolucion;
    private bool suprimiendoFinalizacion;
    private float inicioResolucionRealtime;
    private Coroutine corutinaResolucion;
    private const int MaxEntradasHistorial = 6;
    private readonly List<string> historialAcciones = new List<string>();
    private readonly HashSet<PersonajeCombate> personajesDefendidos = new HashSet<PersonajeCombate>();
    private bool seleccionandoObjetivoCuracionDoctor;
    private bool seleccionandoObjetivoVenda;
    private bool seleccionandoObjetivoAceleradorATB;

    private void Awake()
    {
        ActualizarReferenciasCombate();
        fichasPersonaje = GetComponentsInChildren<PersonajeCardUI>(true)
            .OrderBy(ficha => ((RectTransform)ficha.transform).anchoredPosition.x)
            .ToArray();

        nombreEnemigoTexto = Obtener<TMP_Text>("EnemyCard/EnemyName");
        vidaEnemigoTexto = Obtener<TMP_Text>("EnemyCard/EnemyHP");
        vidaEnemigoRelleno = Obtener<Image>("EnemyCard/EnemyBar/Fill");
        vidaEnemigoSlider = AsegurarSlider(vidaEnemigoRelleno);
        atbEnemigoRelleno = Obtener<Image>("EnemyCard/ATBBar/Fill");
        atbEnemigoSlider = AsegurarATBSlider(atbEnemigoRelleno);
        atbEnemigoTexto = Obtener<TMP_Text>("EnemyCard/ATBBar/ATBText");
        debuffEnemigoTexto = Obtener<TMP_Text>("EnemyCard/DebuffText");
        efectoATBEnemigoTexto = Obtener<TMP_Text>("EnemyCard/ATBEffectText");
        avisoDanoVenenoTexto = Obtener<TMP_Text>("EnemyCard/PoisonDamageText");
        panelDescripcionHover = Obtener<Transform>("BattlePanel/ActionPanel/ActionHoverDescription")?.gameObject;
        tituloDescripcionHoverTexto = Obtener<TMP_Text>("BattlePanel/ActionPanel/ActionHoverDescription/Title");
        cuerpoDescripcionHoverTexto = Obtener<TMP_Text>("BattlePanel/ActionPanel/ActionHoverDescription/Body");
        if (panelDescripcionHover != null) panelDescripcionHover.SetActive(false);
        turnoTexto = Obtener<TMP_Text>("EnemyCard/TurnText");
        registroTexto = Obtener<TMP_Text>("CombatHistory/CombatLog");
        menuComandos = Obtener<Transform>("BattlePanel/ActionPanel/Commands")?.gameObject;
        menuAtaques = Obtener<Transform>("BattlePanel/ActionPanel/Attacks")?.gameObject;
        menuInventario = Obtener<Transform>("BattlePanel/ActionPanel/Inventory")?.gameObject;
        panelFinal = Obtener<Transform>("EndPanel")?.gameObject;
        mensajeFinalTexto = Obtener<TMP_Text>("EndPanel/EndMessage");
        tituloFinalTexto = Obtener<TMP_Text>("EndPanel/EndTitle");
        if (tituloFinalTexto == null && panelFinal != null)
            tituloFinalTexto = panelFinal.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(texto => texto != mensajeFinalTexto && texto.text == "VICTORIA");
        tituloComandosTexto = Obtener<TMP_Text>("BattlePanel/ActionPanel/CommandTitle");
        tituloAtaquesTexto = Obtener<TMP_Text>("BattlePanel/ActionPanel/Attacks/AttackTitle");
        tituloInventarioTexto = Obtener<TMP_Text>("BattlePanel/ActionPanel/Inventory/InventoryTitle");
        encabezadoNormalesTexto = Obtener<TMP_Text>("BattlePanel/ActionPanel/Attacks/NormalHeader");
        encabezadoEspecialesTexto = Obtener<TMP_Text>("BattlePanel/ActionPanel/Attacks/ManaHeader");

        botonAtacar = Obtener<Button>("BattlePanel/ActionPanel/Commands/BtnAttack");
        botonInventario = Obtener<Button>("BattlePanel/ActionPanel/Commands/BtnInventory");
        botonDefenderse = Obtener<Button>("BattlePanel/ActionPanel/Commands/BtnGuard");
        botonTajo = Obtener<Button>("BattlePanel/ActionPanel/Attacks/BtnTajo");
        botonEstocada = Obtener<Button>("BattlePanel/ActionPanel/Attacks/BtnEstocada");
        botonChispa = Obtener<Button>("BattlePanel/ActionPanel/Attacks/BtnChispa");
        botonPulso = Obtener<Button>("BattlePanel/ActionPanel/Attacks/BtnPulso");
        botonVolverAtaques = Obtener<Button>("BattlePanel/ActionPanel/Attacks/BtnBackAttacks");
        botonVenda = Obtener<Button>("BattlePanel/ActionPanel/Inventory/BtnVenda");
        botonTonico = Obtener<Button>("BattlePanel/ActionPanel/Inventory/BtnTonico");
        botonSales = Obtener<Button>("BattlePanel/ActionPanel/Inventory/BtnSales") ??
            CrearBotonConsumible("BtnSales", new Vector2(16f, -160f), "SALES DE COMBATE", "Daño +25% durante 3 ataques");
        botonVeneno = Obtener<Button>("BattlePanel/ActionPanel/Inventory/BtnVeneno") ??
            CrearBotonConsumible("BtnVeneno", new Vector2(492f, -160f), "VENENO", "-5 PV al final de las próximas 2 rondas · sin coste de PM/ATB");
        botonEsenciaBestia = Obtener<Button>("BattlePanel/ActionPanel/Inventory/BtnEsenciaBestia") ??
            CrearBotonConsumible("BtnEsenciaBestia", new Vector2(16f, -234f), "ESCENCIA DE BESTIA", "El próximo ataque del GOLEM apunta al aliado elegido");
        botonAceleradorATB = Obtener<Button>("BattlePanel/ActionPanel/Inventory/BtnAceleradorATB") ??
            CrearBotonConsumible("BtnAceleradorATB", new Vector2(492f, -234f), "ELIXIR DE CELERIDAD", "Acelera el ATB del aliado elegido durante 2 partidas");
        botonVolverInventario = Obtener<Button>("BattlePanel/ActionPanel/Inventory/BtnBackInventory");
        botonReiniciar = Obtener<Button>("EndPanel/BtnRestart");

        PrepararEventSystem();
        ConectarBotones();
    }

    private void Start()
    {
        ActualizarReferenciasCombate();
        turno = 1;
        combateTerminado = false;
        turnoDeIren = false;
        resolviendoAcciones = false;
        personajesDefendidos.Clear();
        if (panelFinal != null) panelFinal.SetActive(false);
        foreach (PersonajeCombate aliado in aliados)
            if (aliado != null) aliado.Reiniciar();
        if (enemigo != null) enemigo.Reiniciar();

        if (aliados.Length == 0 || enemigo == null)
        {
            EscribirRegistro(aliados.Length == 0
                ? "No se encontró ningún aliado con PersonajeCombate."
                : "No se encontró el enemigo: revisa el componente EnemigoCombate.");
            if (turnoTexto != null) turnoTexto.text = "COMBATE SIN CONFIGURAR";
            if (tituloFinalTexto != null) tituloFinalTexto.text = "COMBATE SIN CONFIGURAR";
            if (mensajeFinalTexto != null) mensajeFinalTexto.text = aliados.Length == 0
                ? "No se encontró ningún aliado activo con PersonajeCombate en esta escena."
                : "No se encontró ningún enemigo activo con EnemigoCombate en esta escena.";
            if (panelFinal != null) panelFinal.SetActive(true);
            ActivarMenu(null);
            return;
        }

        indiceAliadoActual = -1;
        personaje = null;
        VincularFichasAliados();
        ActivarMenu(null);
        ActualizarEstado();
        historialAcciones.Clear();
        EscribirRegistro("El combate comienza. Cada barra ATB determina por sí misma cuándo actúa su combatiente.");
        EscribirRegistro("El ATB avanza automáticamente según la velocidad configurada de cada combatiente.");
        EscribirRegistro("El tónico acelera a quien lo bebe y CURA puede adelantar 20 ATB al aliado al que regenera PM.");
        if (turnoTexto != null) turnoTexto.text = "ATB · CARGANDO TURNOS";
        ActualizarBarrasATB();
    }
    private int PrimerAliadoVivo() => SiguienteAliadoVivo(0);

    private int SiguienteAliadoVivo(int inicio)
    {
        if (aliados == null || aliados.Length == 0) return -1;
        for (int i = Math.Max(0, inicio); i < aliados.Length; i++)
            if (aliados[i] != null && aliados[i].isActiveAndEnabled && aliados[i].Vida > 0) return i;
        return -1;
    }

    private void SeleccionarAliado(int indice)
    {
        if (indice < 0 || indice >= aliados.Length || aliados[indice] == null) return;
        indiceAliadoActual = indice;
        personaje = aliados[indice];
        ActualizarIndicadorTurno();
    }

    private void VincularFichasAliados()
    {
        if (fichasPersonaje == null) return;
        for (int i = 0; i < fichasPersonaje.Length; i++)
            fichasPersonaje[i].Vincular(i < aliados.Length ? aliados[i] : null);
    }

    private void ActualizarIndicadorTurno()
    {
        if (turnoTexto != null && personaje != null)
            turnoTexto.text = $"ATB · TURNO DE {personaje.Nombre.ToUpperInvariant()}";
    }

    private void Update()
    {
        if (vidaEnemigoSlider != null && vidaEnemigoInicializada)
            vidaEnemigoSlider.value = Mathf.MoveTowards(vidaEnemigoSlider.value, vidaEnemigoObjetivo, 90f * Time.unscaledDeltaTime);
        if (atbEnemigoSlider != null)
            atbEnemigoSlider.value = Mathf.MoveTowards(atbEnemigoSlider.value, atbEnemigoObjetivo * 100f, 150f * Time.unscaledDeltaTime);
        if (debuffEnemigoTexto != null && (enemigo == null || enemigo.RondasVenenoRestantes <= 0) && Time.unscaledTime >= avisoDebuffEnemigoHasta)
            debuffEnemigoTexto.gameObject.SetActive(false);
        if (avisoDanoVenenoTexto != null && Time.unscaledTime >= avisoDanoVenenoHasta)
            avisoDanoVenenoTexto.gameObject.SetActive(false);

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (accionCancelable)
            {
                CancelarAccionPendiente();
                return;
            }
            if (!combateTerminado && turnoDeIren && !resolviendoAcciones)
            {
                MostrarComandos();
                return;
            }
        }

        if (resolviendoAcciones && Time.realtimeSinceStartup - inicioResolucionRealtime > Mathf.Max(2f, tiempoMaximoResolucion))
            RecuperarResolucionAtascada();

        if (combateTerminado)
        {
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                ReiniciarCombate();
            return;
        }

        if (!turnoDeIren && !resolviendoAcciones)
        {
            float delta = Time.unscaledDeltaTime;
            foreach (PersonajeCombate aliado in aliados)
                if (aliado != null) aliado.AvanzarATB(delta);
            if (enemigo != null) enemigo.AvanzarATB(delta);
            ActualizarBarrasATB();
            ProcesarBarrasListas();
        }

        if (Keyboard.current == null || !turnoDeIren || resolviendoAcciones) return;
        if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
            MoverSeleccion(-1);
        else if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
            MoverSeleccion(1);
        else if (Keyboard.current.eKey.wasPressedThisFrame)
            ConfirmarSeleccion();
    }

    private void ProcesarBarrasListas()
    {
        if (combateTerminado || turnoDeIren || resolviendoAcciones) return;
        PersonajeCombate aliadoListo = aliados
            .Where(a => a != null && a.isActiveAndEnabled && a.ListoParaActuar)
            .OrderByDescending(a => a.ATB)
            .FirstOrDefault();
        bool enemigoListo = enemigo != null && enemigo.isActiveAndEnabled && enemigo.ListoParaActuar;

        if (enemigoListo && (aliadoListo == null || enemigo.ATB > aliadoListo.ATB))
        {
            ActivarMenu(null);
            StartResolucionEnemigoATB();
            return;
        }

        if (aliadoListo == null) return;
        if (aliadoListo.TurnosParalizado > 0)
        {
            aliadoListo.ConsumirParalisis();
            aliadoListo.ConsumirATB(100f);
            EscribirRegistro($"{aliadoListo.Nombre} está paralizado y no podrá actuar hasta que termine el combate. Su barra ATB vuelve a cero.");
            ActualizarEstado();
        PersonajeCardUI fichaParalizada = fichasPersonaje.FirstOrDefault(f => f != null && f.Personaje == aliadoListo);
        if (fichaParalizada != null) fichaParalizada.MostrarAvisoDebuff("PARÁLISIS · NO ACTÚA HASTA FIN DEL COMBATE", 6f);
            ActualizarBarrasATB();
            resolviendoAcciones = true;
            inicioResolucionRealtime = Time.realtimeSinceStartup;
            corutinaResolucion = StartCoroutine(ResolverAccionParalizada(aliadoListo));
            return;
        }
        indiceAliadoActual = Array.IndexOf(aliados, aliadoListo);
        SeleccionarAliado(indiceAliadoActual);
        turnoDeIren = true;
        ActualizarIndicadorTurno();
        MostrarComandos();
    }

    private IEnumerator ResolverAccionParalizada(PersonajeCombate objetivo)
    {
        try
        {
            if (turnoTexto != null) turnoTexto.text = $"ATB · {objetivo.Nombre.ToUpperInvariant()} PARALIZADO";
            yield return new WaitForSecondsRealtime(pausaTrasImpacto);
        }
        finally
        {
            if (!suprimiendoFinalizacion) FinalizarResolucion();
        }
    }

    private void ActualizarBarrasATB()
    {
        if (fichasPersonaje != null)
            foreach (PersonajeCardUI ficha in fichasPersonaje)
                if (ficha != null && ficha.Personaje != null)
                    ficha.ActualizarATB(ficha.Personaje.PorcentajeATB, ficha.Personaje.ListoParaActuar);

        if (enemigo != null)
        {
            atbEnemigoObjetivo = enemigo.PorcentajeATB;
            if (atbEnemigoTexto != null)
                atbEnemigoTexto.text = enemigo.ListoParaActuar
                    ? "GOLEM · LISTO"
                    : $"GOLEM · ATB {Mathf.RoundToInt(enemigo.PorcentajeATB * 100f)}%";
            if (efectoATBEnemigoTexto != null)
                efectoATBEnemigoTexto.text = enemigo.AvisoAceleracionATB;
        }
    }
    private void PrepararEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
            eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();

        InputSystemUIInputModule modulo = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (modulo == null)
            modulo = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        modulo.AssignDefaultActions();
    }

    private void ConectarBotones()
    {
        Conectar(botonAtacar, AbrirAtaques);
        Conectar(botonInventario, AbrirInventario);
        Conectar(botonDefenderse, Defenderse);
        Conectar(botonTajo, () => EjecutarAtaque(TipoAtaque.Tajo, "Tajo"));
        Conectar(botonEstocada, () => EjecutarAtaque(TipoAtaque.Estocada, "Estocada"));
        Conectar(botonChispa, () => EjecutarAtaque(TipoAtaque.ChispaUmbria, "Chispa umbría"));
        Conectar(botonPulso, () => EjecutarAtaque(TipoAtaque.PulsoArcano, "Pulso arcano"));
        Conectar(botonVenda, UsarVenda);
        Conectar(botonTonico, UsarTonico);
        Conectar(botonSales, UsarSalesDeCombate);
        Conectar(botonVeneno, UsarVenenoConsumible);
        Conectar(botonEsenciaBestia, AbrirObjetivosEsenciaBestia);
        Conectar(botonAceleradorATB, AbrirObjetivosAceleradorATB);
        Conectar(botonVolverAtaques, MostrarComandos);
        Conectar(botonVolverInventario, MostrarComandos);
        Conectar(botonReiniciar, ReiniciarCombate);

        PrepararHover(botonTajo, botonEstocada,
            botonChispa, botonPulso, botonVenda, botonTonico, botonSales, botonVeneno, botonEsenciaBestia, botonAceleradorATB,
            botonVolverAtaques, botonVolverInventario);
    }

    private void PrepararHover(params Button[] botones)
    {
        foreach (Button boton in botones)
        {
            if (boton == null) continue;
            EventTrigger trigger = boton.GetComponent<EventTrigger>();
            if (trigger == null) trigger = boton.gameObject.AddComponent<EventTrigger>();
            if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();

            EventTrigger.Entry entrar = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entrar.callback.AddListener(_ => MostrarDescripcionHover(boton));
            trigger.triggers.Add(entrar);

            EventTrigger.Entry salir = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            salir.callback.AddListener(_ => OcultarDescripcionHover());
            trigger.triggers.Add(salir);
        }
    }

    private void MostrarDescripcionHover(Button boton)
    {
        if (panelDescripcionHover == null || boton == null || !boton.gameObject.activeInHierarchy) return;
        TMP_Text etiqueta = boton.transform.Find("Label")?.GetComponent<TMP_Text>();
        TMP_Text detalle = boton.transform.Find("Detail")?.GetComponent<TMP_Text>();
        string titulo = etiqueta != null && !string.IsNullOrWhiteSpace(etiqueta.text)
            ? etiqueta.text.Trim()
            : boton.name;
        string descripcion = ObtenerDescripcionExacta(boton, detalle != null ? detalle.text : string.Empty);

        if (tituloDescripcionHoverTexto != null) tituloDescripcionHoverTexto.text = titulo;
        if (cuerpoDescripcionHoverTexto != null) cuerpoDescripcionHoverTexto.text = descripcion;
        PosicionarDescripcionDebajoDeOpciones();
        panelDescripcionHover.transform.SetAsLastSibling();
        panelDescripcionHover.SetActive(true);
    }

    private string ObtenerDescripcionExacta(Button boton, string detalle)
    {
        switch (boton.name)
        {
            case "BtnAttack": return "Abre las categorías de ataques. Los normales no gastan PM; las habilidades gastan PM y tienen su propia recuperación ATB.";
            case "BtnInventory": return "Abre los consumibles. Cada objeto indica aquí su efecto, cantidad restante y coste de recuperación ATB.";
            case "BtnGuard": return "Defiende al personaje hasta el siguiente ataque enemigo que lo elija. Reduce ese impacto a la mitad y reproduce Outward Block. No consume ATB.";
            case "BtnVeneno": return $"La dosis suma {personaje?.DuracionVenenoRondas ?? 2} rondas completas de veneno. El GOLEM pierde {personaje?.DanoVenenoPorTurno ?? 5} PV al final de cada ronda. No consume PM ni ATB.";
            case "BtnEsenciaBestia": return "Elegí a quién atraerá el próximo ataque del GOLEM. Usarla sobre otro aliado cuesta 30 de recuperación ATB; sobre quien la usa no cuesta ATB.";
            case "BtnAceleradorATB": return $"Se aplica al aliado elegido y aumenta su velocidad de ATB un {personaje?.PorcentajeAceleracionATBConsumible ?? 50}% durante 2 partidas. Recuperación ATB: 30.";
            case "BtnBackAttacks":
            case "BtnBackInventory": return "Cierra este submenú y vuelve a los comandos del personaje.";
        }

        if (menuActual == MenuActivo.ObjetivosMana && personaje != null && personaje.EsCura)
            return $"CURA gasta {personaje.CostoRegenerarMana} PM para que el aliado elegido recupere hasta {personaje.CantidadRegenerarMana} PM y adelanta 20 ATB a ese aliado. Recuperación de CURA: 35 ATB.";
        if (menuActual == MenuActivo.ObjetivosCuracionDoctor && personaje != null && personaje.EsDoctor)
            return $"DOCTOR gasta {personaje.CostoCuracionDoctor} PM para recuperar hasta {personaje.CantidadCuracionDoctor} PV al aliado elegido. Recuperación de DOCTOR: 55 ATB.";
        if (menuActual == MenuActivo.ObjetivosVenda)
            return "Usa una venda del inventario de quien actúa para recuperar 14 PV del aliado elegido. Recuperación ATB: 30.";
        if (menuActual == MenuActivo.ObjetivosAceleradorATB)
            return $"Aumenta la velocidad de ATB del aliado elegido un {personaje?.PorcentajeAceleracionATBConsumible ?? 50}% durante {personaje?.DuracionAceleracionATBConsumiblePartidas ?? 2} partidas. Recuperación ATB: 30.";

        if (string.IsNullOrWhiteSpace(detalle))
        {
            if (boton == botonTajo || boton == botonEstocada || boton == botonChispa || boton == botonPulso)
                return "Acción disponible para " + (personaje != null ? personaje.Nombre : "este personaje") + ". Revisa sus requisitos de PV, PM y ATB antes de elegir.";
            return "Selecciona esta opción para realizar la acción indicada.";
        }
        return detalle.Trim();
    }

    private void PosicionarDescripcionDebajoDeOpciones()
    {
        RectTransform descripcion = panelDescripcionHover != null ? panelDescripcionHover.transform as RectTransform : null;
        if (descripcion == null) return;
        float bordeInferior = -82f;
        foreach (Button boton in botonesActivos)
        {
            if (boton == null || !boton.gameObject.activeInHierarchy) continue;
            RectTransform rect = boton.transform as RectTransform;
            if (rect == null) continue;
            bordeInferior = Mathf.Min(bordeInferior, rect.anchoredPosition.y - rect.rect.height);
        }
        descripcion.anchoredPosition = new Vector2(16f, bordeInferior - 10f);
    }

    private void OcultarDescripcionHover()
    {
        if (panelDescripcionHover != null) panelDescripcionHover.SetActive(false);
    }

    private void ConfigurarAccionesAtaque()
    {
        RestaurarBotonObjetivo(botonTajo);
        RestaurarBotonObjetivo(botonEstocada);
        RestaurarBotonObjetivo(botonChispa);
        RestaurarBotonObjetivo(botonPulso);

        if (personaje != null && personaje.EsEsqueleto)
        {
            CambiarTextoBoton(botonTajo, "TAJO DE ESPADA", "7–10 daño · 0 PM · ATB 24");
            CambiarTextoBoton(botonEstocada, "CORTE PESADO", $"11–15 daño · {personaje.CostoAtaquePesadoEsqueleto} PM · ATB 64");
            CambiarTextoBoton(botonChispa, "GUARDIA REFLEJA", $"Devuelve el próximo golpe · {personaje.CostoContraataque} PM · sin recuperación ATB adicional");
            CambiarTextoBoton(botonPulso, "", "");
            if (botonPulso != null) botonPulso.gameObject.SetActive(false);
            if (encabezadoNormalesTexto != null) encabezadoNormalesTexto.text = "ATAQUES DE ESPADA";
            if (encabezadoEspecialesTexto != null) encabezadoEspecialesTexto.text = "GASTAN PM   ·   MÁS RECUPERACIÓN";
            Conectar(botonTajo, () => EjecutarAtaque(TipoAtaque.Tajo, "Tajo de espada"));
            Conectar(botonEstocada, () => EjecutarAtaque(TipoAtaque.Estocada, "Corte pesado"));
            Conectar(botonChispa, PrepararContraataque);
        }
        else if (personaje != null && personaje.EsCura)
        {
            CambiarTextoBoton(botonTajo, "GOLPE DE BÁCULO", "5–8 daño · 0 PM · ATB 26");
            CambiarTextoBoton(botonEstocada, "RAYO SAGRADO", "22–27 daño · 11 PM · ATB 70");
            CambiarTextoBoton(botonChispa, "REGENERAR PM", $"Cuesta {personaje.CostoRegenerarMana} PM · +{personaje.CantidadRegenerarMana} PM · +20 ATB · coste ATB 35");
            CambiarTextoBoton(botonPulso, "", "");
            if (botonPulso != null) botonPulso.gameObject.SetActive(false);
            if (encabezadoNormalesTexto != null) encabezadoNormalesTexto.text = "CUERPO A CUERPO";
            if (encabezadoEspecialesTexto != null) encabezadoEspecialesTexto.text = "MAGIA   ·   APOYO";
            Conectar(botonTajo, () => EjecutarAtaque(TipoAtaque.GolpeBaculo, "Golpe de báculo"));
            Conectar(botonEstocada, () => EjecutarAtaque(TipoAtaque.RayoSagrado, "Rayo sagrado"));
            Conectar(botonChispa, AbrirObjetivosMana);
        }
        else
        {
            CambiarTextoBoton(botonTajo, "TAJO", "7–10 daño · 0 PM · ATB 24");
            CambiarTextoBoton(botonEstocada, "ESTOCADA", "11–15 daño · 0 PM · ATB 36");
            CambiarTextoBoton(botonChispa,
                personaje != null && personaje.EsDoctor ? "GOLPE DE IMPULSO" : "CHISPA UMBRÍA",
                personaje != null && personaje.EsDoctor
                    ? $"{personaje.DanoImpulsoMinimo}–{personaje.DanoImpulsoMaximo} daño · {personaje.CostoImpulsoMana} PM · ATB 65 · GOLEM +{personaje.AceleracionATBImpulso:P0} velocidad"
                    : "15–19 daño · 6 PM · ATB 42");
            CambiarTextoBoton(botonPulso,
                personaje != null && personaje.EsDoctor ? "SANACIÓN ALQUÍMICA" : "PULSO ARCANO",
                personaje != null && personaje.EsDoctor
                    ? $"{personaje.CostoCuracionDoctor} PM · recupera hasta {personaje.CantidadCuracionDoctor} PV · ATB 55"
                    : "22–27 daño · 10 PM · ATB 62");
            if (botonPulso != null) botonPulso.gameObject.SetActive(true);
            if (encabezadoNormalesTexto != null) encabezadoNormalesTexto.text = "NORMALES   ·   0 PM";
            if (encabezadoEspecialesTexto != null) encabezadoEspecialesTexto.text = "ESPECIALES   ·   GASTAN PM";
            Conectar(botonTajo, () => EjecutarAtaque(TipoAtaque.Tajo, "Tajo"));
            Conectar(botonEstocada, () => EjecutarAtaque(TipoAtaque.Estocada, "Estocada"));
            if (personaje != null && personaje.EsDoctor)
                Conectar(botonChispa, () => EjecutarAtaque(TipoAtaque.GolpeImpulsor, "Golpe de impulso"));
            else
                Conectar(botonChispa, () => EjecutarAtaque(TipoAtaque.ChispaUmbria, "Chispa umbría"));
            if (personaje != null && personaje.EsDoctor)
                Conectar(botonPulso, AbrirObjetivosCuracionDoctor);
            else
                Conectar(botonPulso, () => EjecutarAtaque(TipoAtaque.PulsoArcano, "Pulso arcano"));
        }

        Conectar(botonVolverAtaques, MostrarComandos);
    }

    private void AbrirObjetivosMana()
    {
        if (!PuedeActuar() || personaje == null || !personaje.EsCura) return;

        menuActual = MenuActivo.ObjetivosMana;
        ActivarMenu(menuAtaques);
        if (tituloAtaquesTexto != null) tituloAtaquesTexto.text = "REGENERAR PM   ·   ELEGIR ALIADO";
        if (encabezadoNormalesTexto != null) encabezadoNormalesTexto.text = "ELIGE A QUIÉN AYUDAR";
        if (encabezadoEspecialesTexto != null) encabezadoEspecialesTexto.text = "NO SE PUEDE ELEGIR AL ENEMIGO";

        Button[] botonesObjetivo = { botonTajo, botonEstocada, botonChispa, botonPulso };
        foreach (Button boton in botonesObjetivo)
            if (boton != null) boton.gameObject.SetActive(true);
        List<Button> seleccionables = new List<Button>();
        int indiceBoton = 0;
        foreach (PersonajeCombate aliado in aliados)
        {
            if (aliado == null || aliado.Vida <= 0 || indiceBoton >= botonesObjetivo.Length) continue;
            Button boton = botonesObjetivo[indiceBoton++];
            boton.gameObject.SetActive(true);
            CambiarTextoBoton(boton, aliado.Nombre.ToUpperInvariant(), $"Recupera {personaje.CantidadRegenerarMana} PM");
            bool puedeRecibir = aliado == personaje || aliado.Mana < aliado.ManaMaximo;
            boton.interactable = puedeRecibir && personaje.Mana >= personaje.CostoRegenerarMana;
            Conectar(boton, () => RegenerarMana(aliado));
            seleccionables.Add(boton);
        }

        for (int i = indiceBoton; i < botonesObjetivo.Length; i++)
            botonesObjetivo[i].gameObject.SetActive(false);

        CambiarTextoBoton(botonVolverAtaques, "VOLVER", "");
        Conectar(botonVolverAtaques, AbrirAtaques);
        seleccionables.Add(botonVolverAtaques);
        ConfigurarSeleccion(seleccionables.ToArray());
    }

    private void AbrirObjetivosCuracionDoctor()
    {
        if (!PuedeActuar() || personaje == null || !personaje.EsDoctor) return;

        menuActual = MenuActivo.ObjetivosCuracionDoctor;
        seleccionandoObjetivoCuracionDoctor = true;
        LimpiarObjetivosCuracionDoctor();
        ActivarMenu(menuAtaques);
        if (tituloAtaquesTexto != null) tituloAtaquesTexto.text = "SANACIÓN ALQUÍMICA   ·   ELEGIR ALIADO";
        if (encabezadoNormalesTexto != null) encabezadoNormalesTexto.text = "ELIGE A QUIÉN CURAR";
        if (encabezadoEspecialesTexto != null) encabezadoEspecialesTexto.text = string.Empty;

        Button[] botonesObjetivo = { botonTajo, botonEstocada, botonChispa, botonPulso };
        List<Button> seleccionables = new List<Button>();
        int indiceBoton = 0;
        foreach (PersonajeCombate aliado in aliados)
        {
            if (aliado == null || aliado.Vida <= 0 || indiceBoton >= botonesObjetivo.Length) continue;
            Button boton = botonesObjetivo[indiceBoton++];
            boton.gameObject.SetActive(true);
            CambiarTextoBoton(boton, aliado.Nombre.ToUpperInvariant(),
                $"PV {aliado.Vida}/{aliado.VidaMaxima}   ·   +{Mathf.Min(personaje.CantidadCuracionDoctor, aliado.VidaMaxima - aliado.Vida)} PV");
            boton.interactable = aliado.Vida < aliado.VidaMaxima && personaje.Mana >= personaje.CostoCuracionDoctor;
            Conectar(boton, () => CurarConDoctor(aliado));
            botonesObjetivoCuracionDoctor.Add(boton);
            seleccionables.Add(boton);
        }

        for (int i = indiceBoton; i < botonesObjetivo.Length; i++)
            botonesObjetivo[i].gameObject.SetActive(false);

        CambiarTextoBoton(botonVolverAtaques, "VOLVER", "");
        Conectar(botonVolverAtaques, AbrirAtaquesMana);
        seleccionables.Add(botonVolverAtaques);
        ConfigurarSeleccion(seleccionables.ToArray());
        ActualizarIndicadorObjetivoCuracionDoctor();
    }

    private void AbrirObjetivosVenda()
    {
        if (!PuedeActuar() || personaje == null || personaje.Vendas <= 0 ||
            !aliados.Any(aliado => aliado != null && aliado.Vida > 0 && aliado.Vida < aliado.VidaMaxima)) return;

        menuActual = MenuActivo.ObjetivosVenda;
        seleccionandoObjetivoVenda = true;
        botonesObjetivoVenda.Clear();
        OcultarIndicadoresObjetivo();
        ActivarMenu(menuAtaques);
        if (tituloAtaquesTexto != null) tituloAtaquesTexto.text = "VENDA · ELEGIR ALIADO";
        if (encabezadoNormalesTexto != null) encabezadoNormalesTexto.text = "ELIGE A QUIÉN CURAR";
        if (encabezadoEspecialesTexto != null) encabezadoEspecialesTexto.text = string.Empty;

        Button[] opciones = { botonTajo, botonEstocada, botonChispa, botonPulso };
        int indice = 0;
        List<Button> seleccionables = new List<Button>();
        foreach (PersonajeCombate aliado in aliados)
        {
            if (aliado == null || aliado.Vida <= 0 || indice >= opciones.Length) continue;
            Button boton = opciones[indice++];
            boton.gameObject.SetActive(true);
            int cura = Mathf.Min(14, aliado.VidaMaxima - aliado.Vida);
            CambiarTextoBoton(boton, aliado.Nombre.ToUpperInvariant(), $"PV {aliado.Vida}/{aliado.VidaMaxima} · +{cura} PV");
            boton.interactable = aliado.Vida < aliado.VidaMaxima;
            Conectar(boton, () => AplicarVenda(aliado));
            botonesObjetivoVenda.Add(boton);
            seleccionables.Add(boton);
        }
        for (int i = indice; i < opciones.Length; i++) opciones[i].gameObject.SetActive(false);
        CambiarTextoBoton(botonVolverAtaques, "VOLVER AL INVENTARIO", string.Empty);
        Conectar(botonVolverAtaques, AbrirInventario);
        seleccionables.Add(botonVolverAtaques);
        ConfigurarSeleccion(seleccionables.ToArray());
    }

    private void AplicarVenda(PersonajeCombate objetivo)
    {
        if (!PuedeActuar() || personaje == null || objetivo == null || objetivo.Vida <= 0) return;
        seleccionandoObjetivoVenda = false;
        OcultarIndicadoresObjetivo();
        IniciarResolucion(() => !personaje.UsarVenda(objetivo, out int curacion) ? string.Empty :
            $"{personaje.Nombre} usa Venda amarga sobre {objetivo.Nombre} y recupera {curacion} PV.", 30f);
    }

    private void AbrirObjetivosAceleradorATB()
    {
        if (!PuedeActuar() || personaje == null || personaje.AceleradoresATB <= 0 ||
            !aliados.Any(aliado => aliado != null && aliado.Vida > 0)) return;

        menuActual = MenuActivo.ObjetivosAceleradorATB;
        seleccionandoObjetivoAceleradorATB = true;
        botonesObjetivoAceleradorATB.Clear();
        OcultarIndicadoresObjetivo();
        ActivarMenu(menuAtaques);
        if (tituloAtaquesTexto != null) tituloAtaquesTexto.text = "ELIXIR DE CELERIDAD · ELEGIR ALIADO";
        if (encabezadoNormalesTexto != null) encabezadoNormalesTexto.text = "AUMENTA SU VELOCIDAD DE ATB";
        if (encabezadoEspecialesTexto != null)
            encabezadoEspecialesTexto.text = $"+{personaje.PorcentajeAceleracionATBConsumible}% DURANTE {personaje.DuracionAceleracionATBConsumiblePartidas} PARTIDAS";

        Button[] opciones = { botonTajo, botonEstocada, botonChispa, botonPulso };
        int indice = 0;
        List<Button> seleccionables = new List<Button>();
        foreach (PersonajeCombate aliado in aliados)
        {
            if (aliado == null || aliado.Vida <= 0 || indice >= opciones.Length) continue;
            Button boton = opciones[indice++];
            boton.gameObject.SetActive(true);
            CambiarTextoBoton(boton, aliado.Nombre.ToUpperInvariant(),
                $"VEL ATB {aliado.VelocidadATB:0.#} → {aliado.VelocidadATB * (1f + personaje.PorcentajeAceleracionATBConsumible / 100f):0.#}");
            boton.interactable = true;
            Conectar(boton, () => AplicarAceleradorATB(aliado));
            botonesObjetivoAceleradorATB.Add(boton);
            seleccionables.Add(boton);
        }
        for (int i = indice; i < opciones.Length; i++) opciones[i].gameObject.SetActive(false);
        CambiarTextoBoton(botonVolverAtaques, "VOLVER AL INVENTARIO", string.Empty);
        Conectar(botonVolverAtaques, AbrirInventario);
        seleccionables.Add(botonVolverAtaques);
        ConfigurarSeleccion(seleccionables.ToArray());
    }

    private void AplicarAceleradorATB(PersonajeCombate objetivo)
    {
        if (!PuedeActuar() || personaje == null || objetivo == null || objetivo.Vida <= 0) return;
        seleccionandoObjetivoAceleradorATB = false;
        OcultarIndicadoresObjetivo();
        IniciarResolucion(() =>
        {
            if (!personaje.UsarAceleradorATB(objetivo)) return string.Empty;
            PersonajeCardUI ficha = fichasPersonaje.FirstOrDefault(f => f != null && f.Personaje == objetivo);
            if (ficha != null) ficha.MostrarAvisoATB($"ATB +{personaje.PorcentajeAceleracionATBConsumible}% · {personaje.DuracionAceleracionATBConsumiblePartidas} PARTIDAS");
            return $"{personaje.Nombre} usa Elixir de celeridad sobre {objetivo.Nombre}: velocidad ATB +{personaje.PorcentajeAceleracionATBConsumible}% durante {personaje.DuracionAceleracionATBConsumiblePartidas} partidas.";
        }, 30f);
    }

    private void LimpiarObjetivosCuracionDoctor()
    {
        botonesObjetivoCuracionDoctor.Clear();
        OcultarIndicadoresObjetivo();
    }

    private void OcultarIndicadoresObjetivo()
    {
        if (fichasPersonaje == null) return;
        foreach (PersonajeCardUI ficha in fichasPersonaje)
            if (ficha != null) ficha.MostrarIndicadorObjetivo(false);
    }

    private void CurarConDoctor(PersonajeCombate objetivo)
    {
        if (!PuedeActuar() || personaje == null || objetivo == null || objetivo.Vida <= 0 || personaje.Mana < personaje.CostoCuracionDoctor) return;
        seleccionandoObjetivoCuracionDoctor = false;
        LimpiarObjetivosCuracionDoctor();
        IniciarResolucion(() => personaje.CurarConMana(objetivo, out int recuperado)
            ? $"{personaje.Nombre} usa Sanación alquímica sobre {objetivo.Nombre}: +{recuperado} PV."
            : string.Empty, 55f);
    }

    private void ActualizarIndicadorObjetivoCuracionDoctor()
    {
        if (fichasPersonaje == null) return;
        OcultarIndicadoresObjetivo();
        if ((!seleccionandoObjetivoCuracionDoctor && !seleccionandoObjetivoVenda && !seleccionandoObjetivoAceleradorATB) ||
            botonesActivos.Count == 0 || indiceSeleccionado < 0 || indiceSeleccionado >= botonesActivos.Count) return;

        List<Button> botonesObjetivo = seleccionandoObjetivoCuracionDoctor
            ? botonesObjetivoCuracionDoctor
            : seleccionandoObjetivoVenda ? botonesObjetivoVenda : botonesObjetivoAceleradorATB;
        int indiceObjetivo = botonesObjetivo.IndexOf(botonesActivos[indiceSeleccionado]);
        if (indiceObjetivo < 0) return;
        PersonajeCombate objetivo = aliados.Where(a => a != null && a.Vida > 0).ElementAtOrDefault(indiceObjetivo);
        if (objetivo == null) return;
        PersonajeCardUI fichaObjetivo = fichasPersonaje.FirstOrDefault(f => f != null && f.Personaje == objetivo);
        if (fichaObjetivo != null) fichaObjetivo.MostrarIndicadorObjetivo(true);
    }

    private void RegenerarMana(PersonajeCombate objetivo)
    {
        if (!PuedeActuar() || personaje == null || objetivo == null || objetivo.Vida <= 0 || personaje.Mana < personaje.CostoRegenerarMana) return;
        IniciarResolucion(() =>
        {
            if (!personaje.UsarRegenerarMana(objetivo, out int recuperadoAhora)) return string.Empty;
            objetivo.AcelerarATB(20f);
            PersonajeCardUI fichaAcelerada = fichasPersonaje.FirstOrDefault(f => f != null && f.Personaje == objetivo);
            if (fichaAcelerada != null) fichaAcelerada.MostrarAvisoATB("+20 ATB · ACELERADO");
            return $"{personaje.Nombre} regenera {recuperadoAhora} PM de {objetivo.Nombre} y acelera su ATB +20.";
        }, 35f);
    }

    private static void CambiarTextoBoton(Button boton, string titulo, string detalle)
    {
        if (boton == null) return;
        TMP_Text label = boton.transform.Find("Label")?.GetComponent<TMP_Text>();
        TMP_Text detail = boton.transform.Find("Detail")?.GetComponent<TMP_Text>();
        if (label != null) label.text = titulo;
        if (detail != null) detail.text = detalle;
    }

    private static void RestaurarBotonObjetivo(Button boton)
    {
        if (boton != null) boton.gameObject.SetActive(true);
    }

    private void Conectar(Button boton, UnityEngine.Events.UnityAction accion)
    {
        if (boton == null) return;
        boton.onClick.RemoveAllListeners();
        boton.onClick.AddListener(accion);
        Navigation navigation = boton.navigation;
        navigation.mode = Navigation.Mode.None;
        boton.navigation = navigation;
    }

    private void MostrarComandos()
    {
        if (combateTerminado || !turnoDeIren || resolviendoAcciones) return;
        if (personaje == null || !personaje.isActiveAndEnabled || personaje.Vida <= 0)
        {
            ActivarMenu(null);
            return;
        }
        menuActual = MenuActivo.Comandos;
        ActivarMenu(menuComandos);
        if (tituloComandosTexto != null)
            tituloComandosTexto.text = $"¿QUÉ HARÁ {personaje.Nombre.ToUpperInvariant()}?";
        ConfigurarSeleccion(botonAtacar, botonInventario, botonDefenderse);
    }

    private void AbrirAtaques()
    {
        if (!PuedeActuar()) return;
        menuActual = MenuActivo.Ataques;
        ActivarMenu(menuAtaques);
        ConfigurarAccionesAtaque();
        if (tituloAtaquesTexto != null) tituloAtaquesTexto.text = "ELEGIR TIPO DE ATAQUE";
        if (encabezadoNormalesTexto != null) encabezadoNormalesTexto.gameObject.SetActive(false);
        if (encabezadoEspecialesTexto != null) encabezadoEspecialesTexto.gameObject.SetActive(false);
        MostrarBotonesAtaque(botonTajo, botonChispa);
        CambiarTextoBoton(botonTajo, "ATAQUES NORMALES", "No consumen PM");
        CambiarTextoBoton(botonChispa, "HABILIDADES DE PM", "Usan maná");
        if (botonTajo != null) botonTajo.interactable = true;
        if (botonChispa != null) botonChispa.interactable = true;
        CambiarTextoBoton(botonVolverAtaques, "VOLVER", "");
        Conectar(botonTajo, AbrirAtaquesNormales);
        Conectar(botonChispa, AbrirAtaquesMana);
        Conectar(botonVolverAtaques, MostrarComandos);
        ConfigurarSeleccion(botonTajo, botonChispa, botonVolverAtaques);
    }

    private void AbrirAtaquesNormales()
    {
        if (!PuedeActuar()) return;
        menuActual = MenuActivo.AtaquesNormales;
        ActivarMenu(menuAtaques);
        ConfigurarAccionesAtaque();
        ActualizarDisponibilidad();
        if (tituloAtaquesTexto != null) tituloAtaquesTexto.text = "ATAQUES NORMALES · 0 PM";
        if (encabezadoNormalesTexto != null)
        {
            encabezadoNormalesTexto.gameObject.SetActive(true);
            encabezadoNormalesTexto.text = "ATAQUES NORMALES";
        }
        if (encabezadoEspecialesTexto != null) encabezadoEspecialesTexto.gameObject.SetActive(false);
        if (personaje.EsCura || personaje.EsEsqueleto) MostrarBotonesAtaque(botonTajo);
        else MostrarBotonesAtaque(botonTajo, botonEstocada);
        CambiarTextoBoton(botonVolverAtaques, "VOLVER A CATEGORÍAS", "");
        Conectar(botonVolverAtaques, AbrirAtaques);
        if (personaje.EsCura || personaje.EsEsqueleto) ConfigurarSeleccion(botonTajo, botonVolverAtaques);
        else ConfigurarSeleccion(botonTajo, botonEstocada, botonVolverAtaques);
    }

    private void AbrirAtaquesMana()
    {
        if (!PuedeActuar()) return;
        menuActual = MenuActivo.AtaquesMana;
        ActivarMenu(menuAtaques);
        ConfigurarAccionesAtaque();
        ActualizarDisponibilidad();
        if (tituloAtaquesTexto != null) tituloAtaquesTexto.text = "HABILIDADES · USAN PM";
        if (encabezadoNormalesTexto != null) encabezadoNormalesTexto.gameObject.SetActive(false);
        if (encabezadoEspecialesTexto != null)
        {
            encabezadoEspecialesTexto.gameObject.SetActive(true);
            encabezadoEspecialesTexto.text = personaje.EsCura ? "MAGIA   ·   APOYO" : personaje.EsEsqueleto ? "ATAQUES PESADOS   ·   USAN PM" : "ESPECIALES   ·   GASTAN PM";
        }
        if (personaje.EsCura) MostrarBotonesAtaque(botonEstocada, botonChispa);
        else if (personaje.EsEsqueleto) MostrarBotonesAtaque(botonEstocada, botonChispa);
            else if (personaje.EsDoctor) MostrarBotonesAtaque(botonChispa, botonPulso);
        else MostrarBotonesAtaque(botonChispa, botonPulso);
        CambiarTextoBoton(botonVolverAtaques, "VOLVER A CATEGORÍAS", "");
        Conectar(botonVolverAtaques, AbrirAtaques);
        if (personaje.EsCura) ConfigurarSeleccion(botonEstocada, botonChispa, botonVolverAtaques);
        else if (personaje.EsEsqueleto) ConfigurarSeleccion(botonEstocada, botonChispa, botonVolverAtaques);
        else if (personaje.EsDoctor) ConfigurarSeleccion(botonChispa, botonPulso, botonVolverAtaques);
        else ConfigurarSeleccion(botonChispa, botonPulso, botonVolverAtaques);
    }

    private void MostrarBotonesAtaque(params Button[] visibles)
    {
        Button[] todos = { botonTajo, botonEstocada, botonChispa, botonPulso };
        foreach (Button boton in todos)
            if (boton != null) boton.gameObject.SetActive(visibles.Contains(boton));
    }

    private void AbrirInventario()
    {
        if (!PuedeActuar()) return;
        menuActual = MenuActivo.Inventario;
        ActivarMenu(menuInventario);
        ActualizarDisponibilidad();
        ConfigurarSeleccion(botonVenda, botonTonico, botonSales, botonVeneno, botonEsenciaBestia, botonAceleradorATB, botonVolverInventario);
    }

    private void AbrirObjetivosEsenciaBestia()
    {
        if (!PuedeActuar() || personaje == null || personaje.EsenciasBestia <= 0 || enemigo == null || enemigo.Vida <= 0) return;
        menuActual = MenuActivo.ObjetivosEsenciaBestia;
        ActivarMenu(menuAtaques);
        if (tituloAtaquesTexto != null) tituloAtaquesTexto.text = "ESCENCIA DE BESTIA · ELEGIR ALIADO";
        if (encabezadoNormalesTexto != null) encabezadoNormalesTexto.text = "EL PRÓXIMO ATAQUE DEL GOLEM IRÁ A ESTE ALIADO";
        if (encabezadoEspecialesTexto != null) encabezadoEspecialesTexto.text = string.Empty;

        Button[] botonesObjetivo = { botonTajo, botonEstocada, botonChispa, botonPulso };
        List<Button> seleccionables = new List<Button>();
        int indice = 0;
        foreach (PersonajeCombate aliado in aliados)
        {
            if (aliado == null || aliado.Vida <= 0 || indice >= botonesObjetivo.Length) continue;
            Button boton = botonesObjetivo[indice++];
            boton.gameObject.SetActive(true);
            string detalle = aliado == personaje
                ? "Próximo ataque del GOLEM a este personaje · sin coste de ATB"
                : "Próximo ataque del GOLEM a este personaje · recuperación ATB 30";
            CambiarTextoBoton(boton, aliado.Nombre.ToUpperInvariant(), detalle);
            boton.interactable = true;
            Conectar(boton, () => UsarEsenciaBestia(aliado));
            seleccionables.Add(boton);
        }
        for (int i = indice; i < botonesObjetivo.Length; i++)
            botonesObjetivo[i].gameObject.SetActive(false);

        CambiarTextoBoton(botonVolverAtaques, "VOLVER AL INVENTARIO", string.Empty);
        Conectar(botonVolverAtaques, AbrirInventario);
        seleccionables.Add(botonVolverAtaques);
        ConfigurarSeleccion(seleccionables.ToArray());
    }

    private void ActivarMenu(GameObject menu)
    {
        OcultarDescripcionHover();
        if (menuActual != MenuActivo.ObjetivosCuracionDoctor && seleccionandoObjetivoCuracionDoctor)
        {
            seleccionandoObjetivoCuracionDoctor = false;
            ActualizarIndicadorObjetivoCuracionDoctor();
        }
        bool limpiarIndicadoresObjetivo = false;
        if (menuActual != MenuActivo.ObjetivosVenda && seleccionandoObjetivoVenda)
        {
            seleccionandoObjetivoVenda = false;
            limpiarIndicadoresObjetivo = true;
        }
        if (menuActual != MenuActivo.ObjetivosAceleradorATB && seleccionandoObjetivoAceleradorATB)
        {
            seleccionandoObjetivoAceleradorATB = false;
            limpiarIndicadoresObjetivo = true;
        }
        if (limpiarIndicadoresObjetivo) OcultarIndicadoresObjetivo();
        bool mostrarComandos = menu != null && menu == menuComandos;
        bool mostrarAtaques = menu != null && menu == menuAtaques;
        bool mostrarInventario = menu != null && menu == menuInventario;
        if (menuComandos != null) menuComandos.SetActive(mostrarComandos);
        if (menuAtaques != null) menuAtaques.SetActive(mostrarAtaques);
        if (menuInventario != null) menuInventario.SetActive(mostrarInventario);
        if (tituloComandosTexto != null) tituloComandosTexto.gameObject.SetActive(mostrarComandos);
        if (tituloAtaquesTexto != null) tituloAtaquesTexto.gameObject.SetActive(mostrarAtaques);
        if (tituloInventarioTexto != null) tituloInventarioTexto.gameObject.SetActive(mostrarInventario);
    }

    private void ConfigurarSeleccion(params Button[] botones)
    {
        botonesActivos.Clear();
        foreach (Button boton in botones)
        {
            if (boton == null) continue;
            botonesActivos.Add(boton);
            PointerSeleccion pointer = boton.GetComponent<PointerSeleccion>();
            if (pointer == null) pointer = boton.gameObject.AddComponent<PointerSeleccion>();
            int indice = botonesActivos.Count - 1;
            pointer.Configurar(() => Seleccionar(indice), boton);
        }

        indiceSeleccionado = PrimerHabilitado(0, 1);
        if (botonesActivos.Count > 0)
            EventSystem.current?.SetSelectedGameObject(botonesActivos[indiceSeleccionado].gameObject);
        ActualizarIndicadorObjetivoCuracionDoctor();
    }

    private void MoverSeleccion(int direccion)
    {
        if (botonesActivos.Count == 0) return;
        indiceSeleccionado = PrimerHabilitado(indiceSeleccionado + direccion, direccion);
        EventSystem.current?.SetSelectedGameObject(botonesActivos[indiceSeleccionado].gameObject);
        ActualizarIndicadorObjetivoCuracionDoctor();
    }

    private int PrimerHabilitado(int inicio, int direccion)
    {
        if (botonesActivos.Count == 0) return 0;
        int indice = (inicio + botonesActivos.Count) % botonesActivos.Count;
        for (int i = 0; i < botonesActivos.Count; i++)
        {
            if (botonesActivos[indice].interactable) return indice;
            indice = (indice + direccion + botonesActivos.Count) % botonesActivos.Count;
        }
        return Mathf.Clamp(indiceSeleccionado, 0, botonesActivos.Count - 1);
    }

    private void Seleccionar(int indice)
    {
        if (indice < 0 || indice >= botonesActivos.Count || !botonesActivos[indice].interactable) return;
        indiceSeleccionado = indice;
        EventSystem.current?.SetSelectedGameObject(botonesActivos[indice].gameObject);
        ActualizarIndicadorObjetivoCuracionDoctor();
    }

    private void ConfirmarSeleccion()
    {
        if (botonesActivos.Count == 0 || indiceSeleccionado >= botonesActivos.Count) return;
        Button boton = botonesActivos[indiceSeleccionado];
        if (boton.interactable) boton.onClick.Invoke();
    }

    private void EjecutarAtaque(TipoAtaque tipo, string nombreAtaque)
    {
        if (!PuedeActuar() || enemigo == null || enemigo.Vida <= 0 || !personaje.PuedeUsarAtaque(tipo)) return;
        IniciarResolucion(() =>
        {
            if (!personaje.UsarAtaque(tipo, out int dano, enemigo.transform)) return string.Empty;
            enemigo.RecibirDano(dano);
            string accion = $"{personaje.Nombre} usa {nombreAtaque}: {dano} de daño.";
            if (tipo == TipoAtaque.GolpeImpulsor)
            {
                bool aceleraEsteLlenado = enemigo.ATB < 100f;
                enemigo.AcelerarProximaRecuperacionATB(personaje.AceleracionATBImpulso);
                accion += aceleraEsteLlenado
                    ? $" El próximo llenado ATB del GOLEM va {personaje.AceleracionATBImpulso:P0} más rápido."
                    : $" El llenado ATB posterior del GOLEM irá {personaje.AceleracionATBImpulso:P0} más rápido.";
            }
            if (enemigo.Vida <= 0) accion += " El GOLEM cae.";
            return accion;
        }, ObtenerCostoRecuperacion(tipo));
    }

    private void UsarSalesDeCombate()
    {
        if (!PuedeActuar() || personaje == null || personaje.Sales <= 0) return;
        IniciarResolucion(() => !personaje.UsarSalesDeCombate() ? string.Empty : $"{personaje.Nombre} usa Sales de combate: daño +{personaje.PorcentajeBuffSales:P0} durante sus próximos {personaje.DuracionSalesAtaques} ataques.", 32f);
    }

    private void UsarVenenoConsumible()
    {
        if (!PuedeActuar() || personaje == null || personaje.Venenos <= 0 || enemigo == null || enemigo.Vida <= 0) return;
        ActivarMenu(null);
        IniciarResolucion(() => !personaje.UsarVeneno(enemigo) ? string.Empty :
            $"{personaje.Nombre} usa Veneno: añade {personaje.DuracionVenenoRondas} rondas completas; el GOLEM pierde {enemigo.DanoVeneno} PV al final de cada ronda. Quedan {enemigo.RondasVenenoRestantes} rondas. No consume PM ni ATB.", 0f, false);
    }

    private void UsarEsenciaBestia(PersonajeCombate objetivo)
    {
        if (!PuedeActuar() || personaje == null || objetivo == null || objetivo.Vida <= 0 || enemigo == null || enemigo.Vida <= 0) return;
        bool sobreSiMismo = objetivo == personaje;
        ActivarMenu(null);
        IniciarResolucion(() =>
        {
            if (!personaje.UsarEsenciaBestia(objetivo.transform)) return string.Empty;
            enemigo.ForzarProximoObjetivo(objetivo);
            return $"{personaje.Nombre} usa Escencia de bestia sobre {objetivo.Nombre}: el próximo ataque del GOLEM lo tendrá como objetivo" +
                (sobreSiMismo ? ". No consume ATB." : ". Recuperación ATB: 30.");
        }, sobreSiMismo ? 0f : 30f, !sobreSiMismo);
    }
    private void UsarVenda()
    {
        AbrirObjetivosVenda();
    }

    private void UsarTonico()
    {
        if (!PuedeActuar() || personaje.Tonicos <= 0 || personaje.Mana >= personaje.ManaMaximo) return;
        IniciarResolucion(() =>
        {
            if (!personaje.UsarTonico(out int recuperacion)) return string.Empty;
            personaje.AcelerarATB(12f);
            PersonajeCardUI fichaAcelerada = fichasPersonaje.FirstOrDefault(f => f != null && f.Personaje == personaje);
            if (fichaAcelerada != null) fichaAcelerada.MostrarAvisoATB("+12 ATB · ACELERADO");
            return $"{personaje.Nombre} usa Tónico de luciérnaga y recupera {recuperacion} PM; adelanta 12 ATB.";
        }, 28f);
    }

    private void Defenderse()
    {
        if (!PuedeActuar()) return;
        IniciarResolucion(() =>
        {
            personajesDefendidos.Add(personaje);
            personaje.ReproducirDefensa();
            return $"{personaje.Nombre} se defiende.";
        }, 0f);
    }

    private void PrepararContraataque()
    {
        if (!PuedeActuar() || personaje == null || personaje.Mana < personaje.CostoContraataque) return;
        IniciarResolucion(() => !personaje.PrepararContraataque() ? string.Empty : $"{personaje.Nombre} prepara Guardia refleja y consume {personaje.CostoContraataque} PM.", 0f);
    }

    private void IniciarResolucion(Func<string> ejecutar, float costoRecuperacion = 30f, bool consumirATB = true)
    {
        if (!PuedeActuar() || resolviendoAcciones || personaje == null) return;
        OcultarDescripcionHover();
        resolviendoAcciones = true;
        accionCancelable = true;
        int id = ++idResolucion;
        inicioResolucionRealtime = Time.realtimeSinceStartup;
        BloquearBotones(true);
        corutinaResolucion = StartCoroutine(ResolverAccionJugador(ejecutar, costoRecuperacion, consumirATB, id));
    }

    private IEnumerator ResolverAccionJugador(Func<string> ejecutar, float costoRecuperacion, bool consumirATB, int id)
    {
        yield return new WaitForSecondsRealtime(ventanaCancelarAccion);
        if (id != idResolucion || !accionCancelable) yield break;
        accionCancelable = false;
        turnoDeIren = false;
        try
        {
            string accion = ejecutar != null ? ejecutar() : string.Empty;
            if (string.IsNullOrWhiteSpace(accion)) yield break;
            if (consumirATB) personaje.ConsumirATB(costoRecuperacion);
            ActualizarBarrasATB();
            EscribirRegistro(accion);
            ActualizarEstado();
            if (turnoTexto != null) turnoTexto.text = $"ATB · ACCIÓN DE {personaje.Nombre.ToUpperInvariant()}";
            yield return new WaitForSecondsRealtime(pausaTrasImpacto);

            if (enemigo != null && enemigo.Vida <= 0)
            {
                TerminarCombate(true);
                yield break;
            }
            if (TodosLosPersonajesDerrotados(aliados))
            {
                TerminarCombate(false);
                yield break;
            }
        }
        finally
        {
            if (!suprimiendoFinalizacion && id == idResolucion) FinalizarResolucion();
        }
    }

    private void CancelarAccionPendiente()
    {
        if (!accionCancelable) return;
        accionCancelable = false;
        ++idResolucion;
        corutinaResolucion = null;
        resolviendoAcciones = false;
        turnoDeIren = true;
        DesbloquearTodosLosBotones();
        MostrarComandos();
    }

    private void StartResolucionEnemigoATB()
    {
        if (enemigo == null || enemigo.Vida <= 0 || resolviendoAcciones) return;
        resolviendoAcciones = true;
        turnoDeIren = false;
        inicioResolucionRealtime = Time.realtimeSinceStartup;
        BloquearBotones(true);
        corutinaResolucion = StartCoroutine(ResolverTurnoEnemigoATB());
    }

    private IEnumerator ResolverTurnoEnemigoATB()
    {
        try
        {
            int cantidadAtaques = EsRondaDeAtaqueDoble() ? 2 : 1;
            float costoATBTotalEnemigo = 0f;
            EscribirRegistro(cantidadAtaques == 2
                ? $"Ronda {turno}: ¡combo de {cantidadAtaques} ataques del GOLEM!"
                : $"Ronda {turno}: {enemigo.Nombre} prepara su acción.");
            for (int numeroAtaque = 1; numeroAtaque <= cantidadAtaques; numeroAtaque++)
            {
                if (enemigo == null || enemigo.Vida <= 0)
                {
                    TerminarCombate(true);
                    yield break;
                }
                if (TodosLosPersonajesDerrotados(aliados))
                {
                    TerminarCombate(false);
                    yield break;
                }

                yield return new WaitForSecondsRealtime(pausaAntesDecisionEnemiga);
                AccionEnemigaPendiente pendiente = enemigo.PrepararAccionConProteccion(aliados, personajesDefendidos);
                if (pendiente.objetivo == null)
                {
                    EscribirRegistro($"{enemigo.Nombre} no encuentra a nadie en pie.");
                    if (TodosLosPersonajesDerrotados(aliados)) TerminarCombate(false);
                    yield break;
                }

                if (turnoTexto != null)
                    turnoTexto.text = $"ATB · RONDA {turno:00} · ATAQUE {numeroAtaque}/{cantidadAtaques}";
                EscribirRegistro($"¡{enemigo.Nombre} prepara ataque {numeroAtaque}/{cantidadAtaques}: {pendiente.nombreAtaque} contra {pendiente.objetivo.Nombre}{(pendiente.costoMana > 0 ? $" ({pendiente.costoMana} PM)" : string.Empty)}!");
                yield return new WaitForSecondsRealtime(pausaDePreparacion);

                float costoATBEnemigo = pendiente.costoMana > 0 ? 44f : 32f;
                costoATBTotalEnemigo += costoATBEnemigo;
                EscribirRegistro($"Recuperación del GOLEM: {costoATBEnemigo:0} ATB tras esta acción.");
                ResultadoAccionEnemiga resultado = enemigo.EjecutarAccion(pendiente);
                ActualizarEstado();
                ActualizarBarrasATB();
                if (turnoTexto != null)
                    turnoTexto.text = $"ATB · IMPACTO {numeroAtaque}/{cantidadAtaques}";
                if (resultado.contraataque)
                    EscribirRegistro($"Ataque {numeroAtaque}/{cantidadAtaques}: {resultado.objetivo.Nombre} bloquea con su escudo y devuelve {resultado.danoDevuelto} de daño a {enemigo.Nombre}.");
                else if (resultado.objetivo != null)
                    EscribirRegistro($"Ataque {numeroAtaque}/{cantidadAtaques}: {enemigo.Nombre} usa {resultado.nombreAtaque} contra {resultado.objetivo.Nombre}: -{resultado.dano} PV (quedan {resultado.objetivo.Vida}/{resultado.objetivo.VidaMaxima}){(resultado.objetivoDefendido ? "; defensa reduce el daño" : "")}.");
                else
                    EscribirRegistro($"Ataque {numeroAtaque}/{cantidadAtaques}: {enemigo.Nombre} no encuentra un objetivo.");
                if (resultado.paralisisAplicada && resultado.objetivo != null)
                    EscribirRegistro($"{resultado.objetivo.Nombre} queda paralizado: perderá su próxima acción ATB.");

                if (TodosLosPersonajesDerrotados(aliados))
                {
                    yield return new WaitForSecondsRealtime(pausaTrasImpacto);
                    TerminarCombate(false);
                    yield break;
                }
                if (enemigo.Vida <= 0)
                {
                    yield return new WaitForSecondsRealtime(pausaTrasImpacto);
                    TerminarCombate(true);
                    yield break;
                }
                if (numeroAtaque < cantidadAtaques)
                    yield return new WaitForSecondsRealtime(pausaTrasImpacto);
            }

            enemigo.ConsumirATB(costoATBTotalEnemigo);
            ActualizarBarrasATB();
            EscribirRegistro($"Recuperación total ATB del GOLEM: {costoATBTotalEnemigo:0}.");
            personajesDefendidos.Clear();
            yield return new WaitForSecondsRealtime(pausaTrasImpacto * 0.5f);

            int danoVeneno = enemigo.ProcesarVenenoAlFinalDeRonda();
            if (danoVeneno > 0)
            {
                EscribirRegistro($"Fin de ronda {turno}: VENENO hace {danoVeneno} de daño a {enemigo.Nombre} ({enemigo.Vida}/{enemigo.VidaMaxima} PV)." +
                    (enemigo.RondasVenenoRestantes > 0 ? $" Quedan {enemigo.RondasVenenoRestantes} rondas completas." : " El efecto de veneno termina."));
                MostrarAvisoDanoVeneno(danoVeneno);
                MostrarAvisoDebuffEnemigo(enemigo.RondasVenenoRestantes > 0
                    ? $"VENENO · -{enemigo.DanoVeneno} PV · {enemigo.RondasVenenoRestantes} rondas restantes"
                    : "VENENO · efecto terminado", 5f);
                ActualizarEstado();
                if (enemigo.Vida <= 0)
                {
                    TerminarCombate(true);
                    yield break;
                }
            }

            turno++;
            yield return new WaitForSecondsRealtime(pausaTrasImpacto);
            EscribirRegistro($"ATB: continúa el siguiente turno. ({enemigo.Nombre}, velocidad {enemigo.VelocidadATB}).");
        }
        finally
        {
            if (!suprimiendoFinalizacion) FinalizarResolucion();
        }
    }

    private float ObtenerCostoRecuperacion(TipoAtaque tipo)
    {
        switch (tipo)
        {
            case TipoAtaque.Tajo: return 24f;
            case TipoAtaque.Estocada:
                return personaje != null && personaje.EsEsqueleto ? 64f : personaje != null && personaje.EsCura ? 58f : 36f;
            case TipoAtaque.ChispaUmbria:
                return personaje != null && personaje.EsEsqueleto ? 48f : 42f;
            case TipoAtaque.PulsoArcano: return personaje != null && personaje.EsDoctor ? 55f : 62f;
            case TipoAtaque.GolpeBaculo: return 26f;
            case TipoAtaque.RayoSagrado: return 70f;
            case TipoAtaque.GolpeImpulsor: return 65f;
            default: return 30f;
        }
    }

    private bool EsRondaDeAtaqueDoble()
    {
        return rondasAtaqueDoble != null && rondasAtaqueDoble.Contains(turno);
    }

    private void FinalizarResolucion()
    {
        corutinaResolucion = null;
        resolviendoAcciones = false;
        if (combateTerminado) return;
        turnoDeIren = false;
        DesbloquearTodosLosBotones();
        ActualizarDisponibilidad();
        ActualizarEstado();
        ActualizarBarrasATB();
        ProcesarBarrasListas();
    }
    private void RecuperarResolucionAtascada()
    {
        if (!resolviendoAcciones) return;
        accionCancelable = false;
        ++idResolucion;
        suprimiendoFinalizacion = true;
        if (corutinaResolucion != null) StopCoroutine(corutinaResolucion);
        corutinaResolucion = null;
        resolviendoAcciones = false;
        suprimiendoFinalizacion = false;
        EscribirRegistro("La acción tardó demasiado; se recupera el control del combate.");
        if (enemigo != null && enemigo.Vida <= 0) TerminarCombate(true);
        else if (TodosLosPersonajesDerrotados(aliados)) TerminarCombate(false);
        else FinalizarResolucion();
    }
    private void BloquearBotones(bool bloquear)
    {
        foreach (Button boton in botonesActivos)
            if (boton != null) boton.interactable = !bloquear;
    }

    private void DesbloquearTodosLosBotones()
    {
        Button[] botones =
        {
            botonAtacar, botonInventario, botonDefenderse,
            botonTajo, botonEstocada, botonChispa, botonPulso, botonVolverAtaques,
            botonVenda, botonTonico, botonSales, botonVeneno, botonEsenciaBestia, botonAceleradorATB, botonVolverInventario, botonReiniciar
        };
        foreach (Button boton in botones)
            if (boton != null) boton.interactable = true;
    }

    private void ActualizarDisponibilidad()
    {
        if (botonTajo != null) botonTajo.interactable = personaje != null;
        if (botonEstocada != null) botonEstocada.interactable = personaje != null && (personaje.EsEsqueleto ? personaje.Mana >= personaje.CostoAtaquePesadoEsqueleto : !personaje.EsCura || personaje.Mana >= 11);
        if (botonChispa != null) botonChispa.interactable = personaje != null && (personaje.EsEsqueleto
            ? personaje.Mana >= personaje.CostoContraataque
            : personaje.EsCura ? personaje.Mana >= personaje.CostoRegenerarMana
            : personaje.EsDoctor ? personaje.Mana >= personaje.CostoImpulsoMana : personaje.Mana >= 6);
        if (botonPulso != null) botonPulso.interactable = personaje != null &&
            (personaje.EsDoctor ? personaje.Mana >= personaje.CostoCuracionDoctor : !personaje.EsCura && personaje.Mana >= 10);
        if (botonVenda != null) botonVenda.interactable = personaje != null && personaje.Vendas > 0 && aliados.Any(a => a != null && a.Vida > 0 && a.Vida < a.VidaMaxima);
        if (botonTonico != null) botonTonico.interactable = personaje != null && personaje.Tonicos > 0 && personaje.Mana < personaje.ManaMaximo;
        if (botonSales != null) botonSales.interactable = personaje != null && personaje.Sales > 0;
        if (botonVeneno != null) botonVeneno.interactable = personaje != null && personaje.Venenos > 0 && enemigo != null && enemigo.Vida > 0;
        if (botonEsenciaBestia != null) botonEsenciaBestia.interactable = personaje != null && personaje.EsenciasBestia > 0 && enemigo != null && enemigo.Vida > 0;
        if (botonAceleradorATB != null) botonAceleradorATB.interactable = personaje != null && personaje.AceleradoresATB > 0 && aliados.Any(a => a != null && a.Vida > 0);
        ActualizarTituloConsumible(botonVenda, "VENDA", personaje != null ? personaje.Vendas : 0);
        ActualizarTituloConsumible(botonTonico, "TÓNICO", personaje != null ? personaje.Tonicos : 0);
        ActualizarTituloConsumible(botonSales, "SALES DE COMBATE", personaje != null ? personaje.Sales : 0);
        ActualizarTituloConsumible(botonVeneno, "VENENO", personaje != null ? personaje.Venenos : 0);
        ActualizarTituloConsumible(botonEsenciaBestia, "ESCENCIA DE BESTIA", personaje != null ? personaje.EsenciasBestia : 0);
        ActualizarTituloConsumible(botonAceleradorATB, "ELIXIR DE CELERIDAD", personaje != null ? personaje.AceleradoresATB : 0);
        ActualizarDetalle(botonVenda, "Recupera 14 PV · ATB 30");
        ActualizarDetalle(botonTonico, "Recupera 8 PM · ATB 28 · +12 ATB");
        ActualizarDetalle(botonSales, personaje != null ? $"Daño +{personaje.PorcentajeBuffSales:P0} durante {personaje.DuracionSalesAtaques} ataques · ATB 32" : string.Empty);
        ActualizarDetalle(botonVeneno, personaje != null ? $"Añade {personaje.DuracionVenenoRondas} rondas de -{personaje.DanoVenenoPorTurno} PV al final de cada ronda · 0 PM · 0 ATB" : string.Empty);
        ActualizarDetalle(botonEsenciaBestia, "El próximo ataque del GOLEM apunta al aliado elegido · en uno mismo: 0 ATB; en otro: ATB 30");
        ActualizarDetalle(botonAceleradorATB, personaje != null
            ? $"Aumenta la velocidad ATB +{personaje.PorcentajeAceleracionATBConsumible}% durante {personaje.DuracionAceleracionATBConsumiblePartidas} partidas · ATB 30"
            : string.Empty);
        if (personaje != null && personaje.EsDoctor)
            ActualizarDetalle(botonPulso, $"{personaje.CostoCuracionDoctor} PM · recupera hasta {personaje.CantidadCuracionDoctor} PV · ATB 55");
    }

    private static void ActualizarDetalle(Button boton, string texto)
    {
        TMP_Text detail = boton != null ? boton.transform.Find("Detail")?.GetComponent<TMP_Text>() : null;
        if (detail != null) detail.text = texto;
    }

    private static void ActualizarTituloConsumible(Button boton, string nombre, int cantidad)
    {
        TMP_Text label = boton != null ? boton.transform.Find("Label")?.GetComponent<TMP_Text>() : null;
        if (label != null) label.text = $"{nombre} ({Mathf.Max(0, cantidad)})";
    }

    private Button CrearBotonConsumible(string nombre, Vector2 posicion, string titulo, string detalle)
    {
        if (botonTonico == null || botonTonico.transform.parent == null) return null;
        GameObject objeto = Instantiate(botonTonico.gameObject, botonTonico.transform.parent, false);
        objeto.name = nombre;
        RectTransform rect = objeto.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = posicion;
        rect.sizeDelta = new Vector2(455f, 68f);
        TMP_Text textoTitulo = objeto.transform.Find("Label")?.GetComponent<TMP_Text>();
        TMP_Text textoDetalle = objeto.transform.Find("Detail")?.GetComponent<TMP_Text>();
        if (textoTitulo != null) textoTitulo.text = titulo;
        if (textoDetalle != null) textoDetalle.text = detalle;
        return objeto.GetComponent<Button>();
    }

    private void ActualizarEstado()
    {
        if (enemigo != null)
        {
            if (nombreEnemigoTexto != null) nombreEnemigoTexto.text = enemigo.Nombre.ToUpperInvariant();
            if (vidaEnemigoTexto != null) vidaEnemigoTexto.text = $"PV {enemigo.Vida}/{enemigo.VidaMaxima} · PM {enemigo.Mana}/{enemigo.ManaMaximo}";
            if (vidaEnemigoSlider != null)
            {
                vidaEnemigoSlider.minValue = 0f;
                vidaEnemigoSlider.maxValue = Mathf.Max(1, enemigo.VidaMaxima);
                vidaEnemigoObjetivo = Mathf.Clamp(enemigo.Vida, 0, enemigo.VidaMaxima);
                if (!vidaEnemigoInicializada)
                {
                    vidaEnemigoSlider.SetValueWithoutNotify(vidaEnemigoObjetivo);
                    vidaEnemigoInicializada = true;
                }
            }
            if (debuffEnemigoTexto != null)
            {
                string estadoPersistente = enemigo.RondasVenenoRestantes > 0
                    ? $"VENENO · -{enemigo.DanoVeneno} PV por ronda · {enemigo.RondasVenenoRestantes} rondas restantes"
                    : Time.unscaledTime < avisoDebuffEnemigoHasta ? avisoDebuffEnemigo : string.Empty;
                debuffEnemigoTexto.text = estadoPersistente;
                debuffEnemigoTexto.gameObject.SetActive(!string.IsNullOrEmpty(estadoPersistente));
            }
        }
        if (fichasPersonaje != null)
        {
            foreach (PersonajeCardUI ficha in fichasPersonaje)
                if (ficha != null) ficha.Actualizar();
        }
        ActualizarDisponibilidad();
    }

    private void MostrarAvisoDebuffEnemigo(string texto, float duracion = 3f)
    {
        avisoDebuffEnemigo = texto;
        avisoDebuffEnemigoHasta = Time.unscaledTime + Mathf.Max(0.1f, duracion);
        if (debuffEnemigoTexto != null && (enemigo == null || enemigo.RondasVenenoRestantes <= 0))
        {
            debuffEnemigoTexto.text = texto;
            debuffEnemigoTexto.gameObject.SetActive(true);
        }
    }

    private void MostrarAvisoDanoVeneno(int dano)
    {
        avisoDanoVenenoHasta = Time.unscaledTime + 4f;
        if (avisoDanoVenenoTexto == null) return;
        avisoDanoVenenoTexto.text = $"¡VENENO! GOLEM pierde {dano} PV";
        avisoDanoVenenoTexto.gameObject.SetActive(true);
    }

    private void TerminarCombate(bool victoria)
    {
        if (combateTerminado) return;
        accionCancelable = false;
        ++idResolucion;
        combateTerminado = true;
        foreach (PersonajeCombate aliado in aliados)
            if (aliado != null) aliado.ConsumirPartidaAceleracionATB();
        turnoDeIren = false;
        resolviendoAcciones = false;
        corutinaResolucion = null;
        ActivarMenu(null);
        if (turnoTexto != null) turnoTexto.text = victoria ? "VICTORIA" : "DERROTA";
        if (tituloFinalTexto != null) tituloFinalTexto.text = victoria ? "VICTORIA" : "DERROTA";
        string mensajeVictoria = personaje != null
            ? $"El GOLEM cae. {personaje.Nombre} conserva {personaje.Vida} PV y {personaje.Mana} PM."
            : "El GOLEM cae. Victoria.";
        string mensajeDerrota = (personaje != null ? personaje.Nombre : "El grupo") + " ha caído. La senda vuelve al silencio.";
        if (mensajeFinalTexto != null)
            mensajeFinalTexto.text = victoria ? mensajeVictoria : mensajeDerrota;
        EscribirRegistro(victoria ? "Fin del combate: victoria." : "Fin del combate: derrota.");
        if (panelFinal != null) panelFinal.SetActive(true);
        ActualizarEstado();
        ConfigurarSeleccion(botonReiniciar);
    }

    private void ReiniciarCombate()
    {
        accionCancelable = false;
        ++idResolucion;
        suprimiendoFinalizacion = true;
        if (corutinaResolucion != null) StopCoroutine(corutinaResolucion);
        corutinaResolucion = null;
        ActualizarReferenciasCombate();
        foreach (PersonajeCombate aliado in aliados)
            if (aliado != null) aliado.Reiniciar();
        if (enemigo != null) enemigo.Reiniciar();
        DesbloquearTodosLosBotones();
        turno = 1;
        combateTerminado = false;
        turnoDeIren = false;
        resolviendoAcciones = false;
        personajesDefendidos.Clear();
        indiceAliadoActual = -1;
        personaje = null;
        suprimiendoFinalizacion = false;
        if (aliados.Length == 0 || enemigo == null)
        {
            MostrarErrorConfiguracion(aliados.Length == 0
                ? "No se encontró ningún aliado activo con PersonajeCombate en esta escena."
                : "No se encontró ningún enemigo activo con EnemigoCombate en esta escena.");
            return;
        }
        if (panelFinal != null) panelFinal.SetActive(false);
        ActivarMenu(null);
        VincularFichasAliados();
        historialAcciones.Clear();
        EscribirRegistro("Combate reiniciado. Las barras ATB vuelven a cero.");
        ActualizarEstado();
        ActualizarBarrasATB();
        if (turnoTexto != null) turnoTexto.text = "ATB · CARGANDO TURNOS";
    }
    private void ActualizarReferenciasCombate()
    {
        aliados = FindObjectsOfType<PersonajeCombate>(true)
            .Where(a => a != null && a.isActiveAndEnabled && a.gameObject.scene == gameObject.scene)
            .OrderBy(a => a.EsDoctor ? 0 : a.EsCura ? 1 : a.EsEsqueleto ? 2 : 3)
            .ThenBy(a => a.Nombre)
            .ToArray();
        enemigo = FindObjectsOfType<EnemigoCombate>(true)
            .FirstOrDefault(e => e != null && e.isActiveAndEnabled && e.gameObject.scene == gameObject.scene);
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
        if (relleno == null || relleno.transform.parent == null) return null;
        Slider slider = relleno.transform.parent.GetComponent<Slider>();
        if (slider == null) slider = relleno.transform.parent.gameObject.AddComponent<Slider>();
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

    private void MostrarErrorConfiguracion(string mensaje)
    {
        combateTerminado = true;
        turnoDeIren = false;
        resolviendoAcciones = false;
        personaje = null;
        ActivarMenu(null);
        if (turnoTexto != null) turnoTexto.text = "COMBATE SIN CONFIGURAR";
        if (tituloFinalTexto != null) tituloFinalTexto.text = "COMBATE SIN CONFIGURAR";
        if (mensajeFinalTexto != null) mensajeFinalTexto.text = mensaje;
        if (panelFinal != null) panelFinal.SetActive(true);
        EscribirRegistro(mensaje);
        ConfigurarSeleccion(botonReiniciar);
    }

    private void EscribirRegistro(string mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje)) return;
        string entrada = $"TURNO {turno:00} · {mensaje.Replace("\n", " ")}";
        historialAcciones.Add(entrada);
        while (historialAcciones.Count > MaxEntradasHistorial)
            historialAcciones.RemoveAt(0);
        if (registroTexto != null)
            registroTexto.text = string.Join("\n", historialAcciones);
    }

    private bool PuedeActuar() => !combateTerminado && turnoDeIren && personaje != null && personaje.ListoParaActuar;

    private static bool TodosLosPersonajesDerrotados(PersonajeCombate[] personajes)
    {
        if (personajes == null || personajes.Length == 0) return true;
        foreach (PersonajeCombate integrante in personajes)
            if (integrante != null && integrante.Vida > 0) return false;
        return true;
    }

    private T Obtener<T>(string ruta) where T : Component
    {
        Transform elemento = transform.Find(ruta);
        return elemento != null ? elemento.GetComponent<T>() : null;
    }
}
