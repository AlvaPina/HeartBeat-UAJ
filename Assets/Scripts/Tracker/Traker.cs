using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Traker : MonoBehaviour
{
    public static Traker Instance { get; private set; }

    private List<EventoBase> events;
    private IPersistence persistenceObject;
    private string idSesion;

    [SerializeField] private float flushInterval = 10f;
    [SerializeField] private int maxBufferedEvents = 500;
    private float flushTimer = 0f;
    private bool initialized = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SafeInit();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void TrackEvent(EventoBase e)
    {
        if (!initialized || e == null) return;

        try
        {
            e.AsignarSesion(idSesion);

            if (events.Count >= maxBufferedEvents)
                events.RemoveAt(0);

            events.Add(e);

            if (persistenceObject != null)
                persistenceObject.Send(e);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Traker] Error en TrackEvent. El juego continúa. {ex.Message}");
        }
    }

    private void Update()
    {
        if (!initialized) return;

        try
        {
            flushTimer += Time.deltaTime;
            if (flushTimer >= flushInterval)
            {
                persistenceObject?.Flush();
                flushTimer = 0f;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Traker] Error en Flush periódico. El juego continúa. {ex.Message}");
        }
    }

    private void OnApplicationQuit()
    {
        if (!initialized) return;

        try
        {
            TrackEvent(new EventoFinSesion(SafeGetNivelActual()));
            persistenceObject?.Flush();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Traker] Error al cerrar sesión. El juego continúa. {ex.Message}");
        }
    }

    private void SafeInit()
    {
        try
        {
            idSesion = Guid.NewGuid().ToString();
            events = new List<EventoBase>();

            ISerializer serializer = new JsonSerializer();
            persistenceObject = new FilePersistence(serializer, $"telemetry_{idSesion}.jsonl");

            initialized = true;

            TrackEvent(new EventoInicioSesion(SafeGetNivelActual()));
        }
        catch (Exception ex)
        {
            initialized = false;
            Debug.LogWarning($"[Traker] No se pudo inicializar telemetría. El juego continúa sin tracker. {ex.Message}");
        }
    }

    private int SafeGetNivelActual()
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
    }
}