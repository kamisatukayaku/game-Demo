# Ring Arena / Rogue Full Code Audit

Date: 2026-07-01

## 1. Scope and method

This repository is a Unity 2022 C# project, not an Unreal Engine 5 project. The audit maps the requested UE concepts to Unity equivalents: asmdef, MonoBehaviour lifecycle, Scene, prefab, ScriptableObject/JSON, URP, ParticleSystem, LineRenderer and runtime-created UI.

Reviewed scope:

- All 98 files under `D:/game/docs`, including generated HTML/JS inventories, backlog JSON, design/data/dev documents and helper scripts.
- All 173 Shared C# files (about 28.6k lines).
- All 223 Roguelike C# files (about 37.3k lines).
- All JSON under `D:/game/data` and `Assets/Resources/Data`.
- Scene/prefab script references, asmdef dependencies, bootstrap paths and current Unity Editor log.
- Build verification through `dotnet build Assembly-CSharp.csproj --no-restore`.

World was inspected only to understand dependencies and documentation. It was not modified.

Limitations:

- Static analysis cannot prove visual timing, collision feel, late-wave frame time or every Feature playtest condition.
- The Unity Editor had exited and its latest log ended at 01:50. Historical runtime exceptions were inspected, but a fresh 20-wave Play Mode run and Profiler capture were not available.
- Unity assets can be loaded by string, generated at runtime or referenced through serialized GUIDs. No asset was deleted solely because a text search found no reference.

## 2. Overall health

Score: **6.1 / 10**

Strengths:

- Shared/Roguelike asmdef direction is documented and mostly reflected in folder ownership.
- Damage, health, enemy AI, progression, presentation and event responsibilities are recognizable.
- Roguelike JSON is syntactically valid: 90 checked JSON files, 0 parse failures.
- Six detached-weapon evolution files each contain five tiers and an Lv3 Contact prerequisite.
- All 108 `requires_ids` references found in Roguelike JSON resolve to an existing ID.
- Numeric caps exist for crit chance, mechanic slots and area soft caps.
- Upgrade offers filter previously selected IDs and enforce mechanic-slot limits.
- Current C# source compiles with 0 errors.

Main weaknesses:

- Documentation has multiple incompatible generations and no reliable current source of truth.
- Build-time Resources data is stale and the sync tool does not support the current nested data layout.
- One Shared class still directly references Roguelike, violating the asmdef boundary.
- Runtime-created GameObjects and Destroy calls remain common in projectile/VFX hot paths.
- Several core classes are 900-1430 lines and combine orchestration, data parsing, state and presentation.
- Generated project files contain duplicate compile entries and a clean build emits about 226 warnings.
- Feature status is self-reported as Completed without automated acceptance tests.

## 3. Documentation audit

### P0: conflicting design authorities

`docs/README.md` declares `design/design.md` authoritative, but that file still defines equipment/skill/aura routes and melee/ranged/reflect themes. Newer architecture/backlog documents define Mage/Ranged/Contact/Support build identities, detached weapons, corruption, relics, hazards and a 20-wave arc. Current user-approved design additionally removed traditional classes in favor of starter choices.

Impact: a reviewer can "correct" current code back to obsolete gameplay while believing they are following the official design.

Required decision:

1. Mark old design/data pages as historical, or update them to the current no-class build system.
2. Add `design_version`, `status` and `supersedes` metadata to design documents.
3. Make one current design index authoritative and make backlog Features reference its version.

### P1: generated documentation is stale

`code_tree_data.js`, `src-UNITY_SCRIPTS.md`, `roguelike-module-README.md` and the current source disagree about paths, file counts and removed systems. The generated code tree contains old Equipment and old Build paths.

Impact: dead-code and ownership decisions based on these documents are unsafe.

### P1: feature state is not evidence

All 53 S/A/B/C Feature JSON files currently say `Completed`. Their 153 `possible_files` references mostly resolve, but A18 points to a non-existent Roguelike `UI/StreamModeSettings.cs`; the implementation actually lives in Shared UI. This is documentation drift rather than a missing runtime class.

Recommendation: require each Completed Feature to store a test ID, tested revision, date and evidence screenshot/log.

## 4. Architecture and dependency graph

```text
StartGameUIShared
  -> RoguelikeGameMode
  -> GameSessionConfig
  -> RoguelikeRunSessionConfigurator
  -> ArenaBuildBootstrap / RunBuildState

CombatRoot
  -> RoguelikeCombatSceneBootstrap
  -> Player + HUD + WaveDirector + Loot + progression + presentation

WaveDirector
  -> Shared EnemySpawner
  -> EnemyCore / EnemyAttack / BossCore
  -> DamagePipeline -> Health -> CombatEventBus / GameEventBus

EnemyKilledEvent
  -> LootManager -> XpPickup -> ExperienceSystem
  -> KillStreak / ecosystem / story / rewards / VFX

LevelUpEvent
  -> LevelUpController -> LevelUpCeremonyUI
  -> LevelUpChoiceDatabase -> RunBuildState.ApplyChoice
  -> RunBuildApplier -> player stats / archetypes / detached weapons
```

Intended dependency direction:

```text
Game.Roguelike -> Game.Shared
Game.World     -> Game.Shared
Game.Shared    -X-> Game.Roguelike / Game.World
```

Violation still present:

- `Shared/Audio/ArenaBgmController.cs` imports `Game.Modes.Roguelike.Combat` and accesses `WaveDirector` and `CircleArenaController`.

The Final Boss direct dependency on Roguelike was removed during this audit; phase-2 quadrant behavior is now driven by `BossPhaseChangedEvent` in the Roguelike listener.

Recommended BGM fix: move `ArenaBgmController` into Roguelike Presentation/Audio or add mode-neutral wave-state events/query interfaces. Do not add a Roguelike assembly reference to Shared.

## 5. Lifecycle and event audit

### Fixed: unstable EventListenerHandle

The event handle stored a mutable list index while unsubscribe used swap-remove. Removing one listener changed another listener's index without updating its copied handle. Later unsubscribe could remove the wrong listener.

Impact before fix: intermittent missing/duplicate UI, VFX, loot or progression reactions after object destruction and scene changes.

Fix: handles now use a monotonically assigned stable ID and unsubscribe searches by ID.

Residual risk: callbacks may unsubscribe other callbacks during Publish. Current reverse iteration is usually safe for self-unsubscribe but should receive dedicated mutation tests.

### P1: too many implicit bootstrap entry points

The docs call `CombatRoot` the unique RuntimeInitialize entry, but nine files contain runtime initialization hooks. Several are legitimate registrations, yet order is implicit and split across Shared/Roguelike.

Recommendation: retain registration hooks only, route scene initialization through one ordered bootstrap manifest, and add an initialization trace test.

### P1: DontDestroyOnLoad ownership is broad

Audio, cursor, input UI, VFX pools, loot, progression and combat feedback create persistent objects independently. Singleton guards reduce duplication, but run reset ownership is not centralized.

Recommendation: define AppLifetime, ModeLifetime and RunLifetime roots. Every persistent service must declare one owner and one reset method.

## 6. Data and configuration audit

### P0: build data mirror is stale

The source data is now nested (`combat/`, `roguelike/combat/`, `roguelike/weapons/`, etc.). `DataSyncMenu.SyncJsonData()` only copies top-level JSON and the old `structures` directory. It therefore does not package most current data.

Observed:

- 63 source JSON files have no same relative-path Resources mirror.
- Several flattened mirrors differ from source, including attacks, enemies, AI, mage/ranged upgrades, skills and themes.
- Old Resources-only files remain: melee/player upgrades and warrior progression.
- Editor play can read external `D:/game/data`, hiding the defect.
- A player build relies on Resources and can silently use stale data or fail to load nested databases.

Required fix outside the currently allowed script scope:

1. Make DataSync recursively copy `data/**.json` while preserving relative directories.
2. Remove obsolete mirrors only after loader references are validated.
3. Run sync before CI compile/build and fail if source/mirror hashes differ.
4. Add a player-build smoke test that loads every registered database.

### P1: multiple loaders use inconsistent paths

Most new databases use `JsonDataLoader`, while LootManager/LootService and older Shared databases still have custom file/Resources fallback logic. This creates Editor/Build divergence and duplicate parsing DTOs.

Recommendation: one typed data registry, one path policy, schema version validation and explicit load failure reporting.

### Verified growth constraints

- Mechanic slots: base 3, max 6.
- Crit chance cap: 75%.
- Area uses a soft cap.
- Existing selection IDs are excluded from later offers.
- Build tag stacks raise related offer weights.
- Ranged projectile tracks are mutually restricted.

Design conflict requiring confirmation: code contains four starter choices, while an earlier requirement specified the first level-up should offer only Mage or Shooter. Current backlog uses four Build identities. No gameplay change was made.

## 7. Player audit

Implemented:

- Movement, camera follow, dash, damage/health, death state, auto attack, active skills, XP and level-up integration.
- Death keeps the player object, disables gameplay, plays VFX and opens failure UI.

Risks:

- Player startup is split between CombatRoot, MainSceneStartupFix and RoguelikeCombatSceneBootstrap.
- MainSceneStartupFix performs global SpriteRenderer scans and forcibly changes URP camera settings after scene load.
- Player visual ownership has historically caused null references and scale conflicts; the current code includes multiple repair paths rather than one construction contract.

Historical Editor log exceptions:

- ArenaTutorialController.BuildUi.
- PlayerStateMachine.SpawnDeathVfx.

Current source has changed at both locations and no current compile failure remains. Fresh Play Mode validation is still required.

## 8. Weapon, ability and detached-weapon audit

Implemented:

- Mage/ranged automatic attack infrastructure.
- Detached Contact, Laser, Missile, Explosion, Pulse, Trail and Boomerang runtime/presentation code.
- Evolution JSON: six routes, five mechanism tiers each.
- Detached presentation uses distinct route colors and route-specific VFX classes.

High risks:

- `DetachedWeaponPresentationSystem.cs` is 1431 lines and contains multiple visual types and construction paths. Earlier null-reference defects in Missile/Boomerang came from this ownership complexity.
- `LevelUpChoiceDatabase.cs` is 1235 lines and combines loading, compatibility, filtering, weighting, build gates and DTOs.
- `PlayerAttackDirector.cs` is 971 lines and owns targeting, delivery selection, cooldowns and build hooks.
- Straight/Homing projectiles still call Destroy on hot-path completion. Enemy projectile and ranged VFX creation is not consistently pooled.

Recommendation: split without changing behavior:

1. DetachedWeaponPresenter registry per attack mode.
2. OfferSource + EligibilityRules + WeightPolicy + JSON DTO loader.
3. Projectile lifecycle interface backed by pools.

## 9. Monster and Boss audit

Implemented:

- Shared chase/attack core, charge, ranged, laser, BossCore and multiple concrete Boss skills.
- Roguelike ecosystem profiles, elite affixes, support behavior, split/bomber/disruptor roles and wave combinations.
- Boss phase events and Arena-specific listeners.

Findings:

- Shared EnemyCore is 1139 lines and EnemyAttack is 791 lines; AI state, attack execution, compatibility fields and visual coordination are too concentrated.
- Several serialized EnemyCore projectile/charge fields are assigned but no longer read, indicating incomplete migration.
- FinalBossPrismNexus previously called Roguelike UI/combat directly. Fixed by event-driven Arena handling.
- Monster documentation still says only charge/barrage/laser are authoritative, contradicting the ecosystem code.

Boss exclusion for generic hit reactions appears intentional in presentation code, but must be confirmed in a Boss damage playtest.

## 10. UI audit

Implemented:

- Main menu/mode flow, HUD, wave UI, level-up ceremony, death/victory UI, story/timeline/share card, achievements, relic/corruption picks and build feedback.

Risks:

- `StartGameUIShared.cs` is 1315 lines with seven update-style methods and runtime construction of the whole UI.
- GrowthPreviewUI and LevelUpCeremonyUI are similarly large and mix layout, animation, input and content policy.
- Runtime font/material/sprite fallback paths can hide missing assets and create inconsistent build visuals.

Safe fix: corrected the route display typo `属怀` to `属性`.

## 11. Save and settings audit

### Fixed: reset controls erased all progression

`GameInputBindings.ResetToDefaults()` called `PlayerPrefs.DeleteAll()`. This erased controls plus Arena shards, total runs, victories, best wave, build/evolution unlocks, audio and Stream mode settings.

Fix: delete only each input binding key and save.

Remaining risk:

- Meta progress is spread across raw PlayerPrefs keys with no version, migration, checksum or atomic save object.
- There is no explicit future run-reset/meta-reset boundary.

Recommendation: versioned SaveData DTO with separate SettingsData and MetaProgressData stores.

## 12. Performance audit

### Severe

- Build-time data mismatch can disable systems or load old balance, which is a correctness failure before it is a performance issue.

### High

- 118 Update methods across 88 files.
- 414 `new GameObject` sites across 114 files.
- 233 Destroy sites across 109 files.
- Projectile completion and several high-frequency VFX still use Destroy.
- `RangedProjectileVfx.cs` has seven update loops and many persistent runtime roots.

### Medium

- 53 GameObject.Find/FindWithTag sites across 38 files.
- 487 GetComponent sites across 143 files.
- 45 runtime Material construction sites.
- Several large MonoBehaviours perform polling instead of event-driven updates.

### Low

- Most UI GameObject creation happens once during panel construction.
- EnemyRegistry already replaces many global enemy searches.
- Several expensive effects already use pools and bounded capacities.

Profiler acceptance recommendation:

- Capture W1, W10, W20 and 100 simultaneous deaths.
- Record Main Thread, Scripts, Physics2D, Rendering, GC Alloc and object counts.
- Target zero steady-state GC allocation during ordinary combat; permit bounded allocations only at wave/upgrade transitions.

## 13. Dead and obsolete code

Confirmed empty placeholders: 14 `*Layout.cs` classes under Build/Data, Build/Runtime, Combat/{Arena,Core,Scaling}, Features, Gameplay subfolders, Progression and Runtime.

They have no references and no runtime behavior. They were not deleted because architecture docs explicitly describe them as migration placeholders; deleting them should be paired with a documentation decision.

Other probable obsolete surfaces requiring reference-safe cleanup:

- old Aura/Reflect compatibility code;
- old warrior progression database and bounce accessor;
- Resources-only melee/player/warrior JSON mirrors;
- stale generated code tree;
- legacy custom loaders and duplicate loot DTOs.

No Blueprint/Niagara equivalent asset was deleted. Unity string/resource/GUID references require an AssetDatabase dependency scan before deletion.

## 14. Build and warning status

Current build result: **0 errors, about 226 warnings** on a clean compile.

Important warning groups:

- `Assembly-CSharp.csproj` contains duplicate Compile entries for ArenaLayoutDatabase and BuildMilestoneDatabase.
- JSON DTO fields produce many CS0649 warnings; these are expected for Unity JsonUtility but should be isolated with targeted warning suppression or converted to serialized DTO properties supported by the chosen parser.
- Obsolete Warrior reflect/bounce access remains.
- EnemyCore has assigned-but-unused attack fields.

The earlier incremental build reporting zero warnings was not representative; clean compilation exposes the warning baseline.

## 15. Automatic fixes made

1. Stable GameEventBus listener IDs.
   - Reason: prevent wrong-listener removal after list mutation.
   - Impact: all typed gameplay event subscribers.
   - Risk: low; API shape is unchanged.

2. Final Boss dependency inversion.
   - Reason: remove Shared -> Roguelike references from FinalBossPrismNexus.
   - Impact: Prism Nexus phase 2 and ArenaQuadrantBlocker.
   - Risk: low; same quadrant lock/banner/flash is triggered from the existing phase event.

3. Input reset data-loss fix.
   - Reason: restore-default controls must not erase meta progression/settings.
   - Impact: settings UI and PlayerPrefs.
   - Risk: low.

4. Removed duplicate using and corrected `属性` UI text.
   - Risk: none.

## 16. Feature status summary

Documented Completed: S1-S12, A1-A18, B1-B15, C1-C8.

Static implementation confidence:

- High: files/data and primary lifecycle entry found.
- Medium: most presentation and mid-wave features; behavior exists but playtest criteria are not automated.
- Low until playtest: 20-wave movie arc, dual Boss, quadrant lock, hazard rotations, dynamic BGM, share/codex/achievement persistence and 100-enemy performance.

Unimplemented according to current backlog metadata: none.

Incorrect/ambiguous according to design baseline:

- starter choice count and meaning;
- old three-route design versus current Build identities;
- old three-monster-mode authority versus ecosystem roles;
- removed Aura/Reflect versus retained compatibility code;
- A18 documented file path;
- build data mirror behavior.

## 17. Priority plan

### P0 - immediately

1. Fix recursive data sync and add hash validation for player builds.
2. Move ArenaBgmController out of Shared or replace Roguelike references with mode-neutral events.
3. Declare the current design authority and mark old docs historical.
4. Run fresh MainScene smoke test after Unity recompiles the asmdefs.

### P1 - current sprint

1. Add automated GameEventBus mutation/lifetime tests.
2. Add database load smoke tests for Editor and standalone player paths.
3. Pool Straight/WeakHoming/LockLoss projectiles and high-frequency ranged VFX.
4. Split the three largest God classes without changing behavior.
5. Add a 20-wave deterministic acceptance scene/test harness.
6. Eliminate duplicate project compile entries and establish a warning budget.

### P2 - subsequent

1. Centralize lifetime ownership and run reset.
2. Version Meta/Settings saves.
3. Regenerate code tree and module docs from source in CI.
4. Remove confirmed migration placeholders and legacy Aura/Reflect/Warrior surfaces.
5. Consolidate JSON loaders and DTO conventions.

### P3 - long term

1. Move runtime-generated complex UI toward prefabs or a declarative UI layer.
2. Establish performance budgets per wave and automated profiler regression captures.
3. Add content schema validation, migration and localization validation to CI.

## 18. Required manual playtest matrix

1. Main menu -> starter -> MainScene, no white screen and no Console Error.
2. Mage and Shooter auto attack immediately after starter selection.
3. Enemy contact/ranged/dash/laser attacks repeat after cooldown.
4. First level-up offer count and starter semantics match the chosen design authority.
5. Each detached evolution reaches Tier5 and visually/behaviorally changes.
6. Player death once only, input disabled, failure UI restart/menu work.
7. W8/W13/W18 mini-Boss, Hard W15 double Boss, W20 final Boss P2 quadrant lock.
8. Standalone development build loads the same hashes and balance as Editor.
9. Reset controls preserves shards, unlocks, achievements, audio and Stream mode.
10. W20 with large enemy/death/XP counts stays within frame and GC budgets.
