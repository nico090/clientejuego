# Plan: Conversión de PvE (Boss Room) a PvP Arena

## Contexto
El juego era un cooperativo contra un boss. Se quiere cambiar a PvP arena donde:
- Matar NPC = +1 punto
- Matar jugador = +3 puntos
- Morir por NPC = -3 puntos
- Match de 5 minutos, gana el de mayor puntaje

---

## ✅ Fase 1 — PvPScoreManager

> Sistema de puntaje base.

- `PvPScoreManager.cs` creado con scoring, timer, SyncDictionary de scores, y sistema de match.

---

## ✅ Fase 2 — Conectar kills/muertes al PvPScoreManager

> Sin esto nada funciona. El sistema de daño ya tenía info del atacante pero se perdía al morir.

| Archivo | Cambio |
|---------|--------|
| `Assets/Scripts/Gameplay/Messages/LifeStateChangedEventMessage.cs` | Agregados campos `KillerNetId` y `KilledByNpc` |
| `Assets/Scripts/Gameplay/GameplayObjects/Character/ServerCharacter.cs` | `m_LastDamager` + `NotifyPvPKill()` al morir |
| `Assets/Scripts/Gameplay/GameplayObjects/PublishMessageOnLifeChange.cs` | Propaga info del killer en el mensaje |
| `Assets/Scripts/Gameplay/GameplayObjects/TossedItem.cs` | `m_ThrowerNetId` + `SetThrower()`, Detonate pasa inflicter |
| `Assets/Scripts/Gameplay/Action/ConcreteActions/TossAction.cs` | Llama `SetThrower(parent.netId)` al crear TossedItem |

---

## ✅ Fase 3 — Respawn de jugadores

> Jugadores ahora respawnean 5s después de morir en un spawn point aleatorio con HP completo.

| Archivo | Cambio |
|---------|--------|
| `Assets/Scripts/Gameplay/GameState/ServerBossRoomState.cs` | `CoroRespawnPlayer()` con 5s delay: reposiciona en spawn point + `Revive()` con full HP. Reemplaza `CheckForGameOver()` para jugadores |
| `Assets/Scripts/Gameplay/Messages/LifeStateChangedEventMessage.cs` | Agregado campo `ServerCharacter` para referencia directa |
| `Assets/Scripts/Gameplay/GameplayObjects/PublishMessageOnLifeChange.cs` | Pasa `m_ServerCharacter` en el mensaje |
| `Assets/Scripts/Gameplay/GameplayObjects/NetworkLifeState.cs` | Ya soportaba Fainted → Alive (sin cambios necesarios) |

---

## ✅ Fase 4 — Modificar flujo del match

> Match ahora es controlado por PvPScoreManager (timer 5min). Boss/game over eliminados.

| Archivo | Cambio |
|---------|--------|
| `Assets/Scripts/Gameplay/GameState/ServerBossRoomState.cs` | Registra jugadores en PvPScoreManager al spawn. `StartMatch()` al cargar escena. Suscribe a `MatchEnded` → `CoroGoToPostGame()`. Eliminados `BossDefeated`, `CheckForGameOver`, `CoroGameOver`. Disconnect ya no triggerea game over |
| `Assets/Scripts/Gameplay/GameState/PvPScoreManager.cs` | Sin cambios (ya tenía toda la lógica de timer/match) |

---

## ✅ Fase 5 — Permitir daño entre jugadores (PvP damage)

> Todas las acciones de combate ahora pueden dañar otros jugadores. Self-damage prevenido.

| Archivo | Cambio |
|---------|--------|
| `Assets/Scripts/Gameplay/Action/ConcreteActions/MeleeAction.cs` | `GetIdealMeleeFoe` busca PCs+NPCs, skip self por netId. Nuevo param `selfNetId` |
| `Assets/Scripts/Gameplay/Action/ConcreteActions/AOEAction.cs` | `PerformAoE` incluye layer "PCs", skip self |
| `Assets/Scripts/Gameplay/Action/ConcreteActions/DashAttackAction.cs` | Pasa `parent.netId` a `GetIdealMeleeFoe` |
| `Assets/Scripts/Gameplay/Action/ConcreteActions/FXProjectileTargetedAction.cs` | Target validation: solo rechaza self, no filtra por IsNpc |
| `Assets/Scripts/Gameplay/Action/ConcreteActions/TrampleAction.cs` | `CollideWithVictim` permite daño player-vs-player |
| `Assets/Scripts/Gameplay/GameplayObjects/Projectiles/PhysicsProjectile.cs` | Agrega layer "PCs" a colisión, skip spawner por netId |
| `Assets/Scripts/Gameplay/GameplayObjects/Character/AI/AIBrain.cs` | Sin cambios (ya solo persigue jugadores) |

---

## ✅ Fase 6 — UI de puntaje en tiempo real

> HUD generado dinámicamente: timer arriba-centro, scores arriba-derecha. Sin cambios a prefabs.

| Archivo | Cambio |
|---------|--------|
| `Assets/Scripts/Gameplay/UI/PvPScoreHUD.cs` | **NUEVO** — Singleton que genera Canvas overlay con timer (TextMeshPro, top-center) y lista de scores (top-right). Lee SyncDictionary de PvPScoreManager cada frame. Resuelve nombres via NetworkNameState |
| `Assets/Scripts/Gameplay/GameState/PvPScoreManager.cs` | Llama `PvPScoreHUD.EnsureExists()` en Awake para crear el HUD en todos los peers |

---

## ✅ Fase 7 — PostGame con scoreboard PvP

> Scoreboard rankeado reemplaza pantalla Win/Loss. Scores sincronizados via SyncVar JSON.

| Archivo | Cambio |
|---------|--------|
| `Assets/Scripts/Gameplay/GameState/NetworkPostGame.cs` | Nuevo SyncVar `FinalScoresJson` + evento `FinalScoresChanged` |
| `Assets/Scripts/Gameplay/GameState/ServerPostGameState.cs` | Lee `PvPScoreManager.GetFinalScoresJson()` y lo pasa a `NetworkPostGame.FinalScoresJson` |
| `Assets/Scripts/Gameplay/UI/PostGameUI.cs` | Oculta Win/Loss, genera scoreboard dinámico con ranking (#1 en amarillo), suscribe a `FinalScoresChanged` |

---

## Verificación

- Hostear partida con 2+ jugadores
- Matar NPC → +1 punto visible en HUD
- Matar jugador → +3 puntos
- Morir por NPC → -3 puntos
- Respawn después de 5 segundos
- Timer cuenta hacia atrás
- Al terminar el match se muestra scoreboard
