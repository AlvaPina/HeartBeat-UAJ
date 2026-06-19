using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class EnemyCombatTracker : MonoBehaviour
{
    [Header("Combat Radius")]
    [SerializeField] private float enterRadius = 12f;
    [SerializeField] private float exitRadius = 15f;

    [Header("Id")]
    [SerializeField] private string combatAreaId = "";

    private bool enemyAlive = true;
    private bool encounterOpen = false;
    private bool hadAnyEscape = false;
    private bool playerInside = false;

    private Transform playerTransform;

    private void Awake()
    {
        if (string.IsNullOrEmpty(combatAreaId))
            combatAreaId = Guid.NewGuid().ToString();
    }

    private void Start()
    {
        RefreshPlayerReference();
    }

    private void Update()
    {
        if (!enemyAlive) return;

        if (playerTransform == null)
        {
            RefreshPlayerReference();
            if (playerTransform == null)
                return;
        }

        float distance = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.y),
            new Vector2(playerTransform.position.x, playerTransform.position.y)
        );

        if (!playerInside && distance <= enterRadius)
        {
            playerInside = true;

            if (!encounterOpen)
                StartEncounter();
        }
        else if (playerInside && distance >= exitRadius)
        {
            playerInside = false;

            if (encounterOpen)
            {
                hadAnyEscape = true;
                EndEncounter(true);
            }
        }
    }

    public void NotifyEnemyDeath()
    {
        if (!enemyAlive) return;

        enemyAlive = false;

        if (encounterOpen)
        {
            playerInside = false;
            EndEncounter(false);
            return;
        }

        if (hadAnyEscape)
        {
            SafeTrackCombatEnd(false);
        }
    }

    private void RefreshPlayerReference()
    {
        if (GameManager.Player != null)
        {
            playerTransform = GameManager.Player.transform;
        }
    }

    private void StartEncounter()
    {
        encounterOpen = true;

        Traker.Instance?.TrackEvent(
            new EventoInicioCombate(
                GetNivelActual(),
                combatAreaId,
                GetPlayerPosition()
            )
        );
    }

    private void EndEncounter(bool huida)
    {
        encounterOpen = false;
        SafeTrackCombatEnd(huida);
    }

    private void SafeTrackCombatEnd(bool huida)
    {
        Traker.Instance?.TrackEvent(
            new EventoFinCombate(
                GetNivelActual(),
                combatAreaId,
                GetPlayerPosition(),
                huida
            )
        );
    }

    private int GetNivelActual()
    {
        return SceneManager.GetActiveScene().buildIndex;
    }

    private Vector3 GetPlayerPosition()
    {
        if (playerTransform != null)
            return playerTransform.position;

        return transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enterRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, exitRadius);
    }
}