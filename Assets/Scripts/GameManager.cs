using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{

    public enum Menus { PAUSE, OPTIONS, CONTROLS, SOUND, NoMenu, TECLADO };

    #region parameters
    [SerializeField] private float _audioSFX;
    [SerializeField] private float _audioMusic;

    [SerializeField] private float _audioVolume;
    #endregion

    #region references
    [SerializeField] GameObject _UIManager;
    [SerializeField] GameObject _enemyGroup;
    GameObject _spawnManager;
    [SerializeField] GameObject _closets;

    [SerializeField] GameObject _deathAnimation1;
    [SerializeField] GameObject _deathAnimation2;
    #endregion

    #region properties

    private bool _isInPause;

    private int _amountOfChildren;

    private Menus _actualMenu;
    public Menus ActualMenu
    {
        get { return _actualMenu; }
    }

    public bool IsPause
    {
        get
        {
            return _isInPause;
        }
    }

    private SpawnManger _spawner;
    #endregion

    #region getter/setters
    // Instance para poder coger variables del gamemanager
    static private GameManager _instance;
    static public GameManager Instance { get { return _instance; } }

    //referencia al player
    static private GameObject _player;
    static public GameObject Player { get { return _player; } }

    static private PlayerStates _playerStates;
    static public PlayerStates PlayerStates { get { return _playerStates; } }

    static private InputComponent _inputComponent;
    static public InputComponent InputComponent { get { return _inputComponent; } }

    static private Camera _camera;
    static public Camera getCamera { get { return _camera; } }

    //Volumen del audio 
    public float getSFX { get { return _audioSFX; } }
    public float getMusic { get { return _audioMusic; } }

    public SpawnManger getSpawn { get { return _spawner; } }
    #endregion

    #region methods

    // Funciï¿½n arreglada para no usar Tags, busca por los componentes que ya tenï¿½is
    public void GetEntornoCercano(out bool cercaEnemigo, out string idEnemigo, out float distEnemigo, out Vector3 posEnemigo,
                                  out bool cercaArmario, out string idArmario, out float distArmario)
    {
        cercaEnemigo = false; idEnemigo = ""; distEnemigo = -1f; posEnemigo = Vector3.zero;
        cercaArmario = false; idArmario = ""; distArmario = -1f;

        if (_player == null) return;
        Vector3 pPos = _player.transform.position;

        // Busca todos los objetos que tengan el script EnemyAI
        EnemyAI[] enemigos = FindObjectsOfType<EnemyAI>();
        float minE = float.MaxValue;
        foreach (var e in enemigos)
        {
            float d = Vector2.Distance(pPos, e.transform.position);
            if (d < minE) { minE = d; idEnemigo = e.gameObject.name; posEnemigo = e.transform.position; }
        }
        if (minE != float.MaxValue) { distEnemigo = minE; cercaEnemigo = minE <= 10f; }

        // Busca todos los objetos que tengan el script ClosetComponent
        ClosetComponent[] armarios = FindObjectsOfType<ClosetComponent>();
        float minA = float.MaxValue;
        foreach (var a in armarios)
        {
            float d = Vector2.Distance(pPos, a.transform.position);
            if (d < minA) { minA = d; idArmario = a.gameObject.name; }
        }
        if (minA != float.MaxValue) { distArmario = minA; cercaArmario = minA <= 5f; }
    }
    public void GameOver()
    {
        // Calculamos si estaba normal, fatigado u oculto
        EstadoJugador estado = GameManager.PlayerStates.Tired ? EstadoJugador.Fatigado :
                              (GameManager.PlayerStates.Hidden || GameManager.PlayerStates.IsBox ? EstadoJugador.Oculto : EstadoJugador.Normal);

        GetEntornoCercano(out bool cEnemigo, out string idEnemigo, out float distEnemigo, out Vector3 posEnemigo,
                          out bool cArmario, out string idArmario, out float distArmario);

        Inventory inv = GetComponent<Inventory>();

        Traker.Track(new EventoJugadorMuere(
            SceneManager.GetActiveScene().buildIndex,
            _player.transform.position,
            estado,
            cEnemigo, cArmario,
            idEnemigo, idArmario, distEnemigo, distArmario,
            inv._PildoraEquipado, inv._CajaEquipado, inv._DespertadorEquipado,
            "Enemy"
        ));

        GetComponent<PauseInput>().enabled = false;
        GameObject canvas = GameObject.Find("Canvas");
        _player.SetActive(false);
        Instantiate(_deathAnimation1, canvas.transform);
        Instantiate(_deathAnimation2, canvas.transform);
        Invoke("ChangeSceneDeath", 1.5f);
    }
    private void ChangeSceneDeath()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
    public void ChangePause() //Método para cambiar de estado de pausa a estado de despausa
    {
        //Pausados con logica de en que estado se esta en los propios componentes
        

        //Pausado del jugador para activar o deactivar su pausado
        _player.GetComponent<PlayerStates>().Pause();
        _player.GetComponent<MovementComponent>().Pause();

        //Se pone el menu de pausa o se quita
        _UIManager.GetComponent<UIManager>().PauseMenu();

        //Se le envia a los enemigos el mensaje para que se pausen
        _enemyGroup.BroadcastMessage("Pause");

        if (!_isInPause) //No estaba pausado el juego
        {
            Time.timeScale = 0;

            _player.GetComponent<Animator>().enabled = false;
            GetComponent<InputComponent>().enabled = false;

            //Uno a uno se desactivan los animators de los enemigos
            for (int i = 0; i < _enemyGroup.transform.childCount; i++)
            {
                Animator animator = _enemyGroup.transform.GetChild(i).transform.GetChild(0).GetComponent<Animator>();
                if (animator != null)
                    animator.enabled = false;
            }

            DesactivarArmarios();

            //Se cambia al menu de pausa (sin ver la lógica dentro del UI)
            GetComponent<MenuComponent>().ChangeMenu(Menus.PAUSE);
            
            _isInPause= true;
            _actualMenu = Menus.PAUSE;
        }
        else //Igual pero activandolo todo y quitando la pausa
        {
            Time.timeScale = 1;

            _player.GetComponent<Animator>().enabled = true;
            GetComponent<InputComponent>().enabled = true;

            for (int i = 0; i < _enemyGroup.transform.childCount; i++)
            {
                Animator animator = _enemyGroup.transform.GetChild(i).transform.GetChild(0).GetComponent<Animator>();
                if (animator != null)
                    animator.enabled = true;
            }

            ActivarArmarios();

            _isInPause = false;
            _actualMenu = Menus.NoMenu;
        }
    }

    public void DesactivarArmarios()
    {
        for (int i = 0; i < _closets.transform.childCount; i++)
        {
            _closets.transform.GetChild(i).GetComponent<ClosetComponent>().enabled = false;
        }
    }

    public void ActivarArmarios()
    {
        for (int i = 0; i < _closets.transform.childCount; i++)
        {
            _closets.transform.GetChild(i).GetComponent<ClosetComponent>().enabled = true;
        }
    }


    //Método para cambiar los menus según el nuevo estado que le llegue
    private void UpdateMenu(Menus newMenu)
    {
        //Va diciendo al UI a que menu cambiar y va asignando el primer boton para la navegación por teclado de los menús
        if (newMenu == Menus.OPTIONS)
        {
            _UIManager.GetComponent<UIManager>().ChangeMenu(Menus.OPTIONS);
            GetComponent<MenuComponent>().ChangeMenu(Menus.OPTIONS);
        }
        else if (newMenu == Menus.CONTROLS)
        {
            _UIManager.GetComponent<UIManager>().ChangeMenu(Menus.CONTROLS);
            GetComponent<MenuComponent>().ChangeMenu(Menus.CONTROLS);
        }
        else if (newMenu == Menus.SOUND)
        {
            _UIManager.GetComponent<UIManager>().ChangeMenu(Menus.SOUND);
            GetComponent<MenuComponent>().ChangeMenu(Menus.SOUND);
        }
        else if (newMenu == Menus.PAUSE)
        {
            _UIManager.GetComponent<UIManager>().ChangeMenu(Menus.PAUSE);
            GetComponent<MenuComponent>().ChangeMenu(Menus.PAUSE);
        }
        else if (newMenu == Menus.TECLADO)
        {
            _UIManager.GetComponent<UIManager>().ChangeMenu(Menus.TECLADO);
            GetComponent<MenuComponent>().ChangeMenu(Menus.TECLADO);
        }
    }

    //Comprueba si el cambio de menu es posible dentro de la maquina de estados diseñada
    public void RequestMenuChange(Menus newMenu)
    {
        if ((_actualMenu == Menus.PAUSE || _actualMenu == Menus.CONTROLS || _actualMenu == Menus.SOUND || _actualMenu == Menus.TECLADO) && (newMenu == Menus.OPTIONS))
        {
            _actualMenu = newMenu;
            UpdateMenu(newMenu);
        }
        else if (_actualMenu == Menus.OPTIONS && (newMenu == Menus.CONTROLS || newMenu == Menus.SOUND))
        {
            _actualMenu = newMenu;
            UpdateMenu(newMenu);
        }
        else if (_actualMenu == Menus.OPTIONS && (newMenu == Menus.PAUSE))
        {
            _actualMenu = newMenu;
            UpdateMenu(newMenu);
        }
        else if (_actualMenu == Menus.CONTROLS && (newMenu == Menus.TECLADO))
        {
            _actualMenu = newMenu;
            UpdateMenu(newMenu);
        }
        else if (_actualMenu == Menus.TECLADO && (newMenu == Menus.CONTROLS))
        {
            _actualMenu = newMenu;
            UpdateMenu(newMenu);
        }
        else if (_actualMenu == Menus.OPTIONS && (newMenu == Menus.NoMenu))
        {
            _actualMenu = newMenu;
            UpdateMenu(newMenu);
        }
        else if (_actualMenu == Menus.NoMenu && (newMenu == Menus.OPTIONS))
        {
            _actualMenu = newMenu;
            UpdateMenu(newMenu);
        }
    }

    public void changeSound(string soundType, float newValue)
    {
        if (soundType == "MusicSlider")
        {
            _audioMusic = newValue;
            SetSoundChange();
        } else if (soundType == "SFXSlider") 
        { 
            _audioSFX = newValue;
        }
    }

    public void SetSoundChange()
    {
        AudioSource[] audios = _camera.GetComponents<AudioSource>();
        foreach (AudioSource audio in audios)
        {
            audio.volume = _audioVolume * _audioMusic;
        }
    }

    //Sale del juego en el ejecutable
    public void ExitGame()
    {
        Application.Quit();
    }

    
    #endregion
  
    void Awake()
    {
        _instance = this;
        _player = GameObject.Find("Player");
        _playerStates = _player.GetComponent<PlayerStates>();
        _inputComponent = GetComponent<InputComponent>();
        _camera = Camera.main;
        _spawnManager = GameObject.Find("SpawnManager");
        _spawner = _spawnManager.GetComponent<SpawnManger>();
    }
    void Start()
    {
        GetComponent<PauseInput>().enabled = true;
        _isInPause = false;
        _amountOfChildren = _enemyGroup.transform.childCount;
        _audioMusic = _audioSFX = 1;

        Traker.Track(new EventoInicioNivel(SceneManager.GetActiveScene().buildIndex));
    }
}
