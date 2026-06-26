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

    [Header("Persistencia")]
    [SerializeField] private bool usarServidor = false;
    [SerializeField] private string endpointServidor = "http://localhost:3000/telemetry";
    [SerializeField] private bool guardarCsv = false;

    [Header("Rendimiento")]
    [SerializeField] private float flushInterval = 10f;
    [SerializeField] private int maxBufferedEvents = 500;

    private float flushTimer = 0f;
    private bool initialized = false;
    private float sessionStartRealtime = 0f;

    public string IdSesion => idSesion;
    public bool IsInitialized => initialized;
    public int BufferedEventsCount => events != null ? events.Count : 0;

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

    public static void Track(EventoBase evento)
    {
        if (Instance != null)
            Instance.TrackEvent(evento);
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
            persistenceObject?.Send(e);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Traker] Error en TrackEvent. El juego continúa. {ex.Message}");
        }
    }

    public void FlushNow()
    {
        if (!initialized) return;

        try
        {
            persistenceObject?.Flush();
            flushTimer = 0f;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Traker] Error en FlushNow. El juego continúa. {ex.Message}");
        }
    }

    private void Update()
    {
        if (!initialized) return;

        flushTimer += Time.deltaTime;
        if (flushTimer >= flushInterval)
            FlushNow();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            FlushNow();
    }

    private void OnApplicationQuit()
    {
        if (!initialized) return;

        try
        {
            float duracionSesion = Time.realtimeSinceStartup - sessionStartRealtime;
            TrackEvent(new EventoFinSesion(SafeGetNivelActual(), duracionSesion));
            FlushNow();
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
            sessionStartRealtime = Time.realtimeSinceStartup;

            ISerializer serializer = guardarCsv ? (ISerializer)new CsvSerializer() : new JsonSerializer();

            if (usarServidor)
            {
                persistenceObject = new ServerPersistence(serializer, endpointServidor, this);
            }
            else
            {
                string extension = guardarCsv ? "csv" : "jsonl";
                string fileName = $"telemetry_{idSesion}.{extension}";
                persistenceObject = new FilePersistence(serializer, fileName, guardarCsv);
            }

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
        try
        {
            return SceneManager.GetActiveScene().buildIndex;
        }
        catch
        {
            return -1;
        }
    }
}