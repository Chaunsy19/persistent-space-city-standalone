# Architecture investigation and recommendations

Initially inspected 2026-11-11 against SS14 `9ccfa8d118307aced564fa824d6ad5b9e217847a`, RobustToolbox `d56b0d0bcf94c76bef7fc18625bf226607dca092`. Paths are repository-relative unless prefixed with `RobustToolbox/`. The investigation below records the Milestone 0 baseline; the implemented Milestone 1 and 2.1 extensions are described at the end. Future designs remain recommendations.

## Repository and dependency map

| Location | Responsibility |
| --- | --- |
| `SpaceStation14.slnx`, `global.json` | Full solution and SDK selection: 10.0.100 with `latestFeature` roll-forward |
| `Content.Server` | Authoritative simulation, NPC planning, rounds, station jobs, database services |
| `Content.Shared` | Shared ECS types and mechanics, replicated state, prediction and validated interactions |
| `Content.Client` | Rendering, input, UI, presentation and client prediction |
| `Content.Server.Database`, `Content.Shared.Database` | EF Core database models/migrations and shared database types |
| `Resources/Prototypes`, `Resources/Maps`, `Resources/Locale` | YAML behavior/entity/job/recipe definitions, maps and localization |
| `Content.Tests`, `Content.IntegrationTests`, `Content.Benchmarks` | Unit, simulation integration and performance test infrastructure |
| `RobustToolbox` | Pinned engine submodule: ECS, networking, physics, maps, serialization and client platform dependencies |
| `MSBuild`, `Directory.Packages.props`, `nuget.config`, `BuildChecker` | Build configuration, package versions/feeds and Git/submodule setup |

Git and Python are setup dependencies; .NET compiles/runs C#. NuGet and the pinned engine supply managed/native dependencies. No separate database server was necessary for this local SQLite launch. A usable graphics/audio environment is needed for a graphical client.

## Recommended custom organization

Use folders inside the existing assemblies first; do not add a module loader, separate framework, empty components, or new projects now.

| Proposed location | Future responsibility |
| --- | --- |
| `Content.Server/_SpaceCity/Citizens` | Citizen lifecycle and decision coordination; HTN operators/services/preconditions |
| `Content.Server/_SpaceCity/Jobs` | Work discovery, reservations, schedules and profession task composition |
| `Content.Server/_SpaceCity/Needs` | Server-side decisions from hunger/thirst and future social/rest needs |
| `Content.Server/_SpaceCity/Persistence` | Snapshot orchestration, migrations and restart recovery |
| `Content.Server/_SpaceCity/Infrastructure` | Maintenance requests and expansion coordination |
| `Content.Server/_SpaceCity/Interactions` | Server validation and citizen service interactions |
| `Content.Shared/_SpaceCity` | Only cross-side components, state, events and interaction definitions actually needed |
| `Content.Client/_SpaceCity` | Citizen status and service presentation when needed |
| `Resources/Prototypes/_SpaceCity` | Custom entities, HTN compounds, utility queries, jobs and recipes |
| `Resources/Textures/_SpaceCity`, `Resources/Audio/_SpaceCity`, `Resources/Maps/_SpaceCity`, `Resources/Locale/en-US/_SpaceCity` | Custom assets/maps/localization, with licenses and attribution |

Use `Content.Server._SpaceCity.*` and matching shared/client namespaces, and a `SpaceCity` prefix for new prototype IDs to prevent collisions. Add components only for durable or system-owned state. Prefer events and existing public system methods; if a small upstream integration hook is required, isolate and document it.

Fork comparison: [Corvax server](https://github.com/space-syndicate/space-station-14/tree/master/Content.Server/Corvax) and [prototypes](https://github.com/space-syndicate/space-station-14/tree/master/Resources/Prototypes/Corvax) demonstrate fork folders in existing trees. [Goob Station](https://github.com/Goob-Station/Goob-Station) currently has separate `Content.Goobstation.Server`, `.Shared`, `.Client`, `.Common` and other projects, plus a module manager. Public directory listings were checked on the investigation date. These show two separation strategies; our small initial scope favors folders, avoiding additional assembly/module wiring. No code from those forks was copied.

## NPC architecture: existing capabilities

- `Content.Server/NPC/Components/NPCComponent.cs`, `HTN/HTNComponent.cs`, `Systems/NPCSystem.cs`: NPC lifecycle, server update limits, waking/sleeping, mob-state handling. Attaching a player sleeps AI; detaching does not awaken an NPC with an attached mind. Player takeover/release therefore needs an explicit policy later.
- `HTN/HTNSystem.cs`, `HTNPlanJob.cs`, compound/primitive tasks, preconditions and services: hierarchical decomposition, ordered branches, queued planning and replanning. `HTNComponent` references a root task; its plan/job/cancellation token are runtime state. The source itself contains a full-game-save TODO beside the plan.
- `NPCBlackboard.cs`, `NPCBlackboardSerializer.cs`, `Systems/NPCSystem.Blackboard.cs`: typed keyed context and entity-derived values; operators coordinate through target entity/coordinates/ranges. Do not treat a blackboard as the durable citizen database or retain stale entity references across restarts.
- `Systems/NPCUtilitySystem.cs`, `Queries`, `Resources/Prototypes/NPCs/utility_queries.yml`: candidate selection/scoring reused by HTN utility operators/services. This can find work targets; it does not supply a persistent labor allocation system.
- `Pathfinding/PathfindingSystem*.cs`, `PathFlags.cs`, `PathPortal.cs`, `Systems/NPCSteeringSystem*.cs`, shared movement systems: polygon/grid graph maintenance, A*/BFS path requests, steering and obstacle interactions. Door handling checks access and uses interaction activation; climbing, prying and smashing depend on capabilities/flags. Civilian tasks should not default to destructive traversal.
- `HTN/PrimitiveTasks/Operators/Interactions`: pickup (`EquipOperator` calls `SharedHandsSystem.TryPickup`, despite its name), drop, swap hands, use item, interact and alt-interact. `InteractWithOperator` invokes `SharedInteractionSystem.UserInteraction` and tracks `DoAfter` status. This is concrete evidence that NPCs can enter real player interaction paths.
- Combat operators and `NPCCombatSystem.*` handle melee/ranged actions, wielding and weapon-related steps. `Resources/Prototypes/NPCs` includes idle/follow/combat/nutrition, monkey, cleanbot, medibot, firebot and other behaviors.
- `cleanbot.yml` selects puddles, moves and interacts; `medibot.yml` selects a patient, moves, interacts and awaits a timed injection. Specialized bot components provide capabilities a humanoid does not automatically gain. `clothing.yml` is largely a TODO, not proof of general autonomous dressing.

Humanoids can reuse existing body/species prototypes, hands, inventories, access IDs and movement rather than a new citizen movement model. Appearance alone is insufficient: verify the selected humanoid prototype has the necessary movement/body/interaction components and valid faction configuration.

Department travel is primarily spatial navigation plus actual ID/access rules, not a department-aware commute planner. Workstation selection, safe door policy, disconnected grids/shuttles, congestion, unreachable goals, and paths invalidated by construction must be tested. Do not assume a path portal means arbitrary multi-grid commuting is solved.

### Recommended citizen decision flow

Citizen identity/job/needs/schedule data -> server chooses a bounded goal -> existing HTN decomposes it -> utility queries select reachable targets -> steering navigates -> real hands/inventory/interaction/tool systems execute -> observe result and complete, cancel or replan.

Add narrowly scoped operators only where existing ones lack the required action. Reserve shared tools/workstations/jobs with expiry and release on cancellation, death or deletion. Confirm actual outcomes: the current generic interaction operator can finish after dispatching an interaction without proving the intended workstation result. UI-bound machines may require a small actor-based server API shared with their UI handler; a network UI message tied to a player session is not a universal NPC API. Preserve range, access, power, item, cooldown and DoAfter validation; never emulate success by spawning products or directly editing machine output state.

### Performance and multiplayer

The current HTN queue has a 0.004-second processing budget and default plan cooldown of 0.45 seconds; `NPCSystem` uses `NPCMaxUpdates`. Pathfinding uses a 3 ms time slice, a 256-request per-tick cap and parallel processing. These are source constants, not measured city capacity guarantees. Budget includes graph changes, perception/utility queries, path requests and simulation, not just HTN.

Stagger schedules/replanning, bound search radii and candidate counts, use events to invalidate work, and measure tick time, queue length, cancellations, active NPC counts and allocations under population growth. Do not mutate ECS state from arbitrary worker threads. Verify operation with no players; player PVS visibility must not define city existence. Any future simulation sleeping must preserve explicit needs/work semantics.

Keep planning and authoritative world changes server-side. Replicate only required visible state using existing network components; keep blackboards/plans/reservations server-owned. Validate all player requests, use network entity identifiers for messages rather than raw runtime IDs, and respect existing prediction to avoid duplicate actions. Test two clients, late join, disconnect and eventual restart; a one-client smoke test does not establish these guarantees.

## Professions and reuse

Jobs assign permissions/equipment and player roles; a job prototype is not an autonomous work routine. Inspect `Content.Shared/Roles/JobPrototype.cs`, `StartingGearPrototype.cs`, `Content.Server/Station/Systems/ServerStationJobsSystem*.cs`, `Content.Shared/Station/Systems/StationSpawningSystem.cs`, `StationJobsSystem.cs`, and `Content.Server/GameTicking/ServerGameTicker.Spawning.cs`.

`StationSpawningSystem` exposes gear/loadout and character spawn paths, sets PDA/ID job data and delegates access through `SharedAccessSystem`. Reuse the actor/equipment parts with suitable profile data; keep citizen employment separate from player session preferences, bans, job-slot accounting and mind ownership. Verify how NPC staffing should count toward station jobs before changing assignment rules.

| Work | Existing mechanics/source | Missing citizen behavior |
| --- | --- | --- |
| Access/clothing | `Content.Shared/Access/Systems/AccessReaderSystem.cs`, `StationSpawningSystem`, shared inventory/hands/storage systems; `Resources/Prototypes/Roles/Jobs`, `Loadouts` | Acquire/wear appropriate IDs/tools; maintain inventory and access-aware route policy |
| Cooking | `Content.Shared/Kitchen/EntitySystems/MicrowaveSystem.*`, `Content.Server/Kitchen`, recipe prototypes | Recipe/ingredient selection, transport, appliance reservation, loading/cooking/retrieval; use real ingredients and API checks; recover from failure |
| Cleaning | `Content.Shared/Fluids/SharedAbsorbentSystem.cs`, server `Fluids/EntitySystems/AbsorbentSystem.cs`, puddle systems; cleanbot HTN | Humanoid mop/bucket use, refill/wring/disposal workflow, safe reachable work discovery |
| Medicine | Shared `Medical/Healing/HealingSystem.cs`, defibrillator and analyzer systems; chemistry/solutions; `Silicons/Bots/MedibotSystem.cs` | Triage, supply retrieval, correct treatment/dose, patient movement and interruption rules; no assumption medibot injection transfers directly |
| Engineering | Shared `Repairable/RepairableSystem.cs`, `Tools/Systems/WeldableSystem.cs`; server `Construction`, `Power`, `Atmos` | Fault detection, tool/material selection, safe repairs and maintenance priorities; construction graph progression through actual tools |
| Cargo | Server `Cargo`, shared storage/hands/pulling, existing cargo consoles and delivery mechanics | Hauling/order assignment, destination selection and resource accounting; session-bound consoles need API audit |
| Security | Server NPC combat, shared cuffs/restraints, access, criminal records and security interactions | Incident selection, identification, proportional escalation, custody and player interaction policy |

Source investigation establishes reuse opportunities; it does not establish end-to-end autonomous operation for any profession. Add integration tests per specific workflow as it is implemented.

## Persistence findings

`Content.Server/GameTicking/ServerGameTicker.RoundFlow.cs` drives restart. `ResettingCleanup()` moves players to lobby, broadcasts `RoundRestartCleanupEvent`, calls `EntityManager.FlushEntities()` and clears map/rule/session state. Traditional round lifecycle is a direct obstacle to continuous cities. Suppressing one restart trigger alone would not implement persistence.

Engine `Robust.Shared/EntitySerialization/Systems/MapLoaderSystem.Save.cs` exposes `TrySaveMap`, `TrySaveGrid`, `TrySaveEntity` and recursive serialization; corresponding load files restore entities. `EntitySerializer.cs` builds a YAML UID mapping and handles references, including auto-inclusion. `Options.cs` defines missing-reference behavior (default `IncludeNullspace`), exception behavior, map initialization/pausing, YAML IDs and map assignment. These are useful primitives, not a proven full live-world checkpoint system. Map serialization covers serialized component data, not every transient field or external service.

`Content.Server.Database/Model.cs`, `ModelSqlite.cs`, `ModelPostgres.cs` and migrations provide EF Core SQLite/PostgreSQL support. Server database services are under `Content.Server/Database`. Existing tables include player preferences/profiles, accounts, rounds, bans, playtime and admin records. A saved character preference profile is not its living body/inventory/health; no general city snapshot was identified in this investigation.

### Recommended future save design (Milestone 6)

1. Define a versioned snapshot manifest containing content/engine revisions, schema version, world identity, simulation time and a generation ID. Choose an explicit map/grid/entity closure, including loose runtime-generated items and required nullspace references. Audit serialized fields for each supported system rather than promising all state is automatically covered.
2. On the simulation thread, reach a controlled save boundary and pause relevant activity; serialize a consistent world through existing engine APIs. Do not serialize mutable ECS state concurrently with live mutations. Write snapshot files and citizen/account metadata to a new generation; atomically publish the manifest only when the generation is complete. Retain previous good generations and test interrupted writes.
3. Use stable project-owned citizen/character IDs for long-lived identity and account-to-body association. Let the engine remap entity references within a snapshot; never use raw `EntityUid`, `NetEntity`, session handles or map IDs as durable database keys. Resolve cross-snapshot/external references through stable IDs and explicit missing-target handling. Include inventories/containers, grid parents, machinery links and station association in reference audits.
4. Store custom identity, jobs, schedules and world bookkeeping in versioned project tables or snapshot metadata. SQLite is sufficient for initial single-server development; PostgreSQL remains an option using existing infrastructure. Bind metadata and entity files by generation to avoid split snapshots. Keep account credentials/admin data separate and protected.
5. On boot, select and validate a complete compatible generation before spawning a fresh station. Load paused, remap/fix references, restore citizen/account associations and initialize systems in a controlled order. Guard MapInit/roundstart spawn and supply logic against duplicate bodies, inventories, machinery and resources. Rebuild derived power/atmos/navigation graphs and validate their restored behavior.
6. Resume by replanning HTN goals, rebuilding reservations/caches and explicitly cancelling/recovering in-flight DoAfters; do not serialize executable plans/jobs or network sessions. Define timer rebasing and offline progression policy (initial recommendation: no offline simulation) and test health, needs, reagent quantities, atmosphere, power and delayed operations individually.
7. Add an explicit persistent-world lifecycle coordinated with the game ticker and player mind/session restoration. Save before intentional teardown and provide a recoverable restart path. Use minimal content integration hooks first; only consider engine changes after proving an API gap.

Required round-trip fixtures: constructed tiles/walls; powered/connected machinery; nested bags and equipment; loose/generated items; identity and job/schedule; damage/reagents/needs; player reconnect to the same body; stack/fluid/food quantities; cross-grid references; deleted/missing entities; schema upgrades; process failure during save. Report unsupported transient state clearly. Long-running simulation, resource depletion, entity growth, tick budgets, upstream merges and snapshot migration are major project risks.

## External references

- [Official development setup](https://docs.spacestation14.com/en/general-development/setup/setting-up-a-development-environment.html)
- [Official codebase organization](https://docs.spacestation14.com/en/general-development/codebase-info/codebase-organization.html)
- [Pinned SS14 source](https://github.com/space-wizards/space-station-14/tree/9ccfa8d118307aced564fa824d6ad5b9e217847a)
- [Pinned RobustToolbox serializer](https://github.com/space-wizards/RobustToolbox/tree/d56b0d0bcf94c76bef7fc18625bf226607dca092/Robust.Shared/EntitySerialization)

Local source at these revisions is the primary basis of this investigation. Recheck paths/APIs when updating upstream.

## Milestone 1 implementation

`Resources/Prototypes/_SpaceCity/citizen.yml` defines `SpaceCityCitizen` (inherits `MobCivilian` -> `MobHuman`), normal randomized human appearance, civilian clothes/passenger ID, `SpaceCityDestination`, a real crowbar-derived test item, and the root HTN compound. No upstream prototypes or engine source were changed.

`Content.Server/_SpaceCity/Citizens` contains a server-only component, system, HTN operator and debug admin command. The existing HTN scheduler executes one small delivery operator. Its per-entity state selects explicitly assigned tasks from a bounded queue, approaches an item through `NPCSteeringSystem`, checks range/line of sight/container accessibility, calls `SharedHandsSystem.TryPickupAnyHand`, confirms actual hand ownership, navigates to a destination, calls the normal `TryDrop` and verifies release. The operator stays active while idle; HTN replanning is disabled for this fixed demo. No new planner, pathfinder, network messages, physics or inventory model was added.

This narrow coordinator is the bridge around upstream steering/hands APIs: the generic pickup operator alone does not enforce interaction reach, and generic operators do not provide this demo's task-level deadline, postcondition reporting and bounded recovery. Steering still obtains its paths from the original engine-integrated graph/pathfinding systems. `NavInteract` is enabled; prying, smashing and climbing are not enabled. Door permissions are checked by existing access/door systems against actual carried/worn access items, not a citizen-only access flag.

Tasks are explicit entity references on one grid, with at most eight waiting tasks. The coordinator runs substantive work every 0.25 seconds through HTN rather than adding a global entity scan/update loop. Each navigation leg allows two attempts by default (clamped to at most three), with a two-second retry delay, six-second no-progress guard and thirty-second absolute leg deadline. The latter also bounds moving targets and repeated path invalidation. Exhausted/invalid tasks are abandoned once, followed by a two-second idle interval and selection of the next queued task. Engine steering performs its own bounded path retries inside each leg.

All planning/debug state remains server-only. Existing transforms, hands containers, doors, inventory and sprites replicate normally. HTN shutdown/player attachment/incapacitation cancels the demo and attempts normal task-item storage/drop cleanup; a deliberate admin reset resumes assignment when the underlying NPC is awake. Component shutdown unregisters steering (cancelling outstanding path work) and releases task references. Logs cover state transitions/failures, not every update.

`Content.IntegrationTests/Tests/_SpaceCity/CitizenTests.cs` exercises real HTN, navmesh, physics, access, doors and hands, including zero-player operation and two independent client replicas attached to one server. The fixture explicitly enables pathfinding, which the normal test pool disables. Power-independent test airlocks and inherent gravity isolate AI/access behavior from power supply and gravity generators; real powered-airlock failure remains subject to normal simulation. See `MILESTONE_1.md` for commands, evidence and limits.


Milestone 1 cleanup follow-up: `SpaceCityCitizenSystem.PutAwayTaskItem` runs once on explicit stop/reset or failed-task abandonment, before task references are cleared. It uses `InventorySystem.TryEquip` for pockets, then checks directly worn `StorageComponent` containers through `SharedStorageSystem.CanInsert`/`PlayerInsertEntityInWorld`, with hands-removal, actor-interaction and container-access checks. The fallback is the normal `SharedHandsSystem.TryDrop`. No force flags, direct inventory/container manipulation, nested bag search or per-frame scan were added. Only the held active-task item is eligible; unrelated hand contents are preserved. If normal removal/drop rules block cleanup, the item remains held and the result explains why. Component deletion still delegates entity/item teardown to the engine. Five additional real-simulation cleanup cases pass alongside the original five.


## Milestone 2.1 implementation

`Content.Server/_SpaceCity/Work` adds a small server-owned delivery board, order/autonomy components, admin interfaces and a separate goal selector. Selection chooses what to do; the original HTN executor remains the only movement/pickup/drop implementation. Autonomy is opt-in. Available work is indexed by descending priority (1..3), then creation sequence. Idle enabled active HTN citizens check at most once per two seconds; manual execution/queues and active automatic claims block new selection. No priority preemption or wandering is added.

Orders are nullspace ECS entities with session-local network IDs and retained terminal diagnostics. A synchronous claim sets one citizen owner before executor submission; open-item and citizen-claim indexes prevent duplicate open deliveries/claims. Execution publishes correlated outcome events after cleanup. Explicit validated manual assignments publish a precedence event before enqueueing, cancelling only the current automatic work. Stop/reset, disable, deletion and round teardown release or finalize ownership. The executor forwards citizen shutdown through a distinct event because engine lifecycle subscriptions are exclusive per component/event pair.

The board checks only nonterminal target validity at 1 Hz and keeps at most 512 records including terminal history per round. Failed orders are terminal after the executor's existing bounded attempts, never endlessly reoffered; operators can fix the cause and post a new order. Item cleanup still uses the Milestone 1 normal pocket/worn-storage/ground fallback. No full-world item search, extra movement framework, new shared network messages, engine changes or persistence are added. A manual order involving an automatically reserved item can invalidate that automatic order; ordinary item availability checks handle it safely. See `MILESTONE_2_1.md` for lifecycle, commands, acceptance steps, complexity and limitations.
