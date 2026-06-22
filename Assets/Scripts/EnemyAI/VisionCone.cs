using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using Unity.VisualScripting;
using UnityEngine;

public class VisionCone : MonoBehaviour
{
    #region properties
    /// <summary>
    /// Esta variable controla la velocidad de la transicion de rotación del
    /// cono de visión, valores entre {0.01 y 0.1}
    /// </summary>
    [SerializeField] float _smoothRotation;
    /// <summary>
    /// Esta variable controla el ángulo total de la visión 
    /// </summary>
    float fov;
    /// <summary>
    /// Controla el ángulo desde el cual se crea el rayo (se genera en sentido horario
    /// </summary>
    float angle;
    /// <summary>
    /// Punto de origen del mesh
    /// </summary>
    private Vector3 origin;
    private Vector3 raySource;
    /// <summary>
    /// Rango máximo al que puede ver el enemigo
    /// </summary>
    float viewDistance;

    //Aunque en algunos pone serializeField, los valores van a ser dependientes de los enemigos.
    //Para ello, simplemente habría que quitar el serialize field y acceder a estos valores desde el script
    //del enemigo usando los métodos Set de más abajo.

    /// <summary>
    /// Cuantos rayos se usan para crear la textura. Cuántos más halla, más refinada es la textura pero más tarda 
    /// en renderizar. Recomiendo entre 50 y 100, dependiendo de la cantidad de enemigos que vayan a tener
    /// que hacer los raycast en ese momento.
    /// </summary>
    [SerializeField] int rayCount;
    /// <summary>
    /// Variable para calcular la variación del ángulo. Declarada en el Start. Usada en el bucle para colocar vértices.
    /// </summary>
    private float angleIncrease;

    //booleano que es true si el jugador entra en el cono de visión
    bool playerFound;
    #endregion

    #region references 
    /// <summary>
    /// Referencia al propio mapa mesh.
    /// </summary>
    private Mesh _myMesh;
    /// <summary>
    /// Referencia a la Mask que activa colisiones (p. ej., colisiona con las paredes pero no con el jugador
    /// u otros enemigos)
    /// </summary>
    [SerializeField] LayerMask _myLayerMask;
    //referencia al collider del cono
    PolygonCollider2D _coneCollider;
    //referencia al ENEMYAI
    [SerializeField] EnemyAI _enemyAI;
    #endregion

    #region methods
    /// <summary>
    /// Método matemático para sacar un vector normalizado desde un ángulo en grados
    /// </summary>
    /// <param name="angle"></param>
    /// <returns></returns>
    private Vector3 GetVectorFromAngle(float angle)
    {
        float angRadians = (angle * Mathf.PI) / 180;
        return new Vector3(Mathf.Cos(angRadians), Mathf.Sin(angRadians)).normalized;
    }

    /// <summary>
    /// Método matemático para sacar un ángulo en grados desde un vector cualquiera
    /// </summary>
    /// <param name="vector"></param>
    /// <returns></returns>
    private float GetAngleFromVector(Vector3 vector)
    {
        vector = vector.normalized;
        float k = Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
        if (k < 0)
        {
            k += 360;
        }

        return k;
    }

    //Los siguientes métodos son públicos porque son los Set. SetOrigin y SetAim se deberán llamar en el update
    //de cada enemigo, mientras que SetFov y SetDistance solo durante el Start.

    /// <summary>
    /// Establece el vértice de origen del ángulo
    /// </summary>
    /// <param name="origin"></param>
    public void SetOrigin(Vector3 origin)
    {
        this.raySource = origin;
    }

    /// <summary>
    /// Toma una dirección a la que apuntar y establece el ángulo de origen necesario para que esta esté en la mitad del FOV
    /// </summary>
    /// <param name="aimDirection"></param>
    public void SetAim(Vector3 aimDirection)
    {
        angle = Mathf.LerpAngle(angle, GetAngleFromVector(aimDirection) + fov / 2, _smoothRotation);
    }

    /// <summary>
    /// Establece el tamaño del ángulo que ocupará el FOV
    /// </summary>
    /// <param name="fov"></param>
    public void SetFov(float fov)
    {
        this.fov = fov;
    }

    /// <summary>
    /// Establece la distancia máxima a la que puede ver un enemigo
    /// </summary>
    /// <param name="viewDistance"></param>
    public void SetDistance(float viewDistance)
    {
        this.viewDistance = viewDistance;
    }
    #endregion

    void UpdateConeCollider()
    {
        Vector2[] newPoints = new Vector2[_myMesh.vertices.Length];
        for (int i = 0; i < _myMesh.vertices.Length; i++)
        {
            newPoints[i] = _myMesh.vertices[i];
        }
        _coneCollider.points = newPoints;
    }

    // Start is called before the first frame update
    void Start()
    {
        playerFound = false;
        _myMesh = GetComponent<MeshFilter>().mesh;
        _coneCollider = gameObject.AddComponent<PolygonCollider2D>();
        _coneCollider.isTrigger = true;
        GetComponent<MeshFilter>().mesh = _myMesh;
        origin = Vector3.zero;
    }

    // Update is called once per frame
    // He desplazado esto a LateUpdate ya que nos queremos asegurar de que lo renderiza después de haber recibido 
    // los datos necesarios de la posición y dirección del enemigo.
    void LateUpdate()
    {
        //Establece los valores de origin a zero (borrar una vez implementado SetOrigin) y calcula cuánto
        //tendrá que variar su ángulo tras cada iteración.
        angleIncrease = fov / rayCount;
        //origin = Vector3.zero;

        //Establece arrays para los vértices, los uv (renderizado de texturas) y los triángulos.
        Vector3[] vertices = new Vector3[rayCount + 2];
        Vector2[] uv = new Vector2[rayCount + 2];
        int[] triangles = new int[rayCount * 3];

        //Esto siempre va a ser así puesto que el vértice 0 siempre es el origen
        vertices[0] = origin;
        uv[0] = origin.normalized;

        //Variables auxiliares que se usan en el bucle.
        int vertexIndex = 1, triangleIndex = 0;

        //Si el bucle es muy complicado de entender (no creo), voy a dejar una explicación detallada
        //en el mismo README en el que explico como poner el script en escena en Unity
        for (int i = 0; i <= rayCount; i++)
        {
            Vector3 vertex;
            RaycastHit2D raycast = Physics2D.Raycast(raySource, GetVectorFromAngle(angle), viewDistance, _myLayerMask);

            if (raycast.collider == null)
            {
                vertex = GetVectorFromAngle(angle) * viewDistance;
            }
            else
            {
                vertex = GetVectorFromAngle(angle) * raycast.distance;
            }

            vertices[vertexIndex] = vertex;
            uv[vertexIndex] = vertex.normalized;

            if (i > 0)
            {
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = vertexIndex - 1;
                triangles[triangleIndex + 2] = vertexIndex;
                triangleIndex += 3;
            }

            vertexIndex++;
            angle -= angleIncrease;
        }

        //Recupera el ángulo incicial. Esto lo hice para que no me diese vueltas a la pantalla, pero para cuando
        //el ángulo sea estipulado por el SetAim en cada update, se podrá borrar
        angle += fov + angleIncrease;

        //Se asignan los arrays al mesh
        _myMesh.vertices = vertices;
        _myMesh.uv = uv;
        _myMesh.triangles = triangles;

        UpdateConeCollider();
        if (playerFound)
            _enemyAI.StartChase();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            playerFound = true;

            EstadoJugador estado = GameManager.PlayerStates.Tired ? EstadoJugador.Fatigado :
                       (GameManager.PlayerStates.Hidden || GameManager.PlayerStates.IsBox ? EstadoJugador.Oculto : EstadoJugador.Normal);

            Traker.Instance?.TrackEvent(new EventoJugadorDetectado(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex,
                _enemyAI.gameObject.name, // ID del enemigo (ej: "BlueEnemy")
                estado,
                collision.transform.position, // Posición Jugador
                _enemyAI.transform.position   // Posición Enemigo
            ));
        } 
        else if (_enemyAI.GetCloset == null && collision.gameObject.GetComponent<ClosetComponent>() != null)
        {
            _enemyAI.SetCloset = collision.gameObject;
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            playerFound = false;
        }
        if (_enemyAI.GetCloset == collision.gameObject && collision.gameObject.GetComponent<ClosetComponent>() != null)
        {
            _enemyAI.SetCloset = null;
        }
    }
}
