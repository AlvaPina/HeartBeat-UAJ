using System;
using UnityEngine;

public enum EstadoJugador
{
    Normal,
    Corriendo,
    Andando,
    Oculto,
    Fatigado
}

public enum TipoItem
{
    Pildora,
    Caja,
    Reloj,
    Llave
}

public enum TipoEscondite
{
    Armario,
    Caja
}

[Serializable]
public class EventoInicioSesion : EventoBase
{
    public EventoInicioSesion(int nivel) : base(TipoEvento.SessionStart, nivel) { }
}

[Serializable]
public class EventoFinSesion : EventoBase
{
    // Segundos desde que arrancó la sesión/juego.
    public float duracionSesion;

    public EventoFinSesion(int nivel, float duracionSesion) : base(TipoEvento.SessionEnd, nivel)
    {
        this.duracionSesion = duracionSesion;
    }
}

[Serializable]
public class EventoInicioNivel : EventoBase
{
    public EventoInicioNivel(int nivel) : base(TipoEvento.LevelStart, nivel) { }
}

[Serializable]
public class EventoNivelCompletado : EventoBase
{
    // Segundos hasta completar el nivel.
    public float tiempoCompletado;

    public EventoNivelCompletado(int nivel, float tiempoCompletado) : base(TipoEvento.LevelComplete, nivel)
    {
        this.tiempoCompletado = tiempoCompletado;
    }
}

[Serializable]
public class EventoNivelFallido : EventoBase
{
    // Permite distinguir un intento fallido de una muerte concreta.
    public float tiempoHastaFallo;
    public string motivo;
    public Vector3 posicion;
    public EstadoJugador estadoJugador;

    public EventoNivelFallido(
        int nivel,
        float tiempoHastaFallo,
        string motivo,
        Vector3 posicion,
        EstadoJugador estadoJugador
    ) : base(TipoEvento.LevelFail, nivel)
    {
        this.tiempoHastaFallo = tiempoHastaFallo;
        this.motivo = motivo ?? string.Empty;
        this.posicion = posicion;
        this.estadoJugador = estadoJugador;
    }
}

[Serializable]
public class EventoEstadoJugador : EventoBase
{
    public Vector3 posicion;
    public float velocidad;
    public EstadoJugador estadoJugador;
    public bool cercaEnemigo;
    public bool cercaArmario;

    // Campos extra para no depender solo de booleanos.
    public string idEnemigoMasCercano;
    public float distanciaEnemigo;
    public string idEsconditeCercano;
    public float distanciaArmario;
    public bool tienePildora;
    public bool tieneCaja;
    public bool tieneReloj;

    public EventoEstadoJugador(
        int nivel,
        Vector3 posicion,
        float velocidad,
        EstadoJugador estadoJugador,
        bool cercaEnemigo,
        bool cercaArmario
    ) : this(
        nivel,
        posicion,
        velocidad,
        estadoJugador,
        cercaEnemigo,
        cercaArmario,
        string.Empty,
        -1f,
        string.Empty,
        -1f,
        false,
        false,
        false
    ) { }

    public EventoEstadoJugador(
        int nivel,
        Vector3 posicion,
        float velocidad,
        EstadoJugador estadoJugador,
        bool cercaEnemigo,
        bool cercaArmario,
        string idEnemigoMasCercano,
        float distanciaEnemigo,
        string idEsconditeCercano,
        float distanciaArmario,
        bool tienePildora,
        bool tieneCaja,
        bool tieneReloj
    ) : base(TipoEvento.PlayerState, nivel)
    {
        this.posicion = posicion;
        this.velocidad = velocidad;
        this.estadoJugador = estadoJugador;
        this.cercaEnemigo = cercaEnemigo;
        this.cercaArmario = cercaArmario;
        this.idEnemigoMasCercano = idEnemigoMasCercano ?? string.Empty;
        this.distanciaEnemigo = distanciaEnemigo;
        this.idEsconditeCercano = idEsconditeCercano ?? string.Empty;
        this.distanciaArmario = distanciaArmario;
        this.tienePildora = tienePildora;
        this.tieneCaja = tieneCaja;
        this.tieneReloj = tieneReloj;
    }
}

[Serializable]
public class EventoJugadorMuere : EventoBase
{
    public Vector3 posicion;
    public EstadoJugador estadoJugador;

    // Datos clave para hipótesis 2, 3 y 4.
    public bool cercaEnemigo;
    public bool cercaArmario;
    public string idEnemigo;
    public string idEsconditeCercano;
    public float distanciaEnemigo;
    public float distanciaArmario;
    public bool teniaPildora;
    public bool teniaCaja;
    public bool teniaReloj;
    public string causaMuerte;

    public EventoJugadorMuere(
        int nivel,
        Vector3 posicion,
        EstadoJugador estadoJugador
    ) : this(
        nivel,
        posicion,
        estadoJugador,
        false,
        false,
        string.Empty,
        string.Empty,
        -1f,
        -1f,
        false,
        false,
        false,
        string.Empty
    ) { }

    public EventoJugadorMuere(
        int nivel,
        Vector3 posicion,
        EstadoJugador estadoJugador,
        bool cercaEnemigo,
        bool cercaArmario,
        string idEnemigo,
        string idEsconditeCercano,
        float distanciaEnemigo,
        float distanciaArmario,
        bool teniaPildora,
        bool teniaCaja,
        bool teniaReloj,
        string causaMuerte
    ) : base(TipoEvento.PlayerDeath, nivel)
    {
        this.posicion = posicion;
        this.estadoJugador = estadoJugador;
        this.cercaEnemigo = cercaEnemigo;
        this.cercaArmario = cercaArmario;
        this.idEnemigo = idEnemigo ?? string.Empty;
        this.idEsconditeCercano = idEsconditeCercano ?? string.Empty;
        this.distanciaEnemigo = distanciaEnemigo;
        this.distanciaArmario = distanciaArmario;
        this.teniaPildora = teniaPildora;
        this.teniaCaja = teniaCaja;
        this.teniaReloj = teniaReloj;
        this.causaMuerte = causaMuerte ?? string.Empty;
    }
}

[Serializable]
public class EventoJugadorDetectado : EventoBase
{
    public string idEnemigo;
    public EstadoJugador estadoJugador;
    public Vector3 posicionJugador;
    public Vector3 posicionEnemigo;

    // La distancia queda guardada para no tener que recalcularla fuera de Unity.
    public float distanciaEnemigo;
    public bool cercaArmario;
    public string idEsconditeCercano;
    public float distanciaArmario;

    public EventoJugadorDetectado(
        int nivel,
        string idEnemigo,
        EstadoJugador estadoJugador,
        Vector3 posicionJugador,
        Vector3 posicionEnemigo
    ) : this(
        nivel,
        idEnemigo,
        estadoJugador,
        posicionJugador,
        posicionEnemigo,
        false,
        string.Empty,
        -1f
    ) { }

    public EventoJugadorDetectado(
        int nivel,
        string idEnemigo,
        EstadoJugador estadoJugador,
        Vector3 posicionJugador,
        Vector3 posicionEnemigo,
        bool cercaArmario,
        string idEsconditeCercano,
        float distanciaArmario
    ) : base(TipoEvento.PlayerSpotted, nivel)
    {
        this.idEnemigo = idEnemigo ?? string.Empty;
        this.estadoJugador = estadoJugador;
        this.posicionJugador = posicionJugador;
        this.posicionEnemigo = posicionEnemigo;
        this.distanciaEnemigo = Vector3.Distance(posicionJugador, posicionEnemigo);
        this.cercaArmario = cercaArmario;
        this.idEsconditeCercano = idEsconditeCercano ?? string.Empty;
        this.distanciaArmario = distanciaArmario;
    }
}

[Serializable]
public class EventoIntentoLatido : EventoBase
{
    public bool exito;

    // Se mantiene el nombre original para no romper el analizador ni los logs previos.
    public float tamañoZonaVerde;

    // Nuevos campos para la hipótesis 1.
    public float distanciaEnemigo;
    public string idEnemigoMasCercano;
    public Vector3 posicionJugador;
    public Vector3 posicionEnemigo;
    public EstadoJugador estadoJugador;
    public bool cercaEnemigo;
    public int fallosConsecutivos;
    public float tiempoDesdeUltimoIntento;
    public float tiempoDesdeUltimoPlayerSpotted;

    public EventoIntentoLatido(
        int nivel,
        bool exito,
        float tamañoZonaVerde
    ) : this(
        nivel,
        exito,
        tamañoZonaVerde,
        -1f,
        string.Empty,
        Vector3.zero,
        Vector3.zero,
        EstadoJugador.Normal,
        false,
        0,
        -1f,
        -1f
    ) { }

    public EventoIntentoLatido(
        int nivel,
        bool exito,
        float tamañoZonaVerde,
        float distanciaEnemigo,
        string idEnemigoMasCercano,
        Vector3 posicionJugador,
        Vector3 posicionEnemigo,
        EstadoJugador estadoJugador,
        bool cercaEnemigo,
        int fallosConsecutivos,
        float tiempoDesdeUltimoIntento,
        float tiempoDesdeUltimoPlayerSpotted
    ) : base(TipoEvento.HeartbeatAttempt, nivel)
    {
        this.exito = exito;
        this.tamañoZonaVerde = tamañoZonaVerde;
        this.distanciaEnemigo = distanciaEnemigo;
        this.idEnemigoMasCercano = idEnemigoMasCercano ?? string.Empty;
        this.posicionJugador = posicionJugador;
        this.posicionEnemigo = posicionEnemigo;
        this.estadoJugador = estadoJugador;
        this.cercaEnemigo = cercaEnemigo;
        this.fallosConsecutivos = fallosConsecutivos;
        this.tiempoDesdeUltimoIntento = tiempoDesdeUltimoIntento;
        this.tiempoDesdeUltimoPlayerSpotted = tiempoDesdeUltimoPlayerSpotted;
    }
}

[Serializable]
public class EventoFatigaActivada : EventoBase
{
    public EstadoJugador estadoJugador;

    // Campos clave para hipótesis 3.
    public Vector3 posicion;
    public bool cercaEnemigo;
    public string idEnemigoMasCercano;
    public float distanciaEnemigo;
    public int fallosConsecutivos;

    public EventoFatigaActivada(
        int nivel,
        EstadoJugador estadoJugador
    ) : this(nivel, estadoJugador, Vector3.zero, false, string.Empty, -1f, 3) { }

    public EventoFatigaActivada(
        int nivel,
        EstadoJugador estadoJugador,
        Vector3 posicion,
        bool cercaEnemigo,
        string idEnemigoMasCercano,
        float distanciaEnemigo,
        int fallosConsecutivos
    ) : base(TipoEvento.FatigueTriggered, nivel)
    {
        this.estadoJugador = estadoJugador;
        this.posicion = posicion;
        this.cercaEnemigo = cercaEnemigo;
        this.idEnemigoMasCercano = idEnemigoMasCercano ?? string.Empty;
        this.distanciaEnemigo = distanciaEnemigo;
        this.fallosConsecutivos = fallosConsecutivos;
    }
}

[Serializable]
public class EventoFatigaFinalizada : EventoBase
{
    public Vector3 posicion;
    public float duracionFatiga;
    public bool sobrevivio;
    public EstadoJugador estadoJugadorFinal;

    public EventoFatigaFinalizada(
        int nivel,
        Vector3 posicion,
        float duracionFatiga,
        bool sobrevivio,
        EstadoJugador estadoJugadorFinal
    ) : base(TipoEvento.FatigueEnded, nivel)
    {
        this.posicion = posicion;
        this.duracionFatiga = duracionFatiga;
        this.sobrevivio = sobrevivio;
        this.estadoJugadorFinal = estadoJugadorFinal;
    }
}

[Serializable]
public class EventoEsconditeRegistrado : EventoBase
{
    // Evento opcional: se lanza al cargar un nivel para conocer también armarios no usados.
    public string idEscondite;
    public TipoEscondite tipoEscondite;
    public Vector3 posicion;

    public EventoEsconditeRegistrado(
        int nivel,
        string idEscondite,
        TipoEscondite tipoEscondite,
        Vector3 posicion
    ) : base(TipoEvento.HideoutRegistered, nivel)
    {
        this.idEscondite = idEscondite ?? string.Empty;
        this.tipoEscondite = tipoEscondite;
        this.posicion = posicion;
    }
}

[Serializable]
public class EventoJugadorOculto : EventoBase
{
    public string idEscondite;
    public TipoEscondite tipoEscondite;
    public Vector3 posicion;
    public bool entrando;

    // Campos para hipótesis 2.
    public bool cercaEnemigo;
    public string idEnemigoMasCercano;
    public float distanciaEnemigo;
    public float tiempoDesdeUltimoPlayerSpotted;

    public EventoJugadorOculto(
        int nivel,
        string idEscondite,
        TipoEscondite tipoEscondite,
        Vector3 posicion,
        bool entrando
    ) : this(
        nivel,
        idEscondite,
        tipoEscondite,
        posicion,
        entrando,
        false,
        string.Empty,
        -1f,
        -1f
    ) { }

    public EventoJugadorOculto(
        int nivel,
        string idEscondite,
        TipoEscondite tipoEscondite,
        Vector3 posicion,
        bool entrando,
        bool cercaEnemigo,
        string idEnemigoMasCercano,
        float distanciaEnemigo,
        float tiempoDesdeUltimoPlayerSpotted
    ) : base(TipoEvento.PlayerHidden, nivel)
    {
        this.idEscondite = idEscondite ?? string.Empty;
        this.tipoEscondite = tipoEscondite;
        this.posicion = posicion;
        this.entrando = entrando;
        this.cercaEnemigo = cercaEnemigo;
        this.idEnemigoMasCercano = idEnemigoMasCercano ?? string.Empty;
        this.distanciaEnemigo = distanciaEnemigo;
        this.tiempoDesdeUltimoPlayerSpotted = tiempoDesdeUltimoPlayerSpotted;
    }
}

[Serializable]
public class EventoItemRecogido : EventoBase
{
    public TipoItem tipoItem;
    public Vector3 posicion;
    public EstadoJugador estadoJugador;
    public bool cercaEnemigo;
    public string idItem;

    public EventoItemRecogido(int nivel, TipoItem tipoItem)
        : this(nivel, tipoItem, Vector3.zero, EstadoJugador.Normal, false, string.Empty) { }

    public EventoItemRecogido(
        int nivel,
        TipoItem tipoItem,
        Vector3 posicion,
        EstadoJugador estadoJugador,
        bool cercaEnemigo,
        string idItem
    ) : base(TipoEvento.ItemPicked, nivel)
    {
        this.tipoItem = tipoItem;
        this.posicion = posicion;
        this.estadoJugador = estadoJugador;
        this.cercaEnemigo = cercaEnemigo;
        this.idItem = idItem ?? string.Empty;
    }
}

[Serializable]
public class EventoItemUsado : EventoBase
{
    public TipoItem tipoItem;

    // Campos clave para hipótesis 4.
    public Vector3 posicion;
    public EstadoJugador estadoJugador;
    public bool cercaEnemigo;
    public string idEnemigoMasCercano;
    public float distanciaEnemigo;
    public float tiempoDesdeUltimoPlayerSpotted;
    public bool jugadorFatigado;
    public int cantidadRestante;

    public EventoItemUsado(int nivel, TipoItem tipoItem)
        : this(nivel, tipoItem, Vector3.zero, EstadoJugador.Normal, false, string.Empty, -1f, -1f, false, -1) { }

    public EventoItemUsado(
        int nivel,
        TipoItem tipoItem,
        Vector3 posicion,
        EstadoJugador estadoJugador,
        bool cercaEnemigo,
        string idEnemigoMasCercano,
        float distanciaEnemigo,
        float tiempoDesdeUltimoPlayerSpotted,
        bool jugadorFatigado,
        int cantidadRestante
    ) : base(TipoEvento.ItemUsed, nivel)
    {
        this.tipoItem = tipoItem;
        this.posicion = posicion;
        this.estadoJugador = estadoJugador;
        this.cercaEnemigo = cercaEnemigo;
        this.idEnemigoMasCercano = idEnemigoMasCercano ?? string.Empty;
        this.distanciaEnemigo = distanciaEnemigo;
        this.tiempoDesdeUltimoPlayerSpotted = tiempoDesdeUltimoPlayerSpotted;
        this.jugadorFatigado = jugadorFatigado;
        this.cantidadRestante = cantidadRestante;
    }
}

[Serializable]
public class EventoInventarioSnapshot : EventoBase
{
    public int pildoras;
    public int cajas;
    public int relojes;
    public int llaves;
    public string contexto;

    public EventoInventarioSnapshot(
        int nivel,
        int pildoras,
        int cajas,
        int relojes,
        int llaves,
        string contexto
    ) : base(TipoEvento.ItemInventorySnapshot, nivel)
    {
        this.pildoras = pildoras;
        this.cajas = cajas;
        this.relojes = relojes;
        this.llaves = llaves;
        this.contexto = contexto ?? string.Empty;
    }
}
