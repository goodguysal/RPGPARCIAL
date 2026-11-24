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
    [SerializeField, Min(2f)] private float tiempoMaximoResolucion = 8f;
    [SerializeField] private int[] rondasAtaqueDoble = { 3, 5 };
    private enum MenuActivo { Comandos, Ataques, AtaquesNormales, AtaquesMana, Inventario, ObjetivosMana, ObjetivosCuracionDoctor }

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
    private Button botonVolverAtaques;
    private Button botonVolverInventario;
    private Button botonReiniciar;

    private readonly List<Button> botonesActivos = new List<Button>();
    private readonly List<Button> botonesObjetivoCuracionDoctor = new List<Button>();
    private MenuActivo menuActual = MenuActivo.Comandos;
    private int turno = 1;
    private int indiceAliadoActual;
    private int indiceSeleccionado;
    private bool turnoDeIren = true;
    private bool combateTerminado;
    private bool resolviendoAcciones;
    private bool suprimiendoFinalizacion;
    private float inicioResolucionRealtime;
    private Coroutine corutinaResolucion;
    private const int MaxEntradasHistorial = 6;
    private readonly List<string> historialAcciones = new List<string>();
    private readonly HashSet<PersonajeCombate> personajesDefendidos = new HashSet<PersonajeCombate>();
    private bool seleccionandoObjetivoCuracionDoctor;

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

        if (resolviendoAcciones && Time.realtimeSinceStartup - inicioResolucionRealtime > Mathf.Max(2f, tiempoMaximoResolucion))
            RecuperarResolucionAtascada();

        if (combateTerminado)
        {
            if (Keyboard.current != null &&
                (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
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
        if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
            MoverSeleccion(-1);
        else if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
            MoverSeleccion(1);
        else if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
            ConfirmarSeleccion();
        else if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame)
            MostrarComandos();
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
        indiceAliadoActual = Array.IndexOf(aliados, aliadoListo);
        SeleccionarAliado(indiceAliadoActual);
        turnoDeIren = true;
        ActualizarIndicadorTurno();
        MostrarComandos();
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
                    ? "GOLEM · ATB LISTO"
                    : $"GOLEM · ATB {Mathf.RoundToInt(enemigo.PorcentajeATB * 100f)}%";
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
        Conectar(botonVolverAtaques, MostrarComandos);
        Conectar(botonVolverInventario, MostrarComandos);
        Conectar(botonReiniciar, ReiniciarCombate);
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
            CambiarTextoBoton(botonChispa, "GUARDIA REFLEJA", $"Devuelve el próximo golpe · {personaje.CostoContraataque} PM · ATB 48");
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
            CambiarTextoBoton(botonEstocada, "RAYO SAGRADO", "22–27 daño · 10 PM · ATB 58");
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
            CambiarTextoBoton(botonChispa, "CHISPA UMBRÍA", "15–19 daño · 6 PM · ATB 42");
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
        if (encabezadoEspecialesTexto != null) encabezadoEspecialesTexto.text = "FLECHAS / W S · E / ENTER / ESPACIO · CLICK";

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

    private void LimpiarObjetivosCuracionDoctor()
    {
        botonesObjetivoCuracionDoctor.Clear();
        if (fichasPersonaje == null) return;
        foreach (PersonajeCardUI ficha in fichasPersonaje)
            if (ficha != null) ficha.MostrarIndicadorObjetivo(false);
    }

    private void CurarConDoctor(PersonajeCombate objetivo)
    {
        if (!PuedeActuar() || personaje == null || !personaje.CurarConMana(objetivo, out int recuperado)) return;
        seleccionandoObjetivoCuracionDoctor = false;
        LimpiarObjetivosCuracionDoctor();
        IniciarResolucion($"{personaje.Nombre} usa Sanación alquímica sobre {objetivo.Nombre}: +{recuperado} PV.", false, 55f);
    }

    private void ActualizarIndicadorObjetivoCuracionDoctor()
    {
        if (fichasPersonaje == null) return;
        foreach (PersonajeCardUI ficha in fichasPersonaje)
            if (ficha != null) ficha.MostrarIndicadorObjetivo(false);

        if (!seleccionandoObjetivoCuracionDoctor || botonesActivos.Count == 0 ||
            indiceSeleccionado < 0 || indiceSeleccionado >= botonesActivos.Count) return;

        int indiceObjetivo = botonesObjetivoCuracionDoctor.IndexOf(botonesActivos[indiceSeleccionado]);
        if (indiceObjetivo < 0 || indiceObjetivo >= aliados.Length) return;
        PersonajeCombate objetivo = aliados.Where(a => a != null && a.Vida > 0).ElementAtOrDefault(indiceObjetivo);
        if (objetivo == null) return;
        PersonajeCardUI fichaObjetivo = fichasPersonaje.FirstOrDefault(f => f != null && f.Personaje == objetivo);
        if (fichaObjetivo != null) fichaObjetivo.MostrarIndicadorObjetivo(true);
    }

    private void RegenerarMana(PersonajeCombate objetivo)
    {
        if (!PuedeActuar() || personaje == null || !personaje.UsarRegenerarMana(objetivo, out int recuperado)) return;
        IniciarResolucion($"{personaje.Nombre} regenera {recuperado} PM de {objetivo.Nombre} y acelera su ATB +20.", false, 35f);
        objetivo.AcelerarATB(20f);
        ActualizarBarrasATB();
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
        else MostrarBotonesAtaque(botonChispa, botonPulso);
        CambiarTextoBoton(botonVolverAtaques, "VOLVER A CATEGORÍAS", "");
        Conectar(botonVolverAtaques, AbrirAtaques);
        if (personaje.EsCura) ConfigurarSeleccion(botonEstocada, botonChispa, botonVolverAtaques);
        else if (personaje.EsEsqueleto) ConfigurarSeleccion(botonEstocada, botonChispa, botonVolverAtaques);
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
        ConfigurarSeleccion(botonVenda, botonTonico, botonVolverInventario);
    }

    private void ActivarMenu(GameObject menu)
    {
        if (menuActual != MenuActivo.ObjetivosCuracionDoctor && seleccionandoObjetivoCuracionDoctor)
        {
            seleccionandoObjetivoCuracionDoctor = false;
            ActualizarIndicadorObjetivoCuracionDoctor();
        }
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
        if (!PuedeActuar() || enemigo == null || enemigo.Vida <= 0) return;
        if (!personaje.UsarAtaque(tipo, out int dano)) return;

        enemigo.RecibirDano(dano);
        ActualizarEstado();
        string accion = $"{personaje.Nombre} usa {nombreAtaque}: {dano} de daño.";
        if (enemigo.Vida <= 0) accion += " El GOLEM cae.";
        IniciarResolucion(accion, false, ObtenerCostoRecuperacion(tipo));
    }
    private void UsarVenda()
    {
        if (!PuedeActuar() || !personaje.UsarVenda(out int curacion)) return;
        IniciarResolucion($"{personaje.Nombre} usa Venda amarga y recupera {curacion} PV.", false, 30f);
    }

    private void UsarTonico()
    {
        if (!PuedeActuar() || !personaje.UsarTonico(out int recuperacion)) return;
        IniciarResolucion($"{personaje.Nombre} usa Tónico de luciérnaga y recupera {recuperacion} PM; adelanta 12 ATB.", false, 28f);
        personaje.AcelerarATB(12f);
        ActualizarBarrasATB();
    }

    private void Defenderse()
    {
        if (!PuedeActuar()) return;
        personajesDefendidos.Add(personaje);
        personaje.ReproducirDefensa();
        IniciarResolucion($"{personaje.Nombre} se defiende.", true, 20f);
    }

    private void PrepararContraataque()
    {
        if (!PuedeActuar() || personaje == null || !personaje.PrepararContraataque()) return;
        IniciarResolucion($"{personaje.Nombre} prepara Guardia refleja y consume {personaje.CostoContraataque} PM.", false, 48f);
    }

    private void IniciarResolucion(string accion, bool defensa, float costoRecuperacion = 30f)
    {
        if (!PuedeActuar() || resolviendoAcciones || personaje == null) return;
        personaje.ConsumirATB(costoRecuperacion);
        ActualizarBarrasATB();
        resolviendoAcciones = true;
        turnoDeIren = false;
        inicioResolucionRealtime = Time.realtimeSinceStartup;
        BloquearBotones(true);
        corutinaResolucion = StartCoroutine(ResolverAccionJugador(accion));
    }

    private IEnumerator ResolverAccionJugador(string accion)
    {
        try
        {
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
            if (!suprimiendoFinalizacion) FinalizarResolucion();
        }
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
            EscribirRegistro(cantidadAtaques == 2
                ? $"Turno enemigo {turno}: ¡combo de {cantidadAtaques} ataques!"
                : $"Turno enemigo {turno}: {enemigo.Nombre} prepara su acción.");
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
                    turnoTexto.text = $"ATB · TURNO ENEMIGO {turno:00} · {numeroAtaque}/{cantidadAtaques}";
                EscribirRegistro($"¡{enemigo.Nombre} prepara ataque {numeroAtaque}/{cantidadAtaques}: {pendiente.nombreAtaque} contra {pendiente.objetivo.Nombre}{(pendiente.costoMana > 0 ? $" ({pendiente.costoMana} PM)" : string.Empty)}!");
                yield return new WaitForSecondsRealtime(pausaDePreparacion);

                float costoATBEnemigo = pendiente.costoMana > 0 ? 44f : 32f;
                EscribirRegistro($"Recuperación del GOLEM: {costoATBEnemigo:0} ATB tras esta acción.");
                ResultadoAccionEnemiga resultado = enemigo.EjecutarAccion(pendiente);
                enemigo.ConsumirATB(costoATBEnemigo);
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

            personajesDefendidos.Clear();
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
            case TipoAtaque.RayoSagrado: return 58f;
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
            botonVenda, botonTonico, botonVolverInventario, botonReiniciar
        };
        foreach (Button boton in botones)
            if (boton != null) boton.interactable = true;
    }

    private void ActualizarDisponibilidad()
    {
        if (botonTajo != null) botonTajo.interactable = personaje != null;
        if (botonEstocada != null) botonEstocada.interactable = personaje != null && (personaje.EsEsqueleto ? personaje.Mana >= personaje.CostoAtaquePesadoEsqueleto : !personaje.EsCura || personaje.Mana >= 10);
        if (botonChispa != null) botonChispa.interactable = personaje != null && (personaje.EsEsqueleto
            ? personaje.Mana >= personaje.CostoContraataque
            : personaje.EsCura ? personaje.Mana >= personaje.CostoRegenerarMana : personaje.Mana >= 6);
        if (botonPulso != null) botonPulso.interactable = personaje != null &&
            (personaje.EsDoctor ? personaje.Mana >= personaje.CostoCuracionDoctor : !personaje.EsCura && personaje.Mana >= 10);
        if (botonVenda != null) botonVenda.interactable = personaje != null && personaje.Vendas > 0 && personaje.Vida < personaje.VidaMaxima;
        if (botonTonico != null) botonTonico.interactable = personaje != null && personaje.Tonicos > 0 && personaje.Mana < personaje.ManaMaximo;
        ActualizarDetalle(botonVenda, personaje != null ? $"Recupera 14 PV · QUEDAN {personaje.Vendas} · ATB 30" : string.Empty);
        ActualizarDetalle(botonTonico, personaje != null ? $"Recupera 8 PM · QUEDAN {personaje.Tonicos} · ATB 28 · +12 ATB" : string.Empty);
        if (personaje != null && personaje.EsDoctor)
            ActualizarDetalle(botonPulso, $"{personaje.CostoCuracionDoctor} PM · recupera hasta {personaje.CantidadCuracionDoctor} PV · ATB 55");
    }

    private static void ActualizarDetalle(Button boton, string texto)
    {
        TMP_Text detail = boton != null ? boton.transform.Find("Detail")?.GetComponent<TMP_Text>() : null;
        if (detail != null) detail.text = texto;
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
        }
        if (fichasPersonaje != null)
        {
            foreach (PersonajeCardUI ficha in fichasPersonaje)
                if (ficha != null) ficha.Actualizar();
        }
        ActualizarDisponibilidad();
    }

    private void TerminarCombate(bool victoria)
    {
        combateTerminado = true;
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
        ConfigurarSeleccion(botonReiniciar);
    }

    private void ReiniciarCombate()
    {
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
