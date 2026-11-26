# Dino Revenge: Punk Beer Heist

Plataformero 2D en Unity: controlas a un dinosaurio punk que recorre una cueva peleando contra calaveras, rockeros, fantasmas cerveceros y otros enemigos, junta cervezas para curarse y al final se enfrenta a un jefe que lanza hachas.

Es el proyecto de la materia **Gramáticas Computacionales** (Universidad Panamericana, 7.º semestre). El juego tiene dos modos:

- **Historia (`Jugar`)**: un nivel diseñado a mano (`FirstGameplayScene`).
- **Arcade (`Arcade`)**: una partida procedural (`ProceduralScene`). Los cuartos, los cuartos secretos y los enemigos se generan con autómatas celulares, un agente que traza la ruta y un sistema de puntuación que adapta la dificultad a cómo juegas.

---

## Índice

1. [Requisitos y cómo abrirlo](#requisitos-y-cómo-abrirlo)
2. [Controles](#controles)
3. [Mecánicas](#mecánicas)
4. [Escenas](#escenas)
5. [Generación procedural (modo Arcade)](#generación-procedural-modo-arcade)
6. [Dificultad adaptativa (PlayerScore)](#dificultad-adaptativa-playerscore)
7. [Enemigos](#enemigos)
8. [Jefe](#jefe)
9. [Estructura del proyecto](#estructura-del-proyecto)
10. [Herramientas de editor y pruebas](#herramientas-de-editor-y-pruebas)
11. [Problemas conocidos / pendientes](#problemas-conocidos--pendientes)
12. [Equipo y créditos](#equipo-y-créditos)

---

## Requisitos y cómo abrirlo

| | |
|---|---|
| Motor | **Unity 6000.0.53f1** (Unity 6) |
| Render | URP 2D (Universal Render Pipeline 17.0.4) |
| Paquetes clave | Cinemachine 3.1.4, Input System 1.14, 2D Feature (Tilemap, Tilemap Extras, Sprite, Animation, Aseprite, PSD Importer), TextMesh Pro |

1. Clona el repo:
   ```powershell
   git clone https://github.com/omarsanlop05/DinoRevenge_PunkBeerHeist.git
   ```
2. Ábrelo desde Unity Hub con la versión **6000.0.53f1** (otra versión puede pedir actualizar el proyecto).
3. Abre `Assets/Scenes/MainMenu.unity` y dale Play.

> `Library/`, `Temp/`, `Logs/` y los `.csproj` los genera Unity y no deben subirse.

---

## Controles

| Acción | Tecla |
|---|---|
| Moverse | `A` / `D` o flechas (eje `Horizontal`) |
| Saltar | `Espacio` (con *coyote time* y *jump buffer*) |
| Atacar (mordida) | Clic izquierdo |
| Tomar cerveza (curarse) | `E` |

Dentro del nivel hay carteles con las instrucciones (`Assets/Prefabs/ControlInstructions`).

---

## Mecánicas

- **Movimiento** (`PlayerMovement.cs`, clase `PlayerController`): caminar, salto con gravedad propia (cae más rápido de lo que sube y el salto se corta si sueltas la tecla), *coyote time* de 0.1 s, *buffer* de salto de 0.15 s y soporte para saltos múltiples (`maxJumpCount`).
- **Ataque**: una mordida que empuja al dino hacia adelante siguiendo una `AnimationCurve`. Hace 15 de daño y tiene 1.1 s de cooldown. La hitbox es `AttackPointBehaviour`.
- **Vida** (`PlayerHealth.cs`): 150 puntos (siempre se empieza con la vida llena), 1 s de invulnerabilidad y *knockback* al recibir daño. Al morir aparece la pantalla de muerte con *Retry* y *Menú*.
- **Cervezas** (`Collectibles.cs`, `CervezaUI.cs`): se guardan en un inventario de máximo 2 y cada una cura 25 al tomarla con `E`.
- **Peligros**: `KillOnFall` mata al caer al vacío y `WallTrigger` maneja las paredes que se activan o destruyen.
- **Cámara**: cámaras de Cinemachine por cuarto, todas con tamaño ortográfico **11.125** (lo define `RoomGenerator.roomCameraOrthoSize` y se aplica a cada cuarto). `CameraChanger` cambia de cámara al cruzar una puerta y congela un momento al jugador durante la transición. También hay `ParallaxBackground` y recentrado vertical (`CameraYRecentering`).
- **Decoración**: letreros de neón que parpadean (`NeonFlicker`) e imágenes que aparecen por zona (`ShowImageOnZone*`).

---

## Escenas

| Escena | Uso |
|---|---|
| `MainMenu` | Menú principal (`MenuSystem`): Jugar, Arcade, Salir |
| `FirstGameplayScene` | Nivel hecho a mano (modo historia) con jefe al final |
| `ProceduralScene` | Modo Arcade: cuartos, enemigos y cuartos secretos procedurales |
| `BossTest` | Escena para probar la pelea con el jefe |
| `RoomsTest` | Escena para probar el generador de cuartos |
| `enemigosPruebas` | Demo de la fábrica de enemigos procedurales |

---

## Generación procedural (modo Arcade)

Lo orquesta `GameManagerProcedural` y lo construye `RoomGenerator` usando la configuración `RoomGenConfig` (ScriptableObject: tamaño del cuarto de 38×24 tiles, grosor de muros, alturas de puerta, alcance del salto del jugador, etc.).

### Flujo de una partida

```
Semilla aleatoria (o fija para pruebas)
  └─ FillWorld: todo el mundo empieza como un bloque sólido gigante
       └─ Se tallan los cuartos 1 y 2 (initialRooms)
            └─ Al entrar al cuarto i se genera el i+1 (siempre uno adelante), hasta totalRooms = 7
                 └─ Tras el último cuarto: Pasillo (prefab) → al entrar, se instancia el Cuarto del Jefe (prefab)
```

Cada cuarto se genera con `baseSeed + índice`, así que **con la misma semilla sale el mismo mundo**.

### Pipeline de cada cuarto (`RoomGenerator.Generate`)

1. **Cascarón orgánico con autómata celular**: se mete ruido (`shellNoisePercent` = 40 %) en la franja de muros, techo y suelo, y después se aplican varias iteraciones de suavizado con vecindario de Moore (`GetNeighbors`). Al final se abren los marcos de las puertas de entrada y salida.
2. **Puertas a distinta altura**: cada puerta toma una fila de `doorRows` (2, 8 o 13). El origen del cuarto siguiente se calcula para que su puerta de entrada quede alineada con la salida del anterior (`RoomData.NextOrigin`).
3. **Ruta crítica con agente** (`BuildLayout` / `TryPlanLayout` / `BuildChain`): un agente arma una cadena de plataformas de la entrada a la salida respetando la física del jugador (`maxJumpHeight`, `min/maxJumpDistance`, `playerHeight`). Puede ir en zig-zag (80 % hacia adelante, 20 % de retroceso), tiene un "presupuesto de huecos" para los pozos y deja una separación vertical mínima entre plataformas. Si un layout no es válido lo vuelve a intentar (hasta 40 veces). En los últimos intentos genera cuartos más simples, sin pilares, como red de seguridad.
4. **Plataformas extra**: se agregan otras alcanzables desde alguna superficie existente (`IsReachable`).
5. **Superficies etiquetadas**: cada superficie se guarda como `Surface` con su tipo (suelo, plataforma, pilar, saliente) y si es parte del camino principal. Eso es lo que usa `EnemyPlacer`.
6. **Cámara y triggers**: una cámara estática por cuarto (con `CinemachineConfiner2D`), un `RoomEntryTrigger` y un `CameraChanger` de regreso al cuarto anterior.
7. **Cuarto secreto** (opcional, ver abajo).
8. **Enemigos** (`EnemyPlacer`, en dos pasadas, ver [Enemigos](#enemigos)).
9. **Decoración** (`RoomDecorator`, ver abajo).

### Decoración (de cueva a ciudad neón)

`RoomDecorator` pone plantas y letreros neón en cada cuarto procedural, sobre los tiles ya tallados. Solo es visual: no tiene colliders.

- **Transición**: el primer cuarto solo tiene plantas y los últimos 2 (`neonOnlyRoomsAtEnd`: el penúltimo y el último antes del pasillo) solo tienen neón. Los de en medio pasan poco a poco de uno a otro (`índice / (totalRooms − 2)`). Con 7 cuartos, la proporción de neón es 0 %, 20 %, 40 %, 60 %, 80 %, 100 % y 100 %. El pasillo y el cuarto del jefe no se decoran.
- **Plantas pegadas a la roca**:
  - arbustos sobre el suelo;
  - lianas colgando del techo o de debajo de las plataformas (se acortan si no hay espacio);
  - enredaderas en paredes, techo o suelo.

  Una parte de cada planta (`plantSink` = 20 %) queda metida en la roca. Van en la capa `EasterEggs`, detrás del tilemap, como en el nivel 1, así que esa parte no se ve.
- **Letreros neón en zonas abiertas**: son 21.
  - Los 10 carteles LED animados de `Assets/Carteles/Prefabs`. Se usan los prefabs tal cual (su animación, material y capa) y solo se escalan. El cartel del título del juego no se usa.
  - 11 letreros hechos de sprites. Los que vienen cortados en piezas ("No Dinosaurs", "No Biting", "Bite") se arman con todas sus piezas en la posición que tienen en la imagen. Estos parpadean como tubo de neón (`NeonSignFlicker`) y van en la capa `Decoraciones`.
- **Dónde y cuántos letreros**: cada letrero busca un hueco libre con 1 tile de margen, lejos de las puertas laterales y dentro del encuadre de la cámara. Si no cabe, se encoge hasta un 64 % o no se pone. Hay como máximo 3 por cuarto (`maxNeonsPerRoom`), también en los cuartos secretos.
- **Repetición mínima**: el decorador lleva la cuenta de toda la partida y siempre elige uno de los letreros menos usados. Ninguno se repite hasta que se hayan usado todos. En una prueba de 20 partidas (con 3 cuartos secretos cada una) no se repitió ninguno.
- **Cuartos secretos**: siempre llevan de 2 a 3 letreros neón. Se decoran cuando rompes la puerta (evento `RoomGenerator.SecretRoomRevealed`), porque antes no están tallados.
- **Configuración**: los sprites, prefabs, tamaños y cantidades están en `Assets/Resources/RoomDecorationSet.asset` (`RoomDecorationSet`) y se pueden cambiar desde el inspector. Un cuarto que es puras plantas lleva de 12 a 18; uno que es puro neón, de 2 a 3 letreros. Para agregar un cartel animado nuevo, crea un elemento en `neons` y arrastra su prefab al campo `prefab`. `GameManagerProcedural` tiene `decorateRooms` para apagar la decoración, y un campo para usar otro set.
- Si un cuarto tiene cuarto secreto, no se pone decoración encima de su puerta rompible, para que se siga viendo como un muro que se puede romper.

### Cuartos secretos

Pueden salir en los cuartos 3 a 7. Cada partida tiene **al menos 2** y **como máximo `totalRooms − 2` = 5** (los cuartos 1 y 2 se generan al iniciar, antes de que el jugador haga nada). El juego le da una ayuda a quien la está pasando mal:

- **Elegibilidad**: un cuarto puede tenerlo si llevas el mínimo de golpes (`secretMinHits` = 0, 0, 1, 1, 2 para los cuartos 3–7) **o** si cualquiera de tus tres puntuaciones está en **−25 o menos** (`secretScoreThreshold`).
- **Probabilidad**: por golpes es 85 % y sube 10 % por cada golpe extra (tope 100 %); por score bajo es 90 %. Baja ×0.95 por cada cuarto secreto ya generado y ×0.85 si el cuarto anterior ya tenía uno.
- **Garantía**: si los cuartos que faltan apenas alcanzan para llegar al mínimo de 2, se fuerzan sin tirar dados.
- **Generación**: un cuarto secreto solo cabe en ciertos layouts (un suelo plano junto a un muro lateral, con la puerta de ese lado lejos). Por eso, cuando un cuarto debe llevarlo, `RoomGenerator` lo vuelve a generar completo (puertas, cascarón y plataformas) hasta 30 veces (`secretRoomAttempts`). En los reintentos, el 75 % de las veces elige puertas que facilitan la entrada (entrada a ras de suelo y salida alta, `secretFriendlyDoorsChance`). En una prueba de 60 partidas se construyeron 300 de 300 cuartos secretos pedidos, con unos 3 intentos por cuarto en promedio; antes de este cambio solo salía el 10 %.
- **Forma**: un muro lateral del cuarto se ensancha en un bloque de roca con una **puerta rompible** (`SecretWall1`) que siempre queda dentro del encuadre de la cámara. Detrás hay un pasillo, un pozo con escalón y un túnel que baja escalonado hasta un cuarto **del mismo tamaño, justo debajo**. Ese cuarto también usa el cascarón del autómata celular y tiene una escalera de plataformas que baja hasta el suelo.
- **Variedad**: cada cuarto secreto sortea su propio estilo (`SecretStyle`):
  - la altura a la que sale el túnel (filas 14 a 18 del cuarto);
  - el ruido de las paredes y las iteraciones del autómata;
  - la separación de la escalera (2 o 3 filas) y el largo de sus plataformas;
  - qué tan seguido da vuelta a media sala;
  - de 0 a 3 plataformas laterales y de 0 a 3 montículos en el suelo;
  - si la **cerveza** está en el suelo o en alto, en una plataforma lateral.

  En una prueba con 400 cuartos secretos salieron 400 layouts distintos.
- **Túnel**: mide 4 filas de alto y cada escalón tiene al menos 2 columnas de ancho, para que quepa el cuerpo del jugador (1.2 × 1.8) al subir de regreso. En la escalera, los escalones de 3 filas quedan pegados (sin hueco), porque con ese desnivel el salto ya no alcanza a cruzar distancia.
- **Validación**: antes de aceptarlo, el generador simula el recorrido de plataformero sobre los tiles finales y exige poder ir de la puerta a la cerveza **y regresar**. El modelo usa la física real del jugador, leída de `PlayerController` (fuerza de salto, multiplicadores de gravedad, velocidad y tamaño del collider): un cuerpo de 2 columnas, y cada salto calcula cuánto avanza según el tiempo en el aire y el techo que tenga encima. Si no se puede, prueba otro diseño. En una prueba con 150 cuartos secretos, un bot con `Rigidbody2D` y el mismo collider que el jugador logró ir y volver en los 150.
- **Cámara**: `SecretCameraZone` cambia entre la cámara del cuarto secreto y la del cuarto principal según dónde esté el centro del jugador (abajo o arriba del borde del cuarto principal), con un margen de medio tile para que no parpadee. Reemplaza a los dos `CameraChanger` que se traslapaban en el pozo.
- Todo se planea y valida al generar el cuarto, pero solo se talla cuando rompes la puerta (`SecretRoomReveal`). Encontrarlo suma puntos (`RegisterSecretRoomFound`).

### Pasillo y cuarto del jefe

`HallwayPrefab` y `BossRoomPrefab` son prefabs hechos a mano con sus propios Tilemaps. Se colocan alineando su `entryAnchor` con la puerta de salida y luego `CarveInterior` hace un *flood fill* desde los anclajes para vaciar solo su interior dentro del bloque del mundo.

---

## Dificultad adaptativa (PlayerScore)

`PlayerScore` lleva **tres puntuaciones** de −100 a 100. Cada una controla un atributo de los enemigos y se ve en su equipo:

| Puntuación | Qué mide | Atributo del enemigo | Se ve como | Multiplicador |
|---|---|---|---|---|
| **Velocidad** | Ritmo: qué tan rápido limpias los cuartos y matas | Velocidad | **Zapatos** | 0.90 – 1.15 |
| **Vida** | Eficiencia ofensiva: cuántos matas y qué tan rápido | Vida | **Casco** | 0.75 – 1.25 |
| **Poder** | Defensa: daño recibido, curaciones | Daño | **Aura** | 0.80 – 1.20 |

Eventos que suman o restan (velocidad / vida / poder):

| Evento | Δ |
|---|---|
| Cambiar de cuarto | +5 / +5 / +5 |
| Matar enemigo (máx. 4 por cuarto) | 0 / +5 / 0 |
| Matar enemigo en < 3 s desde que despertó | +2 / +3 / 0 |
| Limpiar cuarto en ≤ 50 % del tiempo objetivo | +15 / +5 / 0 |
| Limpiar cuarto en ≤ 100 % | +5 / 0 / 0 |
| Limpiar cuarto en > 150 % | −10 / −3 / 0 |
| Cuarto sin recibir daño | 0 / +5 / +15 |
| Por punto de daño recibido | 0 / 0 / −1 |
| Curarse | 0 / −3 / −10 |
| Encontrar cuarto secreto | +5 / +5 / +5 |
| Derrotar al jefe | +15 / +25 / +15 |
| Morir | −50 / −50 / −50 |

El tiempo objetivo de cada cuarto es `6 s + 5 s × número de enemigos`. Lo mide `RoomCombatTracker`, que además mantiene dormidos a los enemigos hasta que entras.

Al generar un cuarto se toma una "foto" de las tres puntuaciones (`EnemyDifficulty`). De esa foto salen la **cantidad de enemigos** (1 a 4, según el promedio y con algo de ruido) y el **nivel de casco, zapatos y aura** (1 a 3) de cada uno. Lo que ves en el sprite es lo que el enemigo hace de verdad.

---

## Enemigos

### Enemigos procedurales (`Assets/enemigosProcedural`)

**16 personajes en 3 tipos**, cada uno con **27 variantes visuales** (casco × zapatos × aura, niveles 1 a 3):

| Tipo | Personajes | Script |
|---|---|---|
| `contacto` | beerGlup, hinchaChivas, ogreBouncer, policiaRebelde, zombiePunk | `ContactVariant` |
| `proyectil` | bartender (martini), calacaGlam (murciélago), mesera (tarro de cerveza), rataPiro (cohete), rataRockera (molotov), skaterSkeleton (lata de aerosol) | `VariantEnemyPatrol` + `VariantEnemyAttack` + `VariantProjectile` |
| `volador` | brewerGhost, cyberPigeon, deliveryZombie, failedMusician, hipsterVampire | `FlyerVariant` |

- `CatalogoEnemigos` (ScriptableObject) guarda los `AnimatorOverrideController` de cada variante. El índice es `(aura-1)*9 + (zapatos-1)*3 + (casco-1)`.
- `FabricaEnemigos.Crear(tipos, casco, zapatos, aura, rng, ...)` elige un personaje con un `System.Random` sembrado y devuelve el GameObject ya animado.
- `EnemyVariantBase` concentra la lógica común: dormir hasta que se active el cuarto, detectar si se atoró, rebotar con otros enemigos, cooldown de daño por contacto y los límites del cuarto.
- Hay más detalle en [`Assets/enemigosProcedural/LEEME.txt`](Assets/enemigosProcedural/LEEME.txt).

### Colocación (`EnemyPlacer`)

1. **Pasada 1, placeholders** (`EnemyPlaceholder`): busca posiciones válidas al azar y anota qué hay alrededor (aire, plataforma aérea, fuera del camino principal, suelo del camino principal). Respeta una zona segura de 10 columnas junto a la entrada, una separación mínima entre enemigos y espacio libre arriba o alrededor.
2. **Pasada 2, spawn**: convierte cada placeholder en el tipo que le toca (voladores en el aire, terrestres en superficies de al menos 4 tiles), con máximo 2 del mismo tipo por cuarto. A los voladores les arma una ruta en línea, en triángulo o vertical y les aplica los multiplicadores.

### Enemigos del nivel hecho a mano (`Assets/Scripts/Enemy`)

- `EnemyPatrol` / `EnemyAttack` / `Projectile`: esqueleto que patrulla y lanza huesos.
- `RollingSkull_Raycast`: calavera que rueda pegada a las superficies usando raycasts.
- `BatEnemy` (`FlyingDude.cs`): volador que patrulla entre puntos y se lanza contra el jugador.
- `EnemyHealth`: vida compartida que reporta las muertes a `PlayerScore`.

---

## Jefe

`BossIA` (`BossAi.cs`) es una máquina de estados (`Persiguiendo`, `AtacandoMelee`, `AtacandoRango`, `Esperando`) que decide según **tres zonas de distancia**:

| Zona | Distancia | Acción |
|---|---|---|
| 1 | ≤ 5 | Ataque melee (`BossAttackHitbox` / `HitboxAttackMelee`) |
| 2 | ≤ 10 | Te persigue |
| 3 | ≤ 16 | Lanza un **hacha** (`ProyectilHacha`: 25 de daño) |
| 4 | > 16 | Te persigue |

- Se despierta con `BossActivator` al entrar a su arena, que también cambia la música y la cámara.
- En el cuarto procedural el jefe usa `EnemyHealth` con **10 de vida** (valor del prefab `BossRoom`); al morir reproduce su animación de muerte y muestra la pantalla de victoria.
- Las animaciones llaman a los ataques con *Animation Events*. Si una animación no avisa que terminó, el ataque se corta solo a los 2 s.

---

## Estructura del proyecto

```
Assets/
├─ Scenes/                  Escenas (ver tabla arriba)
├─ Scripts/
│  ├─ Player/               PlayerMovement, PlayerHealth, PlayerScore, AttackPointBehaviour
│  ├─ Enemy/
│  │  ├─ Boss/              BossAi, BossHealth, BossActivator, hitboxes, ProyectilHacha
│  │  ├─ NormalEnemy/       Patrulla + ataque + proyectil
│  │  ├─ Flying/  Roller/   Murciélago y calavera rodante
│  │  └─ Variants/          Comportamientos de los enemigos procedurales
│  ├─ RoomGeneration/       RoomGenerator, RoomGenConfig, RoomData, EnemyPlacer, triggers, prefabs de pasillo/jefe
│  ├─ Camera/               CameraChanger, seguimiento, parallax
│  ├─ Objects/  Hazards/    Coleccionables, neones, zonas, KillOnFall
│  ├─ UI/                   VidaUI, CervezaUI, StartMenu/MenuSystem
│  ├─ GameManager.cs        Manager del nivel hecho a mano
│  ├─ GameManagerProcedural.cs  Manager del modo Arcade
│  └─ SoundManager.cs / MusicStart.cs
├─ enemigosProcedural/      Fábrica, catálogo, animaciones, sprites y prefabs de los 16 enemigos
├─ Prefabs/                 Player, BossRoom, HallwayForBoss, cerveza, muro secreto, esqueletos
├─ Sprites/                 Dino, enemigos, fondos, UI, paletas de tiles
├─ SFX/                     Música y efectos
└─ Cave Platformer Tileset/ Tileset de la cueva
```

---

## Herramientas de editor y pruebas

- **`RoomGenerator`** (clic derecho en el componente):
  - *Generar cuarto de prueba* / *Generar 3 cuartos de prueba* (usan `testSeed`)
  - *Rellenar mundo (bloque gigante)*
  - *Limpiar Tilemaps*
- **`GameManagerProcedural`**: `useRandomSeed` / `baseSeed` para repetir una partida, `forceSecretRooms` para que siempre se intente generar cuarto secreto y `totalRooms` / `initialRooms`.
- **`EnemyPlacer`**: con `spawnEnemies = false` solo se dejan los placeholders, `keepPlaceholders` y `logPlacement` sirven para depurar.
- **Menú `Herramientas`** (de `enemigosProcedural/Editor`):
  - *Generar animaciones*: crea clips, el controller base y los 27 overrides de cada enemigo.
  - *Actualizar catálogo de enemigos*
  - *Configurar prefabs de enemigos*
  - *Preparar demo de enemigos*
- `TileInspector`: muestra en consola el tile que está bajo el cursor.
- Toda la generación escribe en consola con prefijos (`[RoomGenerator]`, `[GameManager]`, `[EnemyPlacer]`, `[Score]`), incluida la semilla de la partida.

---

## Problemas conocidos / pendientes

- **Build Settings desactualizados**: `ProceduralScene` no está en la lista de escenas, así que el botón *Arcade* falla en un build. Además, la lista incluye `ForgeScene`, `ForgeScene 1`, `ForgeScene 2` y `PlayScene`, que ya no existen (`ForgeScene 2` incluso está habilitada). Hay que agregar `ProceduralScene` y quitar las demás desde *File → Build Profiles*.
- **Dificultad del jefe**: `BossRoomPrefab.Initialize` recibe el multiplicador de dificultad, pero todavía no lo aplica al jefe (hay un `TODO`).
- Los métodos `RegisterChallengeRoom` y `RegisterNeutralRoomAction` de `PlayerScore` ya existen para futuros cuartos de reto, descanso o tienda, pero ningún cuarto los usa todavía.

---

## Equipo y créditos

Proyecto de equipo para Gramáticas Computacionales:

- **Omar A.** ([@omarsanlop05](https://github.com/omarsanlop05)): diseño del nivel, ajustes de jugabilidad, música y build.
- **José Pablo Soto** ([@JpSotoSanchez](https://github.com/JpSotoSanchez)): jefe, enemigos, arte, generación procedural de cuartos, cuartos secretos y sistema de puntuación.
- **Valeria P. M.**: enemigos procedurales (fábrica, catálogo, variantes) y su generación según cómo juegas.

**Assets de terceros**: *Cave Platformer Tileset* (tiles, fondos y escorpiones). La música en `Assets/SFX` (temas de *Castlevania II*, *Donkey Kong Country* y *Super Meat Boy*) pertenece a sus autores y aquí solo se usa con fines académicos, sin fines de lucro.
