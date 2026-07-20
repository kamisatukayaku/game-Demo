# Roguelike Remediation Log - 2026-07-01

## Batch 1: Build and architecture safety

- Fixed recursive `data/**/*.json` synchronization into `Resources/Data/**`.
- Added pre-build hash validation for source and packaged JSON mirrors.
- Repaired invalid asmdef metadata GUIDs so Unity enforces real assembly boundaries.
- Removed Shared dependencies on Roguelike and Sandbox implementations.
- Added a narrow `RoguelikeDebugBridge` instead of exposing internal runtime classes.
- Moved Arena BGM ownership from Shared into Roguelike presentation.

Impact: build/data packaging and dependency boundaries only; no gameplay values changed.

## Batch 2: Correctness guards

- Replaced mutable GameEventBus listener indices with stable listener IDs.
- Limited input reset to input keys; meta progression and settings are preserved.
- Added Editor regression validation for event handles, input reset isolation, JSON mirrors,
  projectile pooling, and upgrade prerequisite chains.

Impact: fixes listener lifetime and settings-loss defects without changing event payloads.

## Batch 3: Hot-path lifecycle

- Added separate plain/ranged pools for factory-created `StraightProjectile` objects.
- Replaced per-shot material allocation with a shared material and `MaterialPropertyBlock` color.
- Reset hit history, build modifiers, culling state, trails, and particles before reuse.
- Custom triangle and homing projectiles remain on their existing lifecycle to avoid visual mixing.

Impact: lower projectile allocation and GC pressure; flight, collision, and damage logic unchanged.

## Batch 4: Upgrade responsibility split

- Extracted ID-chain and stat prerequisite checks into `UpgradeEligibilityRules`.
- Kept offer generation, weighting, and random selection behavior unchanged.
- Retained documented Phase 0 `*Layout.cs` migration placeholders.

## Verification

- Unity 2022.3.62f3c1 batch compilation: PASS.
- Core regression validation: PASS (5/5).
- Architecture dependency validation: PASS.
- `Game.Shared.csproj`: 0 errors.
- `Game.Roguelike.csproj`: 0 errors.
- `Game.World.csproj`: 0 errors (verification only; World gameplay code was not modified).
- JSON source/Resources mirror validation: PASS.

Note: the three dotnet projects must be validated sequentially because parallel builds write shared
Unity-generated artifacts and can produce transient file contention.

## Batch 5: Upgrade weight policy split + Detached weapon VFX file split

### Scope

- Modified only: `Assets/Scripts/Shared` (none), `Assets/Scripts/Modes/Roguelike`, `Assets/Editor`.
- No JSON, Prefab, scene, World gameplay, or design changes.

### Task A — Upgrade weight / offer policy extraction

**New files**

- `Progression/UpgradeRules/UpgradeOfferWeightPolicy.cs`
- `Progression/UpgradeRules/UpgradeWeightedPicker.cs`
- `Progression/UpgradeRules/UpgradeOfferDiversityPolicy.cs`

**Modified files**

- `Progression/LevelUpChoiceDatabase.cs` — JSON load, candidate filtering, arena offer orchestration only.
- `Assets/Editor/Validation/CoreRegressionTests.cs` — four deterministic upgrade tests added.
- `Game.Roguelike.csproj` — new compile entries; removed duplicate `ArenaRunRestart.cs` include.

**Responsibility split**

| Class | Before | After |
| --- | --- | --- |
| `LevelUpChoiceDatabase` | JSON load, eligibility, weights, diversity, pick, offer assembly, DTOs | JSON load, eligibility (via `UpgradeEligibilityRules`), offer orchestration, DTOs |
| `UpgradeOfferWeightPolicy` | (inside database) | Single-offer weight: base `offer_weight`, gameplay/attribute multipliers, tag stack bonus, chain continuation, detached weapon ×2.2, capstone arc/event boosts |
| `UpgradeWeightedPicker` | (inside database) | Weighted unique multi-pick; caller supplies `System.Random`; no static game state |
| `UpgradeOfferDiversityPolicy` | (inside database) | Chain-continuation diversity guard (replace one chain pick when all three are continuations) |
| `UpgradeEligibilityRules` | unchanged | `requires_ids` / `requires_any_ids` / stat prerequisites |

**Upgrade weight call chain**

```
LevelUpChoiceDatabase.BuildArenaOffer
  → GetCandidates (UpgradeEligibilityRules.MeetsUpgradeRequirements)
  → CreateWeightContext(s_config.offer_config, pickStacks, BuildMovieArcDirector, ArenaNarrativeEventDirector)
  → UpgradeWeightedPicker.PickWeightedUnique(s_rng, pool, count, picked, pickedIds, weightContext)
       → UpgradeOfferWeightPolicy.ComputeWeight(def, weightContext)  [per candidate, per roll]
  → UpgradeOfferDiversityPolicy.EnsureOfferDiversity(s_rng, picked, pickedIds, all, weightContext)
       → UpgradeWeightedPicker.PickWeightedUnique (replacement pick)
```

### Task B — Detached weapon VFX mechanical split

**New files**

- `Presentation/VFX/DetachedMissileVisual.cs` — missile body, trail, particles, reset.
- `Presentation/VFX/DetachedMissileBurstPool.cs` — `MissileBurstPool`, `MissileBurstFx`, `MissileBurstDriver`.
- `Presentation/VFX/DetachedMissileLockPool.cs` — `MissileLockPool`, `MissileLockFx`, `MissileLockDriver`.
- `Presentation/VFX/DetachedLaserHitSparkPool.cs` — `LaserHitSparkPool`, `LaserHitSparkFx`, `LaserHitSparkDriver`.
- `Presentation/VFX/DetachedWeaponCoreVisual.cs` — `DetachedWeaponVisual`, `PathNode`, `WeaponCorePalette`, `MotionSample`.

**Modified files**

- `Presentation/VFX/DetachedWeaponPresentationSystem.cs` — lifecycle, event registration, weapon scan, public API only (~107 lines).
- `Game.Roguelike.csproj` — new VFX compile entries.

**Responsibility split**

| Class | Before | After |
| --- | --- | --- |
| `DetachedWeaponPresentationSystem` | All VFX types in one file | Singleton lifecycle, pool wiring, `EnsureExists` / `RefreshExistingWeapons` / `AttachMissile` / burst/lock/spark/flash entry points |
| `DetachedWeaponVisual` | same file | Core orbit weapon rendering, rings, beams, path network, explosion flash |
| `DetachedMissileVisual` | same file | Missile projectile visuals |
| `MissileBurstPool` / `MissileLockPool` / `LaserHitSparkPool` | same file | Pooled one-shot FX + drivers (capacities unchanged: 20 / 24 / 48) |

**VFX call chain**

```
Gameplay (MissileBehavior / LaserShotBehavior / …)
  → DetachedWeaponPresentationSystem.AttachMissile / PlayMissileBurst / PlayMissileLock / PlayLaserHitSpark / PlayExplosionCoreFlash
       → EnsureExists → pool Play(...)
DetachedWeaponSystem.WeaponSpawned
  → DetachedWeaponPresentationSystem.Attach → DetachedWeaponVisual (DetachedWeaponCoreVisual.cs)
```

`DetachedWeaponPresentationSystem.cs.meta` GUID preserved on the main MonoBehaviour script.

### Verification (this batch)

- `Game.Shared.csproj`: 0 errors.
- `Game.Roguelike.csproj`: 0 errors.
- `Game.World.csproj`: 0 errors (read-only, not modified).
- `Assembly-CSharp.csproj`: 0 errors (proxy for Unity script compilation).
- Architecture dependency scan (Rule1 Shared→Modes, Rule2 World→Roguelike): PASS (0 violations).
- Unity 2022.3.62f3c1 BatchMode: **not executed** — `Unity.exe` not found on this machine; use local Hub install to confirm import/meta generation.
- `Tools/Validation/Run Core Regression Tests`: **not executed** — requires Unity Editor (`CoreRegressionTests.RunAll` uses `MenuItem` + `PlayerPrefs` / `DestroyImmediate`); new tests compile with `Assembly-CSharp-Editor`.

**New regression tests added (run in Editor after Unity opens project)**

1. Upgrade weighted picker seed determinism.
2. Picker excludes already-used ids.
3. Tag stack increase does not lower weight.
4. Capstone arc (×2) and event (×1.8) multipliers preserved.

### Residual risks

- New VFX `.meta` files will be created on first Unity import (by design); verify no duplicate script GUID warnings in Console.
- Capstone weight still reads live `BuildMovieArcDirector` / `ArenaNarrativeEventDirector` statics inside `UpgradeOfferWeightPolicy` (same as pre-split behavior).
- Full offer golden-seed parity vs pre-split build not byte-compared; formula and picker loop are line-preserving extractions.
- Sandbox VFX smoke test requires Play Mode in `ArchetypeSandbox` (manual).

## Batch 6: Ring Arena tutorial + ground zone information system

### Scope

- Added event-driven tutorial and ground-zone info under `Tutorial/` + JSON in `data/roguelike/tutorial/`.
- Minimal gameplay publishes only (events); no tutorial logic in combat core.
- Settings reset via `RoguelikeTutorialResetBridge` (Shared hook, Roguelike registration).

### New files

**Data**

- `data/roguelike/tutorial/tutorial_steps.json`
- `data/roguelike/tutorial/ground_zones.json`
- `Assets/Resources/Data/tutorial/*` (mirrored)

**Roguelike**

- `Tutorial/TutorialEvents.cs`
- `Tutorial/RoguelikeTutorialState.cs`
- `Tutorial/TutorialStepDatabase.cs`
- `Tutorial/GroundZoneDefinitionDatabase.cs`
- `Tutorial/RoguelikeTutorialDirector.cs`
- `Tutorial/TutorialEventListener.cs`
- `Tutorial/TutorialPromptUI.cs`
- `Tutorial/GroundZoneInfoPresenter.cs`
- `Tutorial/GroundZoneProximityTracker.cs`
- `Tutorial/TutorialGameplayEventPublisher.cs`
- `Tutorial/RoguelikeTutorialSandboxUI.cs`

**Shared**

- `Shared/UI/RoguelikeTutorialResetBridge.cs`

### Modified files

- `RoguelikeCombatSceneBootstrap.cs` — tutorial system bootstrap
- `RoguelikeGameplayRegistration.cs` — reset bridge registration
- `LevelUpController.cs` — `TutorialUiBlockingEvent` during pick UI
- `PlayerDashController.cs` — `PlayerDashedEvent`
- `DetachedWeaponSystem.cs` — `DetachedWeaponAcquiredEvent`
- `ArenaXpZoneController.cs` — `GroundZoneSpawnedEvent`
- `ArenaMidWaveEventDirector.cs` — zone spawn events (meteor/storm/portal)
- `UI/ArenaTutorialController.cs` — thin forwarder to director
- `Shared/UI/StartGameUIShared.cs` — settings “重置新手引导”
- `CoreRegressionTests.cs` — 4 tutorial tests (13/13 total)
- `Game.Roguelike.csproj`, `Game.Shared.csproj`

### Tutorial state flow

```
WaveStartedEvent (W1)
  → RoguelikeTutorialDirector.OnArenaEntered → step "move"
PlayerMovedEvent
  → complete "move" → offer "dash"
PlayerDashedEvent → complete "dash"
DamageEvent (player attacker) → offer/complete "auto_attack"
TriggerActivatedEvent/XpPickupCollectedEvent → offer/complete "xp_pickup"
TutorialUiBlockingEvent(level_up) → offer "level_up" (footer hint, sort 1245)
UpgradeAppliedEvent/DetachedWeaponAcquiredEvent → offer "detached_weapon"
DetachedWeaponImpactEvent → complete "detached_weapon"
```

Persistence: `Roguelike.Tutorial.Step.*`, `Roguelike.Tutorial.Zone.*`, `Roguelike.Tutorial.ZoneProx.*`

### Ground zone info flow

```
Gameplay spawn (XP zone / mid-wave / hazard)
  → GroundZoneSpawnedEvent
  → GroundZoneInfoPresenter (first encounter label + line, 2.5–3.2s)
  → GroundZoneProximityTracker
       → proximity hint (once per zoneId)
       → GroundZoneEnteredEvent / GroundZoneExitedEvent
       → director status hints
Definitions: GroundZoneDefinitionDatabase ← tutorial/ground_zones.json
```

### New events

- `PlayerMovedEvent`, `PlayerDashedEvent`, `AutoAttackHitEvent` (via DamageEvent filter)
- `XpPickupCollectedEvent`, `GroundZoneSpawnedEvent`, `GroundZoneEnteredEvent`, `GroundZoneExitedEvent`
- `DetachedWeaponAcquiredEvent`, `DetachedWeaponImpactEvent`, `TutorialUiBlockingEvent`

### Verification (this batch)

- `Game.Shared.csproj`: 0 errors
- `Game.Roguelike.csproj`: 0 errors
- `Game.World.csproj`: 0 errors (unchanged)
- `Assembly-CSharp.csproj`: 0 errors
- Architecture scan: PASS
- Unity 2022.3.62f3c1 BatchMode: executed (exit 0)
- Core Regression Tests / JSON mirror: run in Editor (`Tools/Validation/Run Core Regression Tests`, `Tools/Sync Data → Resources/Data`)

### Residual risks

- Hazard zone spawn events not yet wired for `ArenaHazardController` (definitions exist; hazard id mapping pending).
- `GroundZoneProximityTracker` resolves player once via tag lookup at startup (not per-frame Find).
- Full Play Mode tutorial pacing (dash trigger near danger) needs in-arena manual QA.
- Legacy `arena_tutorial_done` PlayerPrefs key replaced by `Roguelike.Tutorial.*` prefix (one-time re-show for returning players).

## Batch 7: Upgrade policy and detached VFX split verification

### Upgrade offer responsibilities

- `LevelUpChoiceDatabase`: JSON loading, candidate pool assembly, starter routing and offer orchestration.
- `UpgradeEligibilityRules`: ID-chain and stat prerequisite checks.
- `UpgradeOfferWeightPolicy`: pure weight formula; runtime tag stacks and capstone matching are injected through `WeightContext`.
- `UpgradeWeightedPicker`: deterministic weighted unique selection using the caller-owned `System.Random`.
- `UpgradeOfferDiversityPolicy`: replaces an all-chain offer with one non-chain alternative when available.

Weight flow:

```
LevelUpChoiceDatabase.BuildArenaOffer
  -> CreateWeightContext
  -> UpgradeWeightedPicker
  -> UpgradeOfferWeightPolicy.ComputeWeight
  -> UpgradeOfferDiversityPolicy
  -> LevelUpOffer
```

The formula remains:

```
base * gameplay-or-attribute multiplier
  + tag stack bonuses
  * chain continuation bonus
  * detached weapon bonus
  * movie-arc capstone boost
  * active narrative capstone boost
```

### Detached weapon VFX responsibilities

- `DetachedWeaponPresentationSystem`: lifecycle, weapon attachment and stable public VFX entry points.
- `DetachedWeaponCoreVisual`: Contact/evolution core presentation and route palette state.
- `DetachedMissileVisual`: missile body, trail, shards and reuse reset.
- `DetachedMissileBurstPool`: launch/hit burst pool and drivers.
- `DetachedMissileLockPool`: target lock/link pool and drivers.
- `DetachedLaserHitSparkPool`: laser hit spark pool and drivers.

Pool capacities remain unchanged: missile bursts 20, missile locks 24, laser hit sparks 48.
All original public entry points remain available and each moved runtime type has exactly one definition.

### Corrections made during verification

- Removed live global-state reads from `UpgradeOfferWeightPolicy`; runtime state is now injected as read-only delegates.
- Replaced the tag-weight test's global build mutation with an in-memory tag stack provider.
- Added a seed-4242 golden sequence assertion (`test_e,test_c,test_d`).
- Added an exact full-formula weight assertion.
- Corrected the already-used-ID regression test to validate exclusion while still allowing unused choices.
- Mirrored tutorial JSON into `Resources/Data/roguelike/tutorial`; JSON mirror validation now passes.

### Verification

- Unity 2022.3.62f3c1 BatchMode compilation: PASS.
- Core regression tests: PASS (14/14).
- Architecture validation: PASS.
- `Game.Shared.csproj`: 0 errors.
- `Game.Roguelike.csproj`: 0 errors.
- `Game.World.csproj`: 0 errors (read-only verification).
- `Assembly-CSharp-Editor.csproj`: 0 errors.

## Batch 8: Runtime acceptance, Ground Zone wiring, Sandbox VFX regression (2026-07-01)

### Scope

- Modified: `Shared/` (BossPlayerDamageRules from prior batch), `Modes/Roguelike/`, `Debug/Sandbox/`, `Editor/`, `data/roguelike/tutorial/` (mirror only).
- No World gameplay, upgrade weights, weapon mechanics, or numeric design changes.

### Fixes

| Issue | Root cause | Fix | Verification |
| --- | --- | --- | --- |
| Hazard zones missing tutorial events | `ArenaHazardController` never published `GroundZoneSpawnedEvent` | `PublishHazardGroundZones()` on wave hazard apply for `toxic_edge`, `laser_sweep`, `gravity_well`, `dual_ring_outer` | dotnet + JSON id registry |
| Support heal ring not in tutorial | `SupportHealRingVfx` had no zone event | Publish `support_heal_ring` once when support build ring enables | Ground zone id in JSON |
| `_dashStepEligible` CS0414 | Field written but never read | Gate `OnPlayerDashed()` on eligibility flag | 0 warnings in Roguelike build |
| VFX pool reuse stale state | Pools lacked explicit reset API | `ResetAll()` / `Deactivate()` on burst/lock/spark pools; `ResetPoolsForSandbox()` | Editor regression test |
| Weapon visual state on disable | Trails/beams not cleared on recycle | `DetachedWeaponVisual.OnDisable()` clears trails, beams, path nodes | Code review + sandbox panel |
| Presentation singleton leak | `WeaponSpawned` not cleared on destroy | `OnDestroy` unsubscribe + null `s_instance` | Code review |
| Sandbox VFX regression gap | No dedicated debug harness | `SandboxDetachedWeaponRegressionPanel` + `SandboxDetachedWeaponRegressionService` in Sandbox UI | Manual Play Mode |

### Ground Zone publish map (complete)

| Zone ID | Publisher |
| --- | --- |
| `xp_boost_zone` | `ArenaXpZoneController` |
| `meteor_strike_zone` | `ArenaMidWaveEventDirector` |
| `energy_storm_zone` | `ArenaMidWaveEventDirector` |
| `warp_portal_zone` | `ArenaMidWaveEventDirector` |
| `toxic_edge` | `ArenaHazardController` |
| `laser_sweep` | `ArenaHazardController` |
| `gravity_well` | `ArenaHazardController` |
| `dual_ring_outer` | `ArenaHazardController` (wave ≥ 12) |
| `support_heal_ring` | `SupportHealRingVfx` |

Enter/exit: `GroundZoneProximityTracker` → `GroundZoneEnteredEvent` / `GroundZoneExitedEvent`.

### New / modified files

- `Tutorial/GroundZoneIds.cs` — gameplay-published id registry for tests.
- `Combat/ArenaHazardController.cs` — hazard zone events.
- `Presentation/VFX/SupportHealRingVfx.cs` — support heal ring event.
- `Tutorial/RoguelikeTutorialDirector.cs` — dash eligibility gate.
- `Presentation/VFX/DetachedWeaponPresentationSystem.cs` — pool reset, visual count, lifecycle.
- `Presentation/VFX/DetachedWeaponCoreVisual.cs` — `OnDisable` reset.
- `Presentation/VFX/DetachedMissileBurstPool.cs`, `DetachedMissileLockPool.cs`, `DetachedLaserHitSparkPool.cs` — pool reset.
- `Gameplay/DetachedWeapons/DetachedWeaponSystem.cs` — `ClearAllWeapons()`.
- `Debug/Sandbox/SandboxDetachedWeaponRegressionService.cs`, `SandboxDetachedWeaponRegressionPanel.cs`.
- `Debug/Sandbox/SandboxWindow.cs` — regression panel in right column.
- `Editor/Validation/CoreRegressionTests.cs` — 4 new tests (18/18 total).

### Verification

- `Game.Shared.csproj`: 0 errors.
- `Game.Roguelike.csproj`: 0 errors.
- `Game.World.csproj`: 0 errors (read-only).
- `Assembly-CSharp.csproj`: 0 errors.
- `Assembly-CSharp-Editor.csproj`: 0 errors.
- Shared→Modes architecture scan: 0 violations (grep).
- Unity BatchMode Core Regression / Architecture menu: requires local Editor (project may be open elsewhere).
- Sandbox + Ring Arena Play Mode: manual steps below.
