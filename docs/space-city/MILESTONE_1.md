# Milestone 1: autonomous humanoid test guide

Development branch: `codex/milestone-1`. Source remains the standalone repository; the older fork URL in the supplied milestone brief was superseded by the approved independence setup. No merge or Milestone 2 work is authorized.

## Spawn and assign

Build the solution, start `run-space-city-server.ps1`, then `run-space-city-client.ps1`. The default development setup grants local host admin. Commands below require the existing Debug admin permission (or server console). Open the in-game console with the grave/backtick key; F3 is the positioning debug overlay, not the console.

1. In a safe, breathable, open floor area, use F5 to spawn **Space City Citizen** (`SpaceCityCitizen`), **Space City destination** (`SpaceCityDestination`) and **Space City test item** (`SpaceCityTestItem`). They use existing artwork. The citizen starts idle.
2. Read each object's **network entity ID** in View Variables/admin inspection. In pretty debug notation `123/n456`, the network ID to enter is `456`, not the client-local `123` and not the literal prefix `n`. IDs vary every session.
3. Alternatively, `spacecity spawn <anchor>` creates an idle citizen, item and destination at offsets of 1, 2 and 4 meters from the anchor and prints all three network IDs. Use your character or a floor marker as anchor; inspect the offsets first because this command does not clear walls/machinery or make an unsafe area habitable.
4. Assign work using the printed/inspected numeric IDs:

```text
spacecity task <citizen> <destination> <item>
spacecity status <citizen>
```

Omit `<item>` for navigation only. Specific targets are the intentional task-discovery mechanism for this milestone; there is no world-wide job search. Queue up to eight additional tasks. The citizen picks one, executes it and returns to idle before selecting another. A completed delivery must leave the real item loose near the destination; a successful command response only means the task was queued. Use status to confirm completion.

```text
spacecity stop <citizen>
spacecity reset <citizen>
```

Stop clears current/queued work and steering. Reset clears work and resumes accepting fresh tasks; it does not resurrect, heal, wake a mind-controlled entity or recover a lost item. On cancellation/failure, the active task item is put into an available pocket or directly worn storage; if it cannot be stored, the citizen tries to drop it at its feet. If normal removal/drop checks reject both options, it remains held and the status explains this. Unrelated held items are untouched. Stopped citizens reject assignments until reset. Current activity, queue, attempts, counters and last result are in the component's VV fields and `spacecity status` output. The normal `npc` debug UI can inspect the HTN.

## Navigation and door checks

Place the destination beyond an ordinary obstacle; verify movement follows a viable route rather than passing through collision. For access checks, use a narrow corridor with an actual closed door and no alternate path. Configure that door/electronics through the normal admin/map tools, and equip the citizen with an ID that supplies the required access. The default passenger ID does not grant every department. A powered airlock must have working power; it will not be pried open by this citizen.

Run once authorized and once with denied access, a bolted door or a solid blockage. The denied route should be abandoned after bounded attempts, leaving the door closed. Queue a reachable target on the same side; it should run after the failed task. Defaults: 0.25-second coordinator interval, 6 seconds without net movement, 30 seconds maximum per leg, 2 attempts per leg with 2 seconds between attempts. VV exposes bounded timing/attempt settings for debugging.

Delete the destination or item while the task is active, then delete the citizen while moving. Verify a clear failure/idle result where the citizen survives, no continued path work for a deleted citizen, and no server exception. Try an unpickable/anchored/contained item: normal checks must reject it rather than placing it in a hand.

## Multiplayer and visual checks

Start a second client from another console:

```powershell
.\run-space-city-client.ps1 --username CityObserverTwo
```

Keep both clients near the same test citizen. On both views, observe movement/collision, the same door opening, the same item appearing in the citizen's hand, and its final drop. Check normal humanoid/clothing sprites and movement animation. Use ordinary player movement to avoid blocking the route accidentally. Disconnect both clients while a task runs and reconnect later; AI does not depend on a player mind/session. The automated fixture separately verifies decisions and delivery without connected players.

## Verification performed

- Full nonincremental solution build: **0 errors, 1,012 warnings**, 00:00:54.48. Milestone 0's original full build had 1,025 warnings. Normalized warning location/code/message comparison found **no new warnings**, including none from `_SpaceCity`; count differences alone are not claimed as fixes to upstream warnings.
- Five focused integration cases passed, none skipped: no-player delivery around a solid wall with observed real hand ownership and physical drop; authorized door traversal; denied door route with abandonment and a queued reachable recovery task; queue/stop/reset/deleted-target/deleted-citizen handling; two distinct clients receiving movement through a door, held-item state, final drop and clothing state.
- Runtime: local server reached Ready; two graphical clients reached InGame. `spacecity spawn` created visible normal humanoids. Live `spacecity task` completed a real pickup/delivery; status confirmed idle, completed=1, failed=0. Both graphical views were inspected. The two-client automated case samples transient states in both replicas, rather than claiming a single screenshot proves replication over time.
- Local evidence: `milestone1-full-rebuild.log`, `milestone1-integration-final.log`, `Content.IntegrationTests/TestResults/milestone1-citizen.trx`, and live client/observation logs. Generated evidence/data is ignored by Git.

## Limits and known issues

- Fixed assigned targets and a bounded demo queue; no professions, resource/workstation reservations, citizen needs/schedules or persistence. Tasks do not survive restart.
- Targets must be on one grid and outside containers. Multi-grid commuting, elevators/shuttles and inventory retrieval from bags are not implemented. Moving targets remain bounded by the leg deadline.
- Crowbar delivery demonstrates pickup/carry/drop, not arbitrary workstation operation, equipping clothing or tool use. Range, line of sight and container accessibility are checked before the normal hands API. No items are teleported into inventories.
- Stop/failure puts away the active task item or drops it, subject to normal checks; a physically unremovable item may remain held with an explicit status. Theft, invalid targets and ordinary interaction rejection fail the task; they do not conjure replacements. Competing citizens are allowed to fail rather than sharing a new reservation framework.
- A partial route can bring the citizen near a denied door before abandonment. The original engine may issue up to three path requests per steering attempt. Doors still follow real power, bolts and access rules; no destructive traversal is enabled.
- HTN suspension due to player attachment or incapacitation stops the demo. Autonomous return after player takeover is not implemented. Reset only affects demo state.
- The no-progress guard measures net displacement; slow/crowded valid routes can time out. Timings are bounded/debug-configurable. The absolute leg deadline prevents endless circling/replanning. Large-population and long-duration performance were not benchmarked.
- The automation harness initially crashed the server console by combining a pseudo-terminal's stdin with redirected stdout (`System.Console.WindowWidth`: invalid handle). A normal interactive console without redirection and ordinary noninteractive launch both worked; no engine workaround was added. Existing loading catch-up/Discord IPC/SQLite historical migration warnings remain environmental/upstream issues.
- The live two-client session also logged late `MsgEntity`/collision-message warnings under the existing Debug fake-lag configuration. The automated replica assertions and live task completion passed; latency/load/soak behavior beyond these checks remains unverified.

Recommended next milestone, after approval: introduce a small citizen identity/need/schedule layer that selects goals for this execution path, with cancellation and zero-player tests. Do not start with full professions or persistence.


## Follow-up: canceled task item cleanup

Canceling/resetting a task or abandoning a failed task now tries, in order:

1. An available normal pocket, respecting item size, clothing dependencies and slot restrictions.
2. Storage directly worn in an inventory slot (for example a backpack or belt), respecting capacity and ordinary insertion/access checks.
3. A normal hands drop at the citizen's current feet if storage is unavailable/full/unsuitable.

Only the active task's item is moved, and only if the citizen actually holds it. Occupied slots are not replaced, unrelated hand contents are not moved, no bag is spawned, and containers/items are never force-inserted or deleted by cleanup. A physically blocked drop (for example an unremovable item or incapacitated actor) can leave the item held; the last-result/status text reports that exception. Successful deliveries still drop at their destination.

The default civilian loadout has pockets and no backpack; equip a backpack through normal inventory/admin interactions if larger storage is wanted. This change does not add automatic retrieval of items from inventory for later tasks: the existing delivery command still selects loose items.

The complete focused suite now has **10 passing cases, none failed/skipped**, including pocket storage, backpack storage with occupied pockets, full-inventory drop, automatic failure cleanup on destination deletion, and blocked-drop preservation. Each cleanup case also verifies that an unrelated held screwdriver is untouched and the task item still exists.
