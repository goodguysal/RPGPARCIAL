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
    private enum MenuActivo { Comandos, Ataques, Inventario, ObjetivosMana }

    private PersonajeCombate personaje;
    private PersonajeCombate[] aliados = Array.Empty<PersonajeCombate>();
    private EnemigoCombate enemigo;
    private PersonajeCardUI[] fichasPersonaje;
    private TMP_Text nombreEnemigoTexto;
    private TMP_Text vidaEnemigoTexto;
    private TMP_Text registroTexto;
    private TMP_Text turnoTexto;
    private TMP_Text mensajeFinalTexto;
    private TMP_Text tituloComandosTexto;
    private TMP_Text tituloAtaquesTexto;
    private TMP_Text encabezadoNormalesTexto;
    private TMP_Text encabezadoEspecialesTexto;
    private Image vidaEnemigoRelleno;
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
    private MenuActivo menuActual = MenuActivo.Comandos;
    private int turno = 1;
    private int indiceAliadoActual;
    private int indiceSeleccionado;
    private bool turnoDeIren = true;
    private bool combateTerminado;
    private bool resolviendoAcciones;
    private bool turnoAvanzadoEnResolucion;
    private bool suprimiendoFinalizacion;
    private float inicioResolucionRealtime;
    private Coroutine corutinaResolucion;
    private const int MaxEntradasHistorial = 6;
    private readonly List<string> historialAcciones = new List<string>();
    private readonly HashSet<PersonajeCombate> personajesDefendidos = new HashSet<PersonajeCombate>();
    private bool siguienteAliadoPendiente;

    private void Awake()
    {
        aliados = FindObjectsOfType<PersonajeCombate>()
            .Where(a => a != null && a.isActiveAndEnabled)
            .OrderByDescending(a => a.Iniciativa)
            .ToArray();
        enemigo = FindFirstObjectByType<EnemigoCombate>();
        fichasPersonaje = GetComponentsInChildren<PersonajeCardUI>(true)
            .OrderBy(ficha => ((RectTransform)ficha.transform).anchoredPosition.x)
            .ToArray();

        nombreEnemigoTexto = Obtener<TMP_Text>("EnemyCard/EnemyName");
        vidaEnemigoTexto = Obtener<TMP_Text>("EnemyCard/EnemyHP");
        vidaEnemigoRelleno = Obtener<Image>("EnemyCard/EnemyBar/Fill");
        turnoTexto = Obtener<TMP_Text>("EnemyCard/TurnText");
        registroTexto = Obtener<TMP_Text>("CombatHistory/CombatLog");
        menuComandos = Obtener<Transform>("BattlePanel/ActionPanel/Commands")?.gameObject;
        menuAtaques = Obtener<Transform>("BattlePanel/ActionPanel/Attacks")?.gameObject;
        menuInventario = Obtener<Transform>("BattlePanel/ActionPanel/Inventory")?.gameObject;
        panelFinal = Obtener<Transform>("EndPanel")?.gameObject;
        mensajeFinalTexto = Obtener<TMP_Text>("EndPanel/EndMessage");
        tituloComandosTexto = Obtener<TMP_Text>("BattlePanel/ActionPanel/CommandTitle");
        tituloAtaquesTexto = Obtener<TMP_Text>("BattlePanel/ActionPanel/Attacks/AttackTitle");
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
        if (aliados.Length == 0 || enemigo == null)
        {
            EscribirRegistro(aliados.Length == 0
                ? "No se encontró ningún aliado con PersonajeCombate."
                : "No se encontró el enemigo: revisa el componente EnemigoCombate.");
            if (turnoTexto != null) turnoTexto.text = "COMBATE SIN CONFIGURAR";
            if (mensajeFinalTexto != null) mensajeFinalTexto.text = "Falta un personaje o enemigo en la escena.";
            if (panelFinal != null) panelFinal.SetActive(true);
            ActivarMenu(null);
            return;
        }
        indiceAliadoActual = PrimerAliadoVivo();
        if (indiceAliadoActual < 0)
        {
            EscribirRegistro("No hay aliados en pie para iniciar el combate.");
            TerminarCombate(false);
            return;
        }
        personaje = aliados[indiceAliadoActual];
        VincularFichasAliados();
        ActualizarEstado();
        historialAcciones.Clear();
        EscribirRegistro("El GOLEM bloquea el camino.");
        EscribirRegistro("Iniciativa: " + string.Join(" → ", aliados.Select(a => a.Nombre + " " + a.Iniciativa)) + ".");
        ActualizarIndicadorTurno();
        MostrarComandos();
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
            turnoTexto.text = $"T{turno:00} · TURNO DE {personaje.Nombre.ToUpperInvariant()}";
    }

    private void Update()
    {
        if (resolviendoAcciones && Time.realtimeSinceStartup - inicioResolucionRealtime > Mathf.Max(2f, tiempoMaximoResolucion))
            RecuperarResolucionAtascada();

        if (Keyboard.current == null)
            return;

        if (combateTerminado)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
                ReiniciarCombate();
            return;
        }

        if (!turnoDeIren || resolviendoAcciones)
            return;

        if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
            MoverSeleccion(-1);
        else if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)
            MoverSeleccion(1);
        else if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
            ConfirmarSeleccion();
        else if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame)
            MostrarComandos();
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

        if (personaje != null && personaje.EsCura)
        {
            CambiarTextoBoton(botonTajo, "GOLPE DE BÁCULO", "5–8 daño   ·   0 PM");
            CambiarTextoBoton(botonEstocada, "RAYO SAGRADO", "22–27 daño   ·   10 PM");
            CambiarTextoBoton(botonChispa, "REGENERAR PM", $"Cuesta {personaje.CostoRegenerarMana} PM   ·   recupera {personaje.CantidadRegenerarMana} PM");
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
            CambiarTextoBoton(botonTajo, "TAJO", "7–10 daño   ·   0 PM");
            CambiarTextoBoton(botonEstocada, "ESTOCADA", "11–15 daño   ·   0 PM");
            CambiarTextoBoton(botonChispa, "CHISPA UMBRÍA", "15–19 daño   ·   6 PM");
            CambiarTextoBoton(botonPulso, "PULSO ARCANO", "22–27 daño   ·   10 PM");
            if (botonPulso != null) botonPulso.gameObject.SetActive(true);
            if (encabezadoNormalesTexto != null) encabezadoNormalesTexto.text = "NORMALES   ·   0 PM";
            if (encabezadoEspecialesTexto != null) encabezadoEspecialesTexto.text = "ESPECIALES   ·   GASTAN PM";
            Conectar(botonTajo, () => EjecutarAtaque(TipoAtaque.Tajo, "Tajo"));
            Conectar(botonEstocada, () => EjecutarAtaque(TipoAtaque.Estocada, "Estocada"));
            Conectar(botonChispa, () => EjecutarAtaque(TipoAtaque.ChispaUmbria, "Chispa umbría"));
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

    private void RegenerarMana(PersonajeCombate objetivo)
    {
        if (!PuedeActuar() || personaje == null || !personaje.UsarRegenerarMana(objetivo, out int recuperado)) return;
        IniciarResolucion($"{personaje.Nombre} regenera {recuperado} PM de {objetivo.Nombre}.", false);
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
        ActualizarDisponibilidad();
        if (tituloAtaquesTexto != null)
            tituloAtaquesTexto.text = personaje.EsCura ? "ATAQUES Y HABILIDADES DE CURA" : "ATAQUES DE DOCTOR";
        if (personaje.EsCura)
            ConfigurarSeleccion(botonTajo, botonEstocada, botonChispa, botonVolverAtaques);
        else
            ConfigurarSeleccion(botonTajo, botonEstocada, botonChispa, botonPulso, botonVolverAtaques);
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
        if (menuComandos != null) menuComandos.SetActive(menu == menuComandos);
        if (menuAtaques != null) menuAtaques.SetActive(menu == menuAtaques);
        if (menuInventario != null) menuInventario.SetActive(menu == menuInventario);
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
    }

    private void MoverSeleccion(int direccion)
    {
        if (botonesActivos.Count == 0) return;
        indiceSeleccionado = PrimerHabilitado(indiceSeleccionado + direccion, direccion);
        EventSystem.current?.SetSelectedGameObject(botonesActivos[indiceSeleccionado].gameObject);
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
    }

    private void ConfirmarSeleccion()
    {
        if (botonesActivos.Count == 0 || indiceSeleccionado >= botonesActivos.Count) return;
        Button boton = botonesActivos[indiceSeleccionado];
        if (boton.interactable) boton.onClick.Invoke();
    }

    private void EjecutarAtaque(TipoAtaque tipo, string nombreAtaque)
    {
        if (!PuedeActuar() || enemigo == null || !personaje.UsarAtaque(tipo, out int dano)) return;

        enemigo.RecibirDano(dano);
        ActualizarEstado();
        string accion = $"{personaje.Nombre} usa {nombreAtaque}: {dano} de daño.";
        if (enemigo.Vida <= 0)
        {
            EscribirRegistro(accion + " El GOLEM cae.");
            TerminarCombate(true);
            return;
        }

        IniciarResolucion(accion, false);
    }

    private void UsarVenda()
    {
        if (!PuedeActuar() || !personaje.UsarVenda(out int curacion)) return;
        IniciarResolucion($"{personaje.Nombre} usa Venda amarga y recupera {curacion} PV.", false);
    }

    private void UsarTonico()
    {
        if (!PuedeActuar() || !personaje.UsarTonico(out int recuperacion)) return;
        IniciarResolucion($"{personaje.Nombre} usa Tónico de luciérnaga y recupera {recuperacion} PM.", false);
    }

    private void Defenderse()
    {
        if (!PuedeActuar()) return;
        personajesDefendidos.Add(personaje);
        IniciarResolucion($"{personaje.Nombre} se defiende.", true);
    }

    private void IniciarResolucion(string accion, bool defensa)
    {
        if (!PuedeActuar() || resolviendoAcciones) return;
        resolviendoAcciones = true;
        turnoDeIren = false;
        turnoAvanzadoEnResolucion = false;
        suprimiendoFinalizacion = false;
        inicioResolucionRealtime = Time.realtimeSinceStartup;
        BloquearBotones(true);
        corutinaResolucion = StartCoroutine(ResolverTurnoEnemigo(accion, defensa));
    }

    private IEnumerator ResolverTurnoEnemigo(string accion, bool defensa)
    {
        try
        {
        EscribirRegistro(accion);
        ActualizarEstado();
        if (turnoTexto != null) turnoTexto.text = $"T{turno:00} · ACCIÓN DE {personaje.Nombre.ToUpperInvariant()}";

        // Cada aliado vivo actúa por iniciativa; el enemigo responde una vez al final de la ronda.
        yield return new WaitForSecondsRealtime(pausaAntesDecisionEnemiga);
        if (enemigo == null || enemigo.Vida <= 0)
        {
            SeleccionarSiguienteAliado();
            yield break;
        }

        int siguiente = SiguienteAliadoVivo(indiceAliadoActual + 1);
        if (siguiente >= 0)
        {
            SeleccionarAliado(siguiente);
            siguienteAliadoPendiente = true;
            EscribirRegistro($"Ahora actúa {personaje.Nombre}: elige una acción.");
            yield break;
        }

        AccionEnemigaPendiente pendiente = enemigo.PrepararAccionConProteccion(aliados, personajesDefendidos);
        if (pendiente.objetivo == null)
        {
            if (TodosLosPersonajesDerrotados(aliados))
            {
                EscribirRegistro($"{enemigo.Nombre} no encuentra a nadie en pie.");
                AvanzarRonda();
                TerminarCombate(false);
            }
            else
            {
                EscribirRegistro($"{enemigo.Nombre} no puede actuar. Comienza una nueva ronda.");
                AvanzarRonda();
            }
            yield break;
        }

        if (turnoTexto != null) turnoTexto.text = $"T{turno:00} · {enemigo.Nombre.ToUpperInvariant()} PREPARA ATAQUE";
        EscribirRegistro($"¡{enemigo.Nombre} prepara {pendiente.nombreAtaque} contra {pendiente.objetivo.Nombre}!");
        yield return new WaitForSecondsRealtime(pausaDePreparacion);

        ResultadoAccionEnemiga resultado = enemigo.EjecutarAccion(pendiente);
        ActualizarEstado();
        BloquearBotones(true);
        if (turnoTexto != null) turnoTexto.text = $"T{turno:00} · IMPACTO";
        if (resultado.objetivo != null)
            EscribirRegistro($"{enemigo.Nombre} usa {resultado.nombreAtaque} contra {resultado.objetivo.Nombre}: -{resultado.dano} PV (quedan {resultado.objetivo.Vida}/{resultado.objetivo.VidaMaxima}){(resultado.objetivoDefendido ? "; defensa reduce el daño" : "")}.");
        else
            EscribirRegistro($"{enemigo.Nombre} intenta atacar, pero ya no encuentra un objetivo.");

        if (TodosLosPersonajesDerrotados(aliados))
        {
            yield return new WaitForSecondsRealtime(pausaTrasImpacto);
            TerminarCombate(false);
            yield break;
        }

        AvanzarRonda();
        yield return new WaitForSecondsRealtime(pausaTrasImpacto);
        EscribirRegistro($"Comienza una nueva ronda. Actúa {personaje.Nombre}.");
        }
        finally
        {
            if (!suprimiendoFinalizacion)
                FinalizarResolucion();
        }
    }

    private void SeleccionarSiguienteAliado()
    {
        int siguiente = SiguienteAliadoVivo(indiceAliadoActual + 1);
        if (siguiente >= 0)
        {
            SeleccionarAliado(siguiente);
            siguienteAliadoPendiente = true;
            return;
        }
        AvanzarRonda();
    }

    private void AvanzarRonda()
    {
        if (turnoAvanzadoEnResolucion) return;
        turno++;
        turnoAvanzadoEnResolucion = true;
        siguienteAliadoPendiente = false;
        personajesDefendidos.Clear();
        int primero = PrimerAliadoVivo();
        if (primero >= 0) SeleccionarAliado(primero);
        ActualizarEstado();
    }

    private void FinalizarResolucion()
    {
        if (!turnoAvanzadoEnResolucion && !siguienteAliadoPendiente && !combateTerminado)
            AvanzarRonda();
        corutinaResolucion = null;
        resolviendoAcciones = false;
        if (combateTerminado) return;
        turnoDeIren = true;
        siguienteAliadoPendiente = false;
        DesbloquearTodosLosBotones();
        ActualizarDisponibilidad();
        ActualizarIndicadorTurno();
        MostrarComandos();
    }

    private void RecuperarResolucionAtascada()
    {
        if (!resolviendoAcciones) return;
        suprimiendoFinalizacion = true;
        if (corutinaResolucion != null)
            StopCoroutine(corutinaResolucion);
        corutinaResolucion = null;
        if (!turnoAvanzadoEnResolucion && !siguienteAliadoPendiente) AvanzarRonda();
        resolviendoAcciones = false;
        suprimiendoFinalizacion = false;
        EscribirRegistro("La acción tardó demasiado; se cierra este turno para recuperar el combate.");
        EscribirRegistro($"Se recupera el control. Actúa {personaje.Nombre}.");
        FinalizarResolucion();
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
        if (botonEstocada != null) botonEstocada.interactable = personaje != null && (!personaje.EsCura || personaje.Mana >= 10);
        if (botonChispa != null) botonChispa.interactable = personaje != null && (personaje.EsCura ? personaje.Mana >= personaje.CostoRegenerarMana : personaje.Mana >= 6);
        if (botonPulso != null) botonPulso.interactable = personaje != null && !personaje.EsCura && personaje.Mana >= 10;
        if (botonVenda != null) botonVenda.interactable = personaje != null && personaje.Vendas > 0 && personaje.Vida < personaje.VidaMaxima;
        if (botonTonico != null) botonTonico.interactable = personaje != null && personaje.Tonicos > 0 && personaje.Mana < personaje.ManaMaximo;
        ActualizarDetalle(botonVenda, personaje != null ? $"Recupera 14 PV   ·   QUEDAN {personaje.Vendas}" : string.Empty);
        ActualizarDetalle(botonTonico, personaje != null ? $"Recupera 8 PM   ·   QUEDAN {personaje.Tonicos}" : string.Empty);
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
            if (vidaEnemigoTexto != null) vidaEnemigoTexto.text = $"{enemigo.Vida} / {enemigo.VidaMaxima} PV";
            if (vidaEnemigoRelleno != null)
                vidaEnemigoRelleno.fillAmount = enemigo.VidaMaxima <= 0 ? 0f : (float)enemigo.Vida / enemigo.VidaMaxima;
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
        foreach (PersonajeCombate aliado in aliados)
            if (aliado != null) aliado.Reiniciar();
        if (enemigo != null) enemigo.Reiniciar();
        DesbloquearTodosLosBotones();
        turno = 1;
        combateTerminado = false;
        turnoDeIren = true;
        resolviendoAcciones = false;
        turnoAvanzadoEnResolucion = false;
        siguienteAliadoPendiente = false;
        personajesDefendidos.Clear();
        indiceAliadoActual = PrimerAliadoVivo();
        if (indiceAliadoActual >= 0) SeleccionarAliado(indiceAliadoActual);
        suprimiendoFinalizacion = false;
        if (panelFinal != null) panelFinal.SetActive(false);
        historialAcciones.Clear();
        EscribirRegistro("El GOLEM bloquea el camino.");
        EscribirRegistro("Iniciativa: " + string.Join(" → ", aliados.Select(a => a.Nombre + " " + a.Iniciativa)) + ".");
        ActualizarEstado();
        MostrarComandos();
    }

    private void EscribirRegistro(string mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje)) return;
        string entrada = $"T{turno:00}  {mensaje.Replace("\n", " ")}";
        historialAcciones.Add(entrada);
        while (historialAcciones.Count > MaxEntradasHistorial)
            historialAcciones.RemoveAt(0);
        if (registroTexto != null)
            registroTexto.text = string.Join("\n", historialAcciones);
    }

    private bool PuedeActuar() => !combateTerminado && turnoDeIren && personaje != null;

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
