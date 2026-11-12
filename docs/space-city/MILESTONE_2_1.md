# Milestone 2.1 — autonomous goal selection

Branch: `codex/milestone-2.1`, based on verified Milestone 1 cleanup commit `750303832e2a6eca12c8885a41187ebc0f8cdbd0`. Canonical origin remains `Chaunsy19/persistent-space-city-standalone`; the older fork URL in the brief is historical. Needs, time, schedules, professions and persistence are outside this milestone.

## Architecture and lifecycle

`SpaceCityGoalSelectorSystem` decides **what** to do; the Milestone 1 HTN operator and `SpaceCityCitizenSystem` still implement **how** to navigate, respect doors, pick up, carry, deliver and recover. `SpaceCityAutonomyComponent` is server-only and disabled by default. Enable it explicitly. Only enabled, idle, active HTN citizens with no manual queue or selected order may claim work. Stopped citizens require `reset` before working again. Idle citizens stay still.

`SpaceCityWorkOrderSystem` owns a server-only delivery board. Each order is an entity with a session-local NetEntity ID, item/destination IDs, original grid, type, priority, creation sequence, status, current owner, last owner, assignment count and result. Nullspace order entities retain terminal diagnostics even after targets/maps disappear. These IDs are not durable persistence keys. Round restart clears the board and engine entity flush removes its records.

| Status | Meaning |
| --- | --- |
| Available | Valid unclaimed delivery, eligible for a citizen on the same grid |
| Claimed | Exclusively assigned to one citizen, queued or executing through HTN |
| Completed | Actual normal pickup and destination drop succeeded; never offered again |
| Failed | Target unavailable, executor failure, or citizen removed; reason retained, claim released |
| Cancelled | Explicit order cancellation, stop/reset, disabling autonomy, or valid manual override; claim released |

Priorities are **1 low, 2 normal, 3 high**. The available index sorts descending priority, then ascending creation sequence. Equal-priority orders are FIFO. Citizens do not abandon active work for a newly posted high-priority order. Selection/claim/submission occur synchronously on the authoritative simulation thread; another citizen cannot claim the same record. An item cannot have two open orders. Destination capacity and unrelated manual-item reservations are not modeled.

Executor outcome events resolve orders after execution state and carried-item cleanup have been handled. A validated manual task cancels only the current automatic assignment before joining the existing manual queue. Invalid manual commands do not preempt. Stop/reset retains its Milestone 1 meaning of clearing the citizen's work; disabling autonomy cancels automatic work and preserves manual work.

Failures use the executor's existing bounded attempts (default two per navigation leg), progress timeout, absolute deadline and retry delay. A failed work order is terminal: it is not repeatedly offered to other workers. Fix the underlying problem and explicitly create a new order. Existing cancellation cleanup tries normal pockets, worn storage, then ground drop, preserving unrelated items and all action/capacity checks. If all ordinary removal/drop checks reject cleanup, the task item remains held with an explanatory result. Items stored during cancellation must be placed loose on the floor before being offered again.

The board validates nonterminal targets once per second. The selector checks its component query once per second and rate-limits each idle citizen to one selection attempt per two seconds. It scans only the priority index, never every world item. Terminal orders are retained but not target-validated. The board has a hard limit of **512 retained orders per round**, including history; creation explains rejection at the limit. Complexity is bounded O(citizens × available orders) per selection interval plus O(retained orders) validation, suitable for this small milestone, not a measured hundreds-of-citizens scheduler. Creation, selection, claims and terminal claim release are logged, without idle/per-frame spam.

## Console commands

Use the server console or the client's admin console (grave/backtick). Commands require Debug admin permission on clients. Use numeric **network** entity IDs; F3 shows the attached entity NetID and VV also exposes IDs. A formatted `123/n456` means use `456`.

```text
spacecity spawn <anchor>
spacecity auto <citizen> on
spacecity auto <citizen> off
spacecity goal <citizen>
spacecity order create <item> <destination> [priority]
spacecity order list
spacecity order status <order>
spacecity order cancel <order>
```

Creation defaults to priority 2. `list` includes all retained records and their status; `status` reports targets, sequence, priority, assignee/last citizen, assignment count and reason. `goal` reports enabled state, selected order and decision. The original commands remain available and unchanged:

```text
spacecity task <citizen> <destination> [item]
spacecity status <citizen>
spacecity stop <citizen>
spacecity reset <citizen>
```

Notice that `task` takes **destination before item**; `order create` takes **item before destination**. Order creation never takes a citizen ID.

## Repeatable acceptance demonstration

Build first, then open separate PowerShell windows from the repository:

```powershell
dotnet build SpaceStation14.slnx --no-restore
.\run-space-city-server.ps1
# Another window:
.\run-space-city-client.ps1
# Optional second client, distinct username/data:
.\run-space-city-client.ps1 --username CityObserver --user-data-dir .space-city-local/client-observer
```

Keep the server bound to localhost with hub advertising disabled. Do not combine redirected stdout with an interactive pseudo-terminal: use the ordinary server console. Join the local development map and choose an open safe floor area on a grid. Avoid walls, vacuum, occupied workstations and inaccessible doors for this simple demonstration.

1. Run `spacecity spawn <your-anchor-NetID>` twice. Each prints citizen, item and destination IDs. Call them `C1/I1/D1` and `C2/I2/D2` in your notes; substitute the actual numbers below. If they overlap, move the anchors/spawn positions to adjacent clear floor areas.
2. Use F5 to spawn a third `SpaceCityTestItem` and `SpaceCityDestination` on clear floor and inspect their NetIDs (`I3/D3`). For a quicker helper-only setup, run spawn once more and leave its citizen's autonomy off; only the original two workers should be enabled.
3. Post `spacecity order create I1 D1 1`, `spacecity order create I2 D2 3`, and `spacecity order create I3 D3 2`. Posting before enabling workers makes priority selection easy to observe.
4. Run `spacecity auto C1 on` and `spacecity auto C2 on`. Inspect `order list`/`goal`: the two highest available priorities should be claimed first. Claim ownership is exclusive; each record's assignments stays 1.
5. Watch the physical pickups, movement and drops. Inspect both clients if connected. Wait for all three orders to become Completed, with current assignees cleared and results retained. Citizens return to idle without wandering. No `spacecity task` is used.
6. Create another loose item/marker pair (another spawn with its new citizen left disabled is convenient). Post a new delivery order. An idle enabled worker should independently select it on its next check and complete it. Reinspect statuses and confirm each order completed once.
7. Optional: cancel while carrying a pen, disable autonomy, issue a manual task, or remove a target. Confirm terminal history, released claims and the original safe item storage/drop behavior. Reset stopped citizens to resume selection.

For a demonstration with exactly two spawned humanoids, create the extra items/markers using F5 instead of additional spawn helpers. Only the two enabled citizens perform autonomous work either way.

## Automated verification

```powershell
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~Content.IntegrationTests.Tests._SpaceCity.CitizenTests
```

The fixture runs real HTN, navigation, physics, hands, inventory and two client replicas. Long work-selection cases use normal godmode/rejuvenation solely to isolate inherited suffocation in the floor-only vacuum test arena; runtime citizens retain normal human physiology. It explicitly enables navmesh generation because the common test harness disables it. Tests cover idle/resume, sequential priority/FIFO, nonpreemption, competing workers, duplicate-item rejection, bounded unreachable failure/recovery, cancellation/reset/disable/manual precedence, item/citizen/map deletion, and the existing Milestone 1 movement/access/storage suite. Test cleanup removes nullspace diagnostic records before returning the pooled world.

Exact executed build, test and live acceptance results are recorded in `DEVELOPMENT_LOG.md`. Local logs/TRX/runtime data are ignored and are not committed.

## Limits and next milestone

Only admin-posted item deliveries, loose targets on one grid, existing human access/physiology and ordinary inventory cleanup are supported. No item retrieval from storage, preemptive emergency scheduling, nearest-worker optimization, station demand/economy, durable identity, save/load, cross-grid routes or population capacity claim. A manual task using another order's item is allowed by the original interface; normal item-availability checks safely fail the conflicting order. Milestone 2.2 should introduce station time and schedules as another selection input while preserving explicit manual precedence, claim ownership and tested executor behavior; implementation requires separate approval.
