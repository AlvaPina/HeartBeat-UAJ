1. TELEMETRÍA

A) Posibles Hipótesis (Marco MDA)
Hipótesis 1 (Mecánica del Pulso bajo presión)

M (Mecánica): La zona verde del minijuego del corazón se reduce en función de la proximidad del enemigo.

D (Dinámica): Bajo la presión de tener a un Lútano cerca, el jugador cometerá más errores rítmicos por el estrés cognitivo de vigilar al enemigo y a la barra a la vez.

A (Estética): Genera tensión.

Métricas a recoger: Tamaño medio de la zona verde en el momento exacto en que se cometen los fallos.

Hipótesis 2 (Uso táctico de armarios)

M (Mecánica): Distribución de escondites estáticos (armarios) por el nivel.

D (Dinámica): El jugador trazará rutas seguras gracias a los armarios.

A (Estética): ayuda a nuestra intención de tener un entorno útil.

Métricas a recoger: Número de usos por cada ID de armario; Porcentaje de armarios no utilizados por nivel.

Hipótesis 3 (la fatiga tiene efecto negativo)

M (Mecánica): Fallar el minijuego 3 veces genera el estado de fatiga (Tired == true), inmovilizando al jugador.

D (Dinámica): La inmovilidad aumentará drásticamente la probabilidad de que el jugador sea interceptado por el cono de visión de un Lútano patrullando.

A (Estética): Castigo que refuerza la tensión.

Métricas a recoger: % de eventos PlayerSpotted (cuando player entra en cono de visión) que ocurren mientras el jugador tiene el estado Tired == true frente a cuando está sano; Tiempo medio de supervivencia tras entrar en fatiga.

B) Los Eventos de Telemetría
Para validar esas hipótesis, necesitaremos los siguientes eventos:

SessionStart / End (Inicio y fin de la ejecución del juego) Parámetros: duracion_sesion

LevelStart / Complete (Flujo de progresión del jugador por los 6 niveles) Parámetros: tiempo_completado

PlayerState (para poder tener muestreos de por donde va) Parámetros: posición, velocidad y estado del jugador

PlayerDeath (El jugador es alcanzado por un enemigo) Parámetros: posición y estado del jugador

PlayerSpotted (El jugador entra en el cono de visión de un enemigo) Parámetros: estado del jugador

HeartbeatAttempt (Cada vez que el jugador pulsa "Espacio" para el pulso) Parámetros: éxito/fallo, tamaño de la zona verde (o lo que es lo mismo "distancia del enemigo mas cercano")

FatigueTriggered (El jugador acumula 3 fallos en el pulso) Parámetros: estado del jugador

PlayerHidden (entrar en armario/caja) Parámetros: ID_escondite y tipo_escondite

ItemPicked (Recogida de un objeto en el mapa) Parámetros: tipo_item

ItemUsed (Uso de Píldora, Reloj, Caja) Parámetros: estado del jugador (fatigado, oculto), enemigos en rango.

**Todos los eventos tienen un evento base que nos indicará timestamp, idEvento, idSesion, tipoEvento y nivel**

C) Informe final con mapa de calor de donde muere el jugador en cada nivel, mapa con el recorrido que sigue, presentación de todas las estadisticas y si las hipotesis quedan validadas o no.

2. INPUT Y REPLAY (opcional, quizas es mucho para dos)
Queremos poder guardar el input de un partida para poder reproducirlo en otra. Cómo vamos a lograr la repetición exacta (determinismo). Tenemos dos retos técnicos para conseguir esto:

El RNG (Aleatoriedad): En EnemyAI.cs usáis Random.Range para el sonido de los enemigos. Tendremos que proponer fijar una semilla (Seed) compartida entre la partida guardada y la repetición.

Los Temporizadores: Guardar el input no por tiempo (Time.time), sino por frame físico (Time.frameCount o en el FixedUpdate()), para que el lag del ordenador no desincronice al jugador de los enemigos.



NOTAS SUCIAS:
- Quizas se puede hacer otra hipotesis con el uso de algún objeto como pildora, caja o reloj.