using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TipoEvento
{
    SessionStart,
    SessionEnd,
    LevelStart,
    LevelComplete,
    PlayerState,
    PlayerDeath,
    PlayerSpotted,
    HeartbeatAttempt,
    FatigueTriggered,
    PlayerHidden,
    ItemPicked,
    ItemUsed
}