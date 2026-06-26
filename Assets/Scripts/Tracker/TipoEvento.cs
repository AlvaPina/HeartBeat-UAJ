using UnityEngine;

public enum TipoEvento
{
    // Flujo de sesión y nivel
    SessionStart,
    SessionEnd,
    LevelStart,
    LevelComplete,
    LevelFail,

    // Estado, peligro y muerte
    PlayerState,
    PlayerDeath,
    PlayerSpotted,

    // Minijuego de latido / fatiga
    HeartbeatAttempt,
    FatigueTriggered,
    FatigueEnded,

    // Escondites
    HideoutRegistered,
    PlayerHidden,

    // Objetos
    ItemPicked,
    ItemUsed,
    ItemInventorySnapshot
}
