using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using Unity.VisualScripting;

public class PlayerStates : MonoBehaviour
{

    #region parameters
    [SerializeField] private GameObject _boxPrefab;
    private GameObject _boxInstance;
    private GameObject _parentBoxInstance;
    [SerializeField] private float _timeOutPill = 10;
    private float _oldSpeed;
    private float _fatigueStartTime;
    #endregion


    #region properties

    private bool _tired = false;
    public bool Tired { get { return _tired; } }

    private bool _withEffect = false;
    public bool WithEffect { get { return _withEffect; } }

    private bool _hidden;
    public bool Hidden { get { return _hidden; } }

    private bool _isBox;
    public bool IsBox { get { return _isBox; } }
    #endregion

    #region references
    [SerializeField] private GameObject _warning;
    [SerializeField] private GameObject _heart;
    [SerializeField] private GameObject _safeZone;
    [SerializeField] private GameObject _heartBar;

    [SerializeField] private GameObject _clockPrefab;
    private GameObject _clock;

    [SerializeField] private GameObject _playerInCloset;
    private Inventory _inventory;
    private GameObject _player;
    private Animator _playerAnimator;

    private AudioSource _pickUpAudio;
    private AudioSource _boxAudio;
    private AudioSource _tiredAudio;
    private float _pickUpVolume;
    private float _boxVolume;
    private float _tiredVolume;

    private GameObject instancedBoxAudio;
    #endregion


    #region methods
    public void PlayTiredAudio()
    {
        _tiredAudio.Play();
    }
    public void PlayPickUpAudio()
    {
        _pickUpAudio.Play();
    }
    public void PlayBoxAudio()
    {
        _boxAudio.Play();
    }
    public void Clock()
    {
        _inventory.EliminaObjeto(3);
        _clock = Instantiate(_clockPrefab, _player.transform.position, new Quaternion(0, 0, 0, 0));
        _clock.GetComponent<ClockDistractionComponent>().SetInstance = true;

        GameManager.Instance.GetEntornoCercano(out bool cEne, out string idEn, out float distE, out _, out _, out _, out _);
        Traker.Track(new EventoItemUsado(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex, TipoItem.Reloj, _player.transform.position,
                     _tired ? EstadoJugador.Fatigado : EstadoJugador.Normal, cEne, idEn, distE, -1f, _tired, 0));
    }
    public void EnterCloset()
    {
        _playerInCloset.SetActive(true);
        _hidden = true;

        GameManager.Instance.GetEntornoCercano(out bool cEne, out string idEn, out float distE, out _, out _, out _, out _);
        Traker.Track(new EventoJugadorOculto(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex, "Armario", TipoEscondite.Armario, transform.position, true, cEne, idEn, distE, -1f));
    }
    public void ExitCloset()
    {
        _playerInCloset.SetActive(false);
        _hidden = false;

        GameManager.Instance.GetEntornoCercano(out bool cEne, out string idEn, out float distE, out _, out _, out _, out _);
        Traker.Track(new EventoJugadorOculto(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex, "Armario", TipoEscondite.Armario, transform.position, false, cEne, idEn, distE, -1f));
    }
    public void EnterBox() // player entra a caja
    {
        instancedBoxAudio = new GameObject();
        AudioSource _instancedAudioSource = instancedBoxAudio.AddComponent<AudioSource>();
        _instancedAudioSource.clip = _boxAudio.clip;
        _instancedAudioSource.volume = _boxAudio.volume;
        _instancedAudioSource.Play();
        _player.SetActive(false);
        _playerInCloset.SetActive(true);

        _parentBoxInstance = new GameObject();
        _parentBoxInstance.transform.position = _player.transform.position;
        _boxInstance = Instantiate(_boxPrefab, _parentBoxInstance.transform);
        _isBox = true;

        GameManager.Instance.GetEntornoCercano(out bool cEne, out string idEn, out float distE, out _, out _, out _, out _);
        Traker.Track(new EventoItemUsado(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex, TipoItem.Caja, transform.position, EstadoJugador.Normal, cEne, idEn, distE, -1f, false, 0));
        Traker.Track(new EventoJugadorOculto(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex, "Caja", TipoEscondite.Caja, transform.position, true, cEne, idEn, distE, -1f));
    }
    public void ExitBox() // player sale de caja
    {
        Destroy(_parentBoxInstance);
        Destroy(instancedBoxAudio);
        _player.SetActive(true);
        PlayBoxAudio();
        _playerInCloset.SetActive(false);
        _inventory._cajaEquipado = false;
        _inventory.EliminaObjeto(2);
        _isBox = false;

        GameManager.Instance.GetEntornoCercano(out bool cEne, out string idEn, out float distE, out _, out _, out _, out _);
        Traker.Track(new EventoJugadorOculto(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex, "Caja", TipoEscondite.Caja, transform.position, false, cEne, idEn, distE, -1f));
    }
    public void PillEffect()
    {
        if (!_withEffect)
        {
            //activamos efecto pasti 
            _heart.GetComponent<HeartMove>().enabled = false;
            _heart.GetComponent<HeartDetection>().enabled = false;
            _heart.GetComponent<HeartDetection>()._pillEffects = true;
            _safeZone.GetComponent<ProximityComponent>().enabled = false;
            _heartBar.SetActive(false);
            Invoke("Flicker", _timeOutPill - 4);
            Invoke("ActiveHeart", _timeOutPill);
            _withEffect = true;
            //quitamos pasti del inventario
            _inventory._pildoraEquipado = false;
            _inventory.EliminaObjeto(1);

            GameManager.Instance.GetEntornoCercano(out bool cEne, out string idEn, out float distE, out _, out _, out _, out _);
            Traker.Track(new EventoItemUsado(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex, TipoItem.Pildora, transform.position, _tired ? EstadoJugador.Fatigado : EstadoJugador.Normal, cEne, idEn, distE, -1f, _tired, 0));
        }
    }

    public void Pause()
    {
        if (!GameManager.Instance.IsPause)
        {
            _heart.GetComponent<HeartMove>().enabled = false;
            _safeZone.GetComponent<ProximityComponent>().enabled = false;
            _heart.GetComponent<HeartDetection>().enabled = false;
        }
        else
        {
            _heart.GetComponent<HeartMove>().enabled = true;
            _safeZone.GetComponent<ProximityComponent>().enabled = true;
            _heart.GetComponent<HeartDetection>().enabled = true;
        }

    }

    private void Flicker()
    {
        _heartBar.SetActive(true);
        _heartBar.transform.GetChild(0).GetComponent<ParpadeoComponent>().enabled = true;
        _heartBar.transform.GetChild(1).GetComponent<ParpadeoComponent>().enabled = true;
        _heartBar.transform.GetChild(2).GetComponent<ParpadeoComponent>().enabled = true;

    }

    private void ActiveHeart()
    {
        _heartBar.transform.GetChild(0).GetComponent<ParpadeoComponent>().enabled = false;
        _heartBar.transform.GetChild(0).GetComponent<Image>().color += new Color(0, 0, 0, 0.25f);
        _heartBar.transform.GetChild(1).GetComponent<ParpadeoComponent>().enabled = false;
        _heartBar.transform.GetChild(1).GetComponent<Image>().color += new Color(0, 0, 0, 0.25f);
        _heartBar.transform.GetChild(2).GetComponent<ParpadeoComponent>().enabled = false;
        _heartBar.transform.GetChild(2).GetComponent<Image>().color += new Color(0, 0, 0, 0.25f);
        _heart.GetComponent<HeartMove>().enabled = true;
        _heart.GetComponent<HeartDetection>().enabled = true;
        _heart.GetComponent<HeartDetection>()._pillEffects = false;
        _safeZone.GetComponent<ProximityComponent>().enabled = true;
        _withEffect = false;
    }

    public void CancelMovementTutorial()
    {
        _playerAnimator.SetBool("Sweat", true);
        PlayTiredAudio();
        _oldSpeed = GetComponent<MovementComponent>().speed;
        GetComponent<MovementComponent>().speed = 0;
    }
    public void ActiveMovementTutorial()
    {
        GetComponent<MovementComponent>().speed = _oldSpeed;
        _playerAnimator.SetBool("Sweat", false);
    }
    public void SweatCancelMovement() // fatiga al fallar 3 veces
    {
        GameManager.Instance.GetEntornoCercano(out bool cEne, out string idEn, out float distE, out _, out _, out _, out _);

        Traker.Track(new EventoFatigaActivada(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex,
            EstadoJugador.Fatigado, transform.position, cEne, idEn, distE, 3
        ));

        _fatigueStartTime = Time.time;
        _tired = true;
        _playerAnimator.SetBool("Sweat", true);
        PlayTiredAudio();
        _oldSpeed = GetComponent<MovementComponent>().speed;
        GetComponent<MovementComponent>().speed = 0;
        Invoke("SweatActiveMovement", 3);
    }

    private void SweatActiveMovement() // reanuda el movimiento
    {
        float duracion = Time.time - _fatigueStartTime;
        Traker.Track(new EventoFatigaFinalizada(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex,
            transform.position, duracion, true, EstadoJugador.Normal
        ));

        _tired = false;
        _playerAnimator.SetBool("Sweat", false);
        GetComponent<MovementComponent>().speed = _oldSpeed;
    }
    #endregion

    void Start()
    {
        _isBox = false;
        _inventory = GameManager.Instance.GetComponent<Inventory>();
        _player = GameManager.Player;
        _playerAnimator = _player.GetComponent<Animator>();
        //_audioSource = _player.GetComponent<AudioSource>();
        AudioSource[] _audioArray = GetComponents<AudioSource>();
        _pickUpAudio = _audioArray[0];
        _boxAudio = _audioArray[1];
        _tiredAudio = _audioArray[2];
        _pickUpVolume = _pickUpAudio.volume;
        _boxVolume = _boxAudio.volume;
        _tiredVolume = _tiredAudio.volume;
    }

    // Update is called once per frame
    void Update()
    {
        _tiredAudio.volume = _tiredVolume * GameManager.Instance.getSFX;
        _boxAudio.volume = _boxVolume * GameManager.Instance.getSFX;
        _pickUpAudio.volume = _pickUpVolume * GameManager.Instance.getSFX;
    }


}
