# ReCode

> *Una máquina solo hace lo que le dices.*

**ReCode** es un Serious Game en Unity 2D (vista cenital) para aprender **pseudocódigo**.
No controlas al personaje: armas una secuencia de instrucciones (↑ ↓ ← →), la ejecutas con **R** y ves si tu algoritmo funciona. Si falla, el sistema rebobina y tienes que **depurarlo**.

## Qué se aprende
| Nivel | Concepto |
|---|---|
| Primeros pasos | Algoritmo |
| Paredes | Depurar |
| Hielo | Bucle |
| Botón y puerta | Booleano |
| Teletransporte | Función |
| A prueba | Eficiencia |

## Controles
| Tecla | Acción |
|---|---|
| ← ↑ → ↓ | Mover la selección en el menú de direcciones |
| **Z** / **Enter** | Confirmar (agrega una instrucción) |
| **R** | Ejecutar la secuencia |
| **E** | Interactuar (terminales, hablar con el guía, avanzar diálogos) |
| **Esc** | Menú de pausa |

## Mecánicas
- **Cola de comandos:** armas la secuencia y se ejecuta casilla por casilla.
- **Terminales:** al pisarlas, la secuencia se detiene y **E** abre un panel con pseudocódigo.
- **Fragmentos de código (5):** se recogen al pasar por su casilla. Se guardan si ganas el nivel; si chocas, vuelven a su lugar.
- **Guía:** aparece al ganar cada nivel; **E** para hablar y avanzar.
- **HUD:** contador de fragmentos, mini-inventario, pasos usados, nivel y objetivo.

## Estructura del código
Los scripts C# están en `Assets/Scripts/`:
`GridLevel`, `LevelRunner`, `LevelManager`, `LevelExtras`, `FragmentInventory`, `GameHUDManager`, `PromptBubble`, `DialogueController`, `CommandQueueUI`, `MenuNavigator`, `StartMenuUI`, `PauseMenuUI`, `OptionsMenuUI`, `ScreenEffects`, `RewindEffect`, `AnimatedBackground`, `SoundFX`, `MusicManager`, `GameSettings`, entre otros.

## Cómo abrirlo
1. Unity 6 con URP.
2. Abre la carpeta del proyecto con Unity Hub.
3. Abre la escena principal y dale Play.

## Autores
_Kenneth Santiago Ceballos Sierra_ 
_Danna Valentina Arenas Jimenez_ 

· Desarrollo de Contenidos Digitales y Multimediales · B192 · 2026
