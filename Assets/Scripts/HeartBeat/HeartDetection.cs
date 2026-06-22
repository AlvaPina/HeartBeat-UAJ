using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class HeartDetection : MonoBehaviour
{
    //El Script se encarga de recibir el input del espacio pulsado y procesarlo según la situación del corazón respecto a la barra de seguridad

    #region references

    [SerializeField]
    private GameObject _safeZone; //Zona verde en la que el jugador debe pulsar

    [SerializeField]
    private GameObject _warning; //Panel rojo claro que avisa si se entra en la zona verde

    [SerializeField]
    private GameObject _closets;

    #endregion

    #region parameters

    [SerializeField] bool DEBUG;
    private int _fails; //Numero de fallos

    #endregion

    #region properties

    private bool _inSafeZone = false; //Se activa si se esta en la zona verde y se desactiva al salir de ella
    private bool _hasPressed = false; //Se activa al presionar el espacio una vez y se desactiva al botar,
                                      //iniciando asi un nuevo proceso
    public bool _pillEffects = false;

    [SerializeField]
    private AudioSource _beepSound;
    [SerializeField]
    private AudioSource _failSound;

    private float _beepVolume;

    [SerializeField] private Sprite _brokenHeart3;
    [SerializeField] private Sprite _brokenHeart2;
    [SerializeField] private Sprite _brokenHeart1;
    [SerializeField] private Sprite _normalHeart;
    [SerializeField] private Sprite _safeHeart;

    private Image _currentImage;

    private bool _canPress;
    #endregion


    #region methots

    public void CanPress() // para el tutorial
    {
        _canPress = true;
    }
    public void CantPress() // para el tutorial
    {
        _canPress = false;
    }
    public void SpacePressed() //Metodo que se activa al pulsar el espacio y realiza acciones diferentes segun el estado del corazón respecto a la barra de pulsaciones
    {
        if (!_hasPressed && _canPress) //Si ya se ha presionado espacio en esa vuelta no hace nada
        {
            if (!_inSafeZone) //Si no esta en la zona segura (FALLO POR PULSAR MAL)
            {
                // Telemetría de Fallo
                Traker.Instance?.TrackEvent(new EventoIntentoLatido(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex,
                    false, // exito = false
                    ObtenerTamañoZonaVerde()
                ));

                _fails++; //Aumenta en uno los fallos
                _failSound.volume = GameManager.Instance.getSFX * _beepVolume / 10;
                _failSound.Play();
                _warning.SetActive(true);

                if (_fails == 1) _currentImage.sprite = _brokenHeart1;
                if (_fails == 2) _currentImage.sprite = _brokenHeart2;
                if (_fails == 3) _currentImage.sprite = _brokenHeart3;
                _hasPressed = true; //Se activa el bool de pulsado
            }
            else if (_fails < 3) // (ACIerto)
            {
                // Telemetría de Acierto
                Traker.Instance?.TrackEvent(new EventoIntentoLatido(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex,
                    true,  // exito = true
                    ObtenerTamañoZonaVerde()
                ));

                _hasPressed = true; //Se activa el bool de pulsado
                _currentImage.sprite = _safeHeart;
            }
        }
    }

    // Método auxiliar para obtener el tamaño de la zona verde
    private float ObtenerTamañoZonaVerde()
    {
        RectTransform rect = _safeZone.GetComponent<RectTransform>();
        if (rect != null)
        {
            return rect.rect.width * rect.localScale.x; // Calcula el tamaño real en pantalla
        }
        return _safeZone.transform.localScale.x; // Por si acaso no usara RectTransform
    }

    public void ResetValues() //Al haber dado una vuelta se activa este metodo para restablecer valores al estado original
    {
        if (_currentImage.sprite == _safeHeart || _currentImage.sprite == _brokenHeart3)
            _currentImage.sprite = _normalHeart;
        _hasPressed = false; //Se reestablece el bool de presionado
        _warning.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D collision) //Metodo que comprueba que se haya entrado a la zona segura
    {
        if (collision.gameObject == _safeZone) //Si el trigger es el de la zona segura
        {
            _beepSound.volume = GameManager.Instance.getSFX * _beepVolume; 
            _beepSound.Play();
            _inSafeZone = true; //El bool de zona segura se activa
        }
    }

    private void OnTriggerExit2D(Collider2D other) //Metodo que comprueba que se ha salido de la zona segura
    {

        if (other.gameObject == _safeZone) //Si el trigger es el de la zona segura
        {
            _inSafeZone = false; //Se muestra que ya no se esta en la zona segura

            if (!_hasPressed && !_pillEffects) //Si no se a presionado el espacio quiere decir que se ha saltado la zona segura y por tanto es un fallo
            {
                Traker.Instance?.TrackEvent(new EventoIntentoLatido(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex,
                false, // exito = false
                ObtenerTamañoZonaVerde()
                ));
                
                _fails++;
                _failSound.volume = GameManager.Instance.getSFX * _beepVolume / 10;
                _failSound.Play();
                _warning.SetActive(true);
                _hasPressed = true;
            }
            if (_fails == 0)
                _currentImage.sprite = _normalHeart;
            if (_fails == 1)
                _currentImage.sprite = _brokenHeart1;
            if (_fails == 2)
                _currentImage.sprite = _brokenHeart2;
            if (_fails == 3)
                _currentImage.sprite = _brokenHeart3;
        }
    }

    #endregion

    private void Awake()
    {
        _canPress = true;
    }
    // Start is called before the first frame update
    void Start()
    {
        _currentImage = GetComponent<Image>();
        _warning.SetActive(false);
        //_beepSound = GetComponent<AudioSource>();
        
        _beepVolume = _beepSound.volume;
    }

    // Update is called once per frame
    void Update()
    {
        if (_fails == 3 && !DEBUG) //Se comprueba si se ha llegado a 3 fallos
        {
            for (int i = 0; i < _closets.transform.childCount; i++)
            {
                if (_closets.transform.GetChild(i).GetComponent<ClosetComponent>().IsHiding)
                {
                    _closets.transform.GetChild(i).GetComponent<ClosetComponent>().ShowPlayer();
                }
            }
            if (GameManager.PlayerStates.IsBox)
            {
                GameManager.PlayerStates.ExitBox();
            }
            GameManager.PlayerStates.SweatCancelMovement();
            GetComponent<HeartMove>().CancelMovement();
            _fails = 0; //Se reestablecen los fallos
        }
    }
}
