using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TipoEvento
{
    SesionStart,//evento Base
    SesionEnd,//evento Base
    LevelStart,
    LevelComplete,
    LevelFail,

    AttackPerformed,
    EnemyEncounterStart,//evento Base
    EnemyEncounterEnd,//evento Base

    PlayerDamaged,
    PlayerDeath,
    PlayerRespawn,//evento Base

    ItemSpawned,//evento Base
    ItemCollected,//evento Base

    AbilityUsed,//evento Base
    AbilityUnlocked,//evento Base
    MagicProjectileHit,//evento Base
    MagicProjectileMiss,//evento Base

    PlayerPositionSample
}
