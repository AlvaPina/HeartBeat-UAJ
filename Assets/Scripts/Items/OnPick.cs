using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
public class OnPick : MonoBehaviour
{
    [SerializeField] private int _id;
    Inventory _Inventory;

    private void Start()
    {
        _Inventory = GameManager.Instance.GetComponent<Inventory>();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            if (_id == 1 && !_Inventory._PildoraEquipado)
            {
                GameManager.PlayerStates.PlayPickUpAudio();
                _Inventory.AñadeObjeto(_id);
                Traker.Instance?.TrackEvent(new EventoItemRecogido(SceneManager.GetActiveScene().buildIndex, TipoItem.Pildora));
                Destroy(gameObject);
            }
            else if (_id == 2 && !_Inventory._CajaEquipado)
            {
                GameManager.PlayerStates.PlayPickUpAudio();
                _Inventory.AñadeObjeto(_id);
                Traker.Instance?.TrackEvent(new EventoItemRecogido(SceneManager.GetActiveScene().buildIndex, TipoItem.Caja));
                Destroy(gameObject);
            }
            else if (_id == 3 && !_Inventory._DespertadorEquipado)
            {
                GameManager.PlayerStates.PlayPickUpAudio();
                _Inventory.AñadeObjeto(_id);
                Traker.Instance?.TrackEvent(new EventoItemRecogido(SceneManager.GetActiveScene().buildIndex, TipoItem.Reloj));
                Destroy(gameObject);
            }
            else if (_id == 4 && !_Inventory._LlaveEquipado)
            {
                GameManager.PlayerStates.PlayPickUpAudio();
                _Inventory.AñadeObjeto(_id);
                GameManager.Instance.getSpawn.setCP(true);
                Traker.Instance?.TrackEvent(new EventoItemRecogido(SceneManager.GetActiveScene().buildIndex, TipoItem.Llave));
                Destroy(gameObject);
            }
        }
    }
}

