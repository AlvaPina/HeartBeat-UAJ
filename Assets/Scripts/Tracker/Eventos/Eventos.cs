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
    public float duracionSesion;

    public EventoFinSesion(
        int nivel,
        float duracionSesion
    ) : base(TipoEvento.SessionEnd, nivel)
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
    public float tiempoCompletado;

    public EventoNivelCompletado(
        int nivel,
        float tiempoCompletado
    ) : base(TipoEvento.LevelComplete, nivel)
    {
        this.tiempoCompletado = tiempoCompletado;
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

    public EventoEstadoJugador(
        int nivel,
        Vector3 posicion,
        float velocidad,
        EstadoJugador estadoJugador,
        bool cercaEnemigo,
        bool cercaArmario
    ) : base(TipoEvento.PlayerState, nivel)
    {
        this.posicion = posicion;
        this.velocidad = velocidad;
        this.estadoJugador = estadoJugador;
        this.cercaEnemigo = cercaEnemigo;
        this.cercaArmario = cercaArmario;
    }
}

[Serializable]
public class EventoJugadorMuere : EventoBase
{
    public Vector3 posicion;
    public EstadoJugador estadoJugador;

    public EventoJugadorMuere(
        int nivel,
        Vector3 posicion,
        EstadoJugador estadoJugador
    ) : base(TipoEvento.PlayerDeath, nivel)
    {
        this.posicion = posicion;
        this.estadoJugador = estadoJugador;
    }
}

[Serializable]
public class EventoJugadorDetectado : EventoBase
{
    public string idEnemigo;
    public EstadoJugador estadoJugador;
    public Vector3 posicionJugador;
    public Vector3 posicionEnemigo;

    public EventoJugadorDetectado(
        int nivel,
        string idEnemigo,
        EstadoJugador estadoJugador,
        Vector3 posicionJugador,
        Vector3 posicionEnemigo
    ) : base(TipoEvento.PlayerSpotted, nivel)
    {
        this.idEnemigo = idEnemigo;
        this.estadoJugador = estadoJugador;
        this.posicionJugador = posicionJugador;
        this.posicionEnemigo = posicionEnemigo;
    }
}

[Serializable]
public class EventoIntentoLatido : EventoBase
{
    public bool exito;
    public float tamañoZonaVerde;

    public EventoIntentoLatido(
        int nivel,
        bool exito,
        float tamañoZonaVerde
    ) : base(TipoEvento.HeartbeatAttempt, nivel)
    {
        this.exito = exito;
        this.tamañoZonaVerde = tamañoZonaVerde;
    }
}

[Serializable]
public class EventoFatigaActivada : EventoBase
{
    public EstadoJugador estadoJugador;

    public EventoFatigaActivada(
        int nivel,
        EstadoJugador estadoJugador
    ) : base(TipoEvento.FatigueTriggered, nivel)
    {
        this.estadoJugador = estadoJugador;
    }
}

[Serializable]
public class EventoJugadorOculto : EventoBase
{
    public string idEscondite;
    public TipoEscondite tipoEscondite;
    public Vector3 posicion;
    public bool entrando;

    public EventoJugadorOculto(
        int nivel,
        string idEscondite,
        TipoEscondite tipoEscondite,
        Vector3 posicion,
        bool entrando
    ) : base(TipoEvento.PlayerHidden, nivel)
    {
        this.idEscondite = idEscondite;
        this.tipoEscondite = tipoEscondite;
        this.posicion = posicion;
        this.entrando = entrando;
    }
}

[Serializable]
public class EventoItemRecogido : EventoBase
{
    public TipoItem tipoItem;

    public EventoItemRecogido(
        int nivel,
        TipoItem tipoItem
    ) : base(TipoEvento.ItemPicked, nivel)
    {
        this.tipoItem = tipoItem;
    }
}

[Serializable]
public class EventoItemUsado : EventoBase
{
    public TipoItem tipoItem;

    public EventoItemUsado(
        int nivel,
        TipoItem tipoItem
    ) : base(TipoEvento.ItemUsed, nivel)
    {
        this.tipoItem = tipoItem;
    }
}