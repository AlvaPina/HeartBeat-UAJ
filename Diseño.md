**TELEMETRÍA HEARTBEAT**

**DE QUE TRATA EL JUEGO?**
Es un juego de sigilo y terror donde tienes que ir recorriendo un mapa resolviendo puzzles, escondiendote en armarios, usando objetos que encuentras por ahí y sobretodo manteniendo el ritmo de tu latido para no fatigarte. Os adjunto un vídeo para que podais ver de que trata el juego antes de leer todo esto.
https://youtu.be/xJJZEVgAJT4?si=s21VC8QQ_mAdtz2K

El objetivo de este proyecto será demostrar 4 hipótesis que consideramos interesantes además de mostrar de forma bonita y detallada mapas de calor y estadísticas/gráficas del juego

A) **POSIBLES HIPOTESIS (Marco MDA)**
Hipótesis 1 (Mecánica del Pulso bajo presión)

M (Mecánica): La zona verde del minijuego del corazón se reduce en función de la proximidad del enemigo.

D (Dinámica): Bajo la presión de tener a un Lútano cerca, el jugador cometerá más errores rítmicos por el estrés cognitivo de vigilar al enemigo y a la barra a la vez.

A (Estética): Genera tensión.

Métricas a recoger: 

Tamaño de la zona verde en el momento exacto en que se cometen los fallos.(con la barra verde se representa la distancia al enemigo)
tasa de fallo según rangos de distancia;
(representaremos en una grafica la tasa de fallos en función del tamaño de la barra verde/distancia enemgios)


Hipótesis 2 (Uso táctico de armarios)

M (Mecánica): Distribución de escondites estáticos (armarios) por el nivel.

D (Dinámica): El jugador usará los armarios como nodos de seguridad, modificando su ruta para desplazarse de escondite en escondite.

A (Estética): Genera una sensación de planificación táctica y alivio temporal al encontrar una ruta segura.

Métricas a recoger:
tiempo medio dentro del armario;
porcentaje de usos de armario con enemigo cerca;
muertes ocurridas cerca de armarios no usados;
armario usado justo después de PlayerSpotted.

Hipótesis 3 (la fatiga tiene efecto negativo)

M (Mecánica): Fallar el minijuego 3 veces genera el estado de fatiga (Tired == true), inmovilizando al jugador.

D (Dinámica): La inmovilidad aumentará drásticamente la probabilidad de que el jugador sea interceptado por el cono de visión de un Lútano patrullando.

A (Estética): Castigo que refuerza la tensión.

Métricas a recoger: % de eventos PlayerSpotted (cuando player entra en cono de visión) que ocurren mientras el jugador tiene el estado Tired == true frente a cuando está sano; % medio de supervivencia tras entrar en fatiga.

Hipótesis 4: Uso táctico de objetos
M: El jugador dispone de objetos consumibles como píldora, reloj o caja.
D: El jugador tenderá a usarlos en momentos de amenaza cercana o tras entrar en fatiga.
A: Refuerza la sensación de supervivencia y toma de decisiones bajo presión.
Métricas: porcentaje de objetos usados con enemigos en rango, tiempo entre detección y uso, objetos no usados al morir.

B) **EVENTOS DE LA TELEMETRÍA**
Para validar esas hipótesis, necesitaremos los siguientes eventos:

SessionStart / End (Inicio y fin de la ejecución del juego) Parámetros: duracion_sesion

LevelStart / Complete (Flujo de progresión del jugador por los 6 niveles) Parámetros: tiempo_completado

PlayerState se registrará cada 0.5 segundos o 1 segundo, y también en momentos clave como detección, entrada en escondite, muerte, uso de objeto o activación de fatiga. Parámetros: posición, velocidad, escondido, uso de objetos y estado del jugador

PlayerDeath (El jugador es alcanzado por un enemigo) Parámetros: posición y estado del jugador

PlayerSpotted (El jugador entra en el cono de visión de un enemigo) Parámetros: ID del enemigo; estado del jugador; posición del jugador y enemigo;

posicion jugador (muestreo cada 1s) parametro: position

cambio de estado del jugador (corriendo, andando, oculto o fatigado) parametro: estado_jugador

evento cercania a un enemigo (se mide con booleano que indica si esta cerca o no) parametro: bool cerca_enemigo
evento cercacia a un armario (se mide con booleano que indica si esta cerca o no) parametro: bool cerca_armario
tiempo desde el último HeartbeatAttempt parametro: tiempo
tiempo hasta muerte o escape parametro: tiempo

entrada al armario parametros: position armario, id armario
salida del armario parametros: position armario, id armario

HeartbeatAttempt (Cada vez que el jugador pulsa "Espacio" para el pulso) Parámetros: éxito/fallo, tamaño de la zona verde (o lo que es lo mismo "distancia del enemigo mas cercano")

FatigueTriggered (El jugador acumula 3 fallos en el pulso) Parámetros: estado_jugador

PlayerHidden (entrar en armario/caja) Parámetros: ID_escondite y tipo_escondite

ItemPicked (Recogida de un objeto en el mapa) Parámetros: tipo_item

ItemUsed (Uso de Píldora, Reloj, Caja) Parámetros: tipo_item.

**Todos los eventos tienen un evento base que nos indicará timestamp, idEvento, idSesion, tipoEvento y nivel**

C) Informe final con mapa de calor:

mapas de calor de muertes por nivel;
mapas de recorrido del jugador;
zonas con más PlayerSpotted;
armarios más y menos usados;
relación entre distancia al enemigo y fallos de pulso;
relación entre fatiga y muerte/detección;
uso de objetos antes de morir o escapar;
conclusión por hipótesis: validada, parcialmente validada o no validada.