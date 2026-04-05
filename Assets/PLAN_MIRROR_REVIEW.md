# Revision Mirror + Gameplay - Plan de Tareas

**Fecha:** 2026-04-05
**Estado general:** La migracion NGO -> Mirror esta completa. El sistema PvP esta implementado pero SIN TESTEAR.

---

## RESUMEN DEL ANALISIS

Se revisaron **72 archivos C#** que usan Mirror en `Scripts/`. La arquitectura general esta bien estructurada: SyncVars con hooks manuales para server, Commands con authority correcta, ClientRpcs bien usados, patron de persistencia con DontDestroyOnLoad, object pooling funcional.

---

## BUGS ENCONTRADOS

### CRITICOS (pueden crashear o romper el juego)

#### BUG-1: `conn` puede ser null en `SpawnPlayer()`
- **Archivo:** `Scripts/Gameplay/GameState/ServerBossRoomState.cs:248`
- **Problema:** Si el cliente se desconecta entre el check de `NetworkServer.connections.TryGetValue` y `NetworkServer.Spawn(newPlayer, conn)`, `conn` sera null y crashea.
- **Codigo actual:**
  ```csharp
  if (NetworkServer.connections.TryGetValue((int)clientId, out var conn) && conn.identity != null)
      persistentPlayerGO = conn.identity.gameObject;
  // ... mas abajo...
  NetworkServer.Spawn(newPlayer, conn); // conn puede ser null si TryGetValue fallo!
  ```
- **Fix:** Agregar `if (conn == null) { Destroy(newPlayer); return; }` antes del Spawn.

#### BUG-2: Null reference en `ReviveAction.OnUpdate()`
- **Archivo:** `Scripts/Gameplay/Action/ConcreteActions/ReviveAction.cs:43`
- **Problema:** `m_TargetCharacter` puede ser null si `GetComponent<ServerCharacter>()` falla en OnStart (linea 23), pero OnUpdate lo usa sin null check.
- **Codigo actual:**
  ```csharp
  m_TargetCharacter = targetNetworkObject.GetComponent<ServerCharacter>(); // puede ser null
  // ... en OnUpdate:
  if (m_TargetCharacter.LifeState == LifeState.Fainted) // NullReferenceException!
  ```
- **Fix:** En OnStart, agregar `if (m_TargetCharacter == null) return false;` despues del GetComponent.

#### BUG-3: `NetworkHealthState.HitPoints` es public field con SyncVar - hook no se dispara en server
- **Archivo:** `Scripts/Gameplay/GameplayObjects/NetworkHealthState.cs:13`
- **Problema:** A diferencia de los otros SyncVars (NetworkLifeState, ServerCharacter), `HitPoints` es un **campo publico directo**, NO tiene property wrapper que dispare el hook manualmente en el server. Cuando el server escribe `HitPoints = X`, el hook `OnHitPointsChanged` NO se ejecuta en el server/host.
- **Impacto:** Los eventos `HitPointsDepleted`, `HitPointsReplenished` y `HitPointsChanged` NUNCA se disparan en el host. La UI de vida del host no se actualiza correctamente.
- **Fix:** Convertir a property con backing field como los demas SyncVars:
  ```csharp
  [SyncVar(hook = nameof(OnHitPointsChanged))]
  int m_HitPoints;
  public int HitPoints {
      get => m_HitPoints;
      set { var old = m_HitPoints; m_HitPoints = value; if (isServer) OnHitPointsChanged(old, value); }
  }
  ```

### MODERADOS (funcionalidad rota parcialmente)

#### BUG-4: `SyncToNetwork()` se llama CADA FRAME - performance
- **Archivo:** `Scripts/Gameplay/GameState/PvPScoreManager.cs:113`
- **Problema:** `Update()` llama `SyncToNetwork()` cada frame, que serializa JSON y actualiza 3 SyncVars. Esto genera trafico de red innecesario y GC pressure por las allocaciones de string.
- **Fix:** Solo sincronizar cuando los datos cambian (flag dirty) o con throttle (ej. cada 0.5s).

#### BUG-5: Jugador desconectado no se desregistra del PvPScoreManager
- **Archivo:** `Scripts/Gameplay/GameState/ServerBossRoomState.cs:178-182`
- **Problema:** `OnServerClientDisconnected` es NO-OP. El jugador desconectado sigue apareciendo en el scoreboard porque nunca se llama `PvPScoreManager.UnregisterPlayer()`.
- **Fix:** Buscar el netId del avatar del jugador y llamar `UnregisterPlayer()`.

#### BUG-6: `NetworkedMessageChannel` usa `ReplaceHandler` - solo un canal activo
- **Archivo:** `Scripts/Infrastructure/PubSub/NetworkedMessageChannel.cs:40`
- **Problema:** `ReplaceHandler<BossRoomChannelMessage>` reemplaza el handler cada vez que se crea un nuevo canal. Si hay multiples `NetworkedMessageChannel<T>` para distintos tipos T, solo el ULTIMO registrado recibira mensajes. Los anteriores quedan muertos.
- **Fix:** Usar un dispatcher centralizado que rutee por `ChannelName`, o registrar handlers unicos por tipo de mensaje.

#### BUG-7: Late-join con HP=0 causa fainted inmediato
- **Archivo:** `Scripts/Gameplay/GameplayObjects/Character/ServerCharacter.cs:322-328`
- **Problema:** `InitializeHitPoints()` restaura HP de la sesion anterior. Si el jugador estaba fainted al desconectarse, rejoinea con 0 HP y `LifeState = Fainted`, pero el respawn logic de `ServerBossRoomState` no detecta esto (solo escucha cambios de LifeState, no el estado inicial).
- **Fix:** Forzar HP completo y LifeState.Alive cuando es late-join, o disparar respawn.

#### BUG-8: `NetworkedLoadingProgressTracker` usa `FindObjectOfType` en Update
- **Archivo:** `Scripts/Infrastructure/NetworkedLoadingProgressTracker.cs:54`
- **Problema:** `FindObjectOfType<LoadingProgressManager>()` se llama cada frame en Update. Muy costoso.
- **Fix:** Cachear la referencia en OnStartClient o Awake.

### MENORES (mejoras de robustez)

#### BUG-9: `PhysicsProjectile.OnStartClient` - visualization detached sin cleanup
- **Archivo:** `Scripts/Gameplay/GameplayObjects/Projectiles/PhysicsProjectile.cs:108`
- **Problema:** `m_Visualization.parent = null` desacopla el visual del projectile. Si el projectile es destruido/pooled antes de `OnStopClient`, el visual queda huerfano en la escena.
- **Status:** Mitigado parcialmente por `OnStopClient` que re-parenta, pero si el objeto es destruido sin pasar por OnStopClient (ej. scene change), leak.

#### BUG-10: Nombres de Commands inconsistentes
- **Problema:** Algunos Commands usan `Server*Rpc()` (ServerPlayActionRpc) y otros `Cmd*()` (CmdSetProgress, CmdPing). Mirror convention es `Cmd*`.
- **Impacto:** Solo legibilidad, no funcional.

#### BUG-11: `PvPScoreHUD` se destruye en scene change
- **Archivo:** `Scripts/Gameplay/UI/PvPScoreHUD.cs`
- **Problema:** No tiene `DontDestroyOnLoad`. Se destruye al cambiar de escena (BossRoom -> CharSelect). Esto es probablemente intencional ya que el HUD solo debe existir en gameplay, pero si la escena cambia y `PvPNetworkState` sigue vivo, llamara `EnsureExists()` y creara uno nuevo en la escena equivocada.

---

## COSAS QUE ESTAN BIEN

- **SyncVar hook pattern:** Todos los SyncVars criticos (excepto NetworkHealthState) usan el patron correcto de property + manual hook fire en server.
- **Authority checking:** Commands estan bien, `ClientInputSender` verifica `isOwned`, projectiles verifican `isServer`.
- **DontDestroyOnLoad:** `PersistentPlayer` lo maneja correctamente en OnStartServer y OnStartClient.
- **Collection cleanup:** Todos los RuntimeCollection (PersistentPlayer, ClientPlayerAvatar) limpian correctamente en OnStop/OnDestroy con flag `m_AddedToCollection`.
- **Event unsubscription:** Todos los scripts desuscriben eventos en OnStop/OnDestroy.
- **NetworkHooks deduplication:** Correcto patron de solo disparar OnNetworkSpawn en OnStartClient si `!isServer`.
- **ConnectionPayload exchange:** Bien implementado con NetworkMessage custom antes del ready.
- **Object pooling:** NetworkObjectPool registra spawn/unspawn handlers correctamente.
- **Respawn null safety:** `CoroRespawnPlayer` verifica `serverCharacter == null` y LifeState despues del delay.
- **Spawn points:** Se repueblan cuando se agotan (linea 188-191).

---

## PLAN DE TAREAS (por prioridad)

### Fase 1 - Bugs Criticos (COMPLETADA)
| # | Tarea | Archivo | Estado |
|---|-------|---------|--------|
| 1 | Fix null conn en SpawnPlayer | ServerBossRoomState.cs | HECHO |
| 2 | Fix null check en ReviveAction | ReviveAction.cs | HECHO |
| 3 | Fix NetworkHealthState hook pattern | NetworkHealthState.cs | HECHO |

### Fase 2 - Bugs Moderados (COMPLETADA)
| # | Tarea | Archivo | Estado |
|---|-------|---------|--------|
| 4 | Throttle SyncToNetwork en PvPScoreManager | PvPScoreManager.cs | HECHO |
| 5 | Desregistrar jugador desconectado del scoreboard | ServerBossRoomState.cs | HECHO |
| 6 | Fix NetworkedMessageChannel dispatcher unico | NetworkedMessageChannel.cs | HECHO |
| 7 | Fix late-join con HP=0 | ServerCharacter.cs | HECHO |
| 8 | Cachear FindObjectOfType en LoadingProgressTracker | NetworkedLoadingProgressTracker.cs | HECHO |

### Fase 3 - Limpieza y Robustez (COMPLETADA)
| # | Tarea | Archivo | Estado |
|---|-------|---------|--------|
| 9 | Cleanup visualization leak en PhysicsProjectile | PhysicsProjectile.cs | HECHO |
| 10 | Estandarizar nombres de Commands a Cmd* | ServerCharacter, NetworkCharSelection, ClientCharSelectState, ClientInputSender, ChargedActionInput | HECHO |
| 11 | Guard PvPScoreHUD contra scene change | PvPScoreHUD.cs | HECHO |

---

## NOTAS PARA TESTING

1. **Probar como Host:** La mayoria de bugs de SyncVar hooks afectan al host (server+client en el mismo proceso).
2. **Probar desconexion:** Desconectar un cliente durante gameplay y verificar que el scoreboard se actualiza.
3. **Probar late-join:** Conectar un cliente despues de que el match empezo.
4. **Probar respawn:** Morir y verificar que el respawn funciona despues de 5s.
5. **Probar fin de match:** Dejar que el timer llegue a 0 y verificar transicion a CharSelect.
