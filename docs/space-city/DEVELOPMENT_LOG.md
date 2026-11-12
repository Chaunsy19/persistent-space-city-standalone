# Development log

Current status: Milestone 1 implemented and verified on `codex/milestone-1`. The earlier Milestone 0 entries below are historical. See `MILESTONE_1.md` for current spawn/test instructions. Milestone 2 and merging remain unapproved.

Current repository: [Chaunsy19/persistent-space-city-standalone](https://github.com/Chaunsy19/persistent-space-city-standalone), an independent repository with default branch `main`. The original setup record below describes the earlier fork; see the independence update at the end for current Git and launch instructions.

Date: 2026-11-11 (America/Los_Angeles). **Milestone 0 completion criteria verified. Milestone 1 has not begun and requires owner approval.**

## Foundation established

- Inspected the workspace first: pre-existing empty parent Git repository and empty `SS14/` directory. Preserved the parent repository and cloned official SS14 into `SS14/`; no unrelated projects changed.
- Created [Chaunsy19/persistent-space-city](https://github.com/Chaunsy19/persistent-space-city), a separate GitHub fork. Local `origin` points there; `upstream` points to `https://github.com/space-wizards/space-station-14.git`.
- Working branch: `codex/milestone-0`. Documentation changes remain local unless explicitly published later; creating the fork did not publish custom gameplay or these local changes.
- Existing dependencies: Git 2.55.0.windows.3, Python 3.11.9, .NET SDK 10.0.102 (x64 Windows). No installers run. `global.json` requests 10.0.100 with `latestFeature`, accepting installed 10.0.102.
- Ran `git submodule update --init --recursive` and official `python RUN_THIS.py`; setup installed repository hooks and confirmed submodules. Subsequent status verification found all five submodules at recorded revisions, with no uninitialized/mismatched markers.

| Repository/submodule | Tested commit |
| --- | --- |
| SS14 | `9ccfa8d118307aced564fa824d6ad5b9e217847a` |
| RobustToolbox | `d56b0d0bcf94c76bef7fc18625bf226607dca092` |
| Lidgren.Network | `4a111ba2e392bc0adcf2b724999c40cbbaae4662` |
| Robust.LoaderApi | `2b6a7db8d85166b345302e446a8492a213fc92aa` |
| XamlX | `8d9b5a8c670cf410308f85e3914373d848ccf698` |
| cefglue | `f8f5135dbcb6d1c1638043966d7a7c620676b5f8` |

## Verification results

| Criterion/test | Observed result |
| --- | --- |
| Repository/fork initialized | PASS: official history cloned, personal fork created, remotes separated |
| Required SDK and setup dependencies | PASS: existing versions compatible; NuGet restore completed |
| Recursive submodules | PASS: all pinned commits verified |
| `dotnet restore SpaceStation14.slnx --verbosity minimal` | PASS, exit 0; advisory/pruning warnings recorded below |
| `dotnet build SpaceStation14.slnx --no-restore --verbosity minimal` | PASS, exit 0; 1,025 warnings, 0 errors; elapsed 00:01:15.72 |
| Local server launch | PASS: `Server Version 295.0.3.0 -> Ready`; Sandbox development station loaded |
| Local-only listener | PASS: server UDP endpoint confirmed `127.0.0.1:1212`; hub advertising explicitly disabled |
| Graphical client launch | PASS: actual game window rendered the development map, character, HUD and welcome message |
| Client/server connection | PASS: server approved `localhost@CityDev`; client received Full GameState, reached `InGame`, attached local player to a `MobHuman`, and entered `GameplayState`; corroborated visually |
| NPC/job/persistence investigation | PASS: source findings and future design recorded in `ARCHITECTURE.md`; two fork organization strategies inspected |
| Documentation/rules | PASS: four requested documents plus repository `AGENTS.md` and workspace navigation README |
| No gameplay changes | PASS: only documentation and local-data ignore rule added; engine and gameplay source untouched |

This is a development smoke test on the upstream Sandbox/Dev setup, not a production hosting, persistence, multi-client, endurance, or autonomous-worker test. No gameplay tests were added and the full automated test suite was not run for this documentation/setup-only milestone. No completion criterion remains unverified at the time of this log.

Local evidence files (ignored by Git): `milestone0-restore.log`, `milestone0-build.log`, `milestone0-server.log`, `milestone0-client.log`. Preserve these when diagnosing this particular build. Runtime SQLite/server data lives in `.space-city-local/server/`, explicitly ignored. Client uses `--self-contained`, so its data stays relative to the built client instead of a user-global profile.

At handoff, the local server and client were left running for the owner to inspect. Observed process IDs were server **20660**, client **1856**; these IDs are session-specific and must be rechecked before any later process operation. No public hosting, router configuration, firewall changes, or hub registration was performed.

## Repeatable development commands

Run from `C:\Users\devin\Documents\ChatGPT\SS14 City Sim\SS14`, not the parent workspace:

```powershell
git status --short
git submodule status --recursive
python RUN_THIS.py
dotnet restore SpaceStation14.slnx
dotnet build SpaceStation14.slnx --no-restore
```

For a new test session, stop the existing test processes first to avoid a port conflict. Open two PowerShell consoles in the checkout. In the first:

```powershell
.\run-space-city-server.ps1
```

After the server reports Ready, in the second:

```powershell
.\run-space-city-client.ps1
```

These use the same official `Content.Server`/`Content.Client` development entry points as `runserver.bat`/`runclient.bat`, with flags for local binding, isolated data and automatic connection. Alternatively launch the client normally and use Direct Connect to `127.0.0.1:1212`. On the unmodified Debug setup, the Sandbox map spawned the test player automatically and granted local host administration; this is development behavior, not a production access policy. Close the client normally; for a server launched in your own console, use its `shutdown` command and wait for exit. Rebuild after code changes before using `--no-build`.

## Known issues and limits

- Restricted-shell network access initially caused `Could not resolve host: github.com`; Git's MSYS helper also reported `NtCreateDirectoryObject ... 0xC0000022` in restricted mode. Network/setup/build/submodule verification succeeded through approved expanded execution. CLI authentication appeared invalid in restricted mode but succeeded with the existing Windows keyring outside that restriction; no login/token change was necessary. Do not mistake these environment restrictions for repository errors.
- Restore reported NU1903 high-severity advisories on `Microsoft.NETCore.App` 1.0.5 and `Microsoft.NETCore.Jit` 1.0.7 (legacy Pow3r dependency graph), and `SixLabors.ImageSharp` 3.1.12 (Pow3r/Robust benchmarks). Exact advisory URLs and affected project contexts are in the restore log. Packages were not upgraded or warnings suppressed. Review reachable dependency impact before any public deployment or use of those tools with untrusted inputs.
- Other upstream build warnings include obsolete APIs/analyzers, NU1510 package-pruning notices, and NETSDK1206 legacy runtime identifiers (Libuv). They did not prevent Debug build or launch. Release builds were not verified.
- Fresh SQLite migrations emitted EF warnings about historical `PRAGMA foreign_keys = 0` operations outside transactions. Initial migration and ongoing admin-log writes succeeded. These databases are local test data, not a tested city persistence store.
- Client logged a Discord IPC connection timeout; it was nonfatal and the game connected/rendered normally. Initial loading also emitted a main-loop catch-up warning. No steady-state performance baseline is claimed.
- Snapshot completeness, restart recovery, job automation, multi-grid commuting, zero-player endurance and population scaling remain future work, not Milestone 0 features.

## Decisions and next step

Use existing assemblies with `_SpaceCity` folders when custom work begins. Extend HTN, steering, hands and shared interaction systems. Do not modify RobustToolbox or duplicate simulation mechanics. Keep durable identity separate from transient plans and raw entity IDs. Proposed persistence combines audited engine serialization with versioned metadata and an explicit persistent-world lifecycle; it must be proven through restart tests in Milestone 6.

Changes: `.gitignore` (local runtime data only), `AGENTS.md`, `docs/space-city/PROJECT_VISION.md`, `ARCHITECTURE.md`, `ROADMAP.md`, and this log. The parent workspace `README.md` points future sessions to the actual game checkout. Official setup generated ignored hook/version/build artifacts. No C# or prototype gameplay files were changed; no custom gameplay directories were created.

Recommended next action: review these notes, then explicitly approve Milestone 1. Its first increment should be one server-controlled humanoid that navigates through an authorized door and performs a real item interaction using existing HTN, with clear blocked-path/cancellation behavior. Verify it from two clients before expanding to needs or professions. Do not start that implementation automatically.

## Milestone 0 update â€” standalone ownership and project identity

Authorized by the owner on 2026-11-11 after reviewing the independence recommendation. Created `Chaunsy19/persistent-space-city-standalone` as a new public repository, preserving `Chaunsy19/persistent-space-city` without modification. GitHub reports `fork: false` and no parent. Local `origin` points exclusively to the standalone repository; the content `upstream` remote was removed. `main` is the project branch and `milestone-0` identifies the completed foundation. Source history and the original Milestone 0 commit remain in ancestry.

Updated README, contribution guidelines, GitHub CODEOWNERS and issue/PR templates to this project's destinations. Updated the manifest title to Persistent Space City and added `space-city.server.toml` plus two project launch scripts. New configuration gives the server its project name/links and binds both UDP game traffic and HTTP status API to loopback, with UPnP/hub advertising disabled. Existing engine submodules, license notices, credits, artwork and gameplay remain unchanged. Inherited Actions are disabled at repository level; their original workflow files remain for reference. See `INDEPENDENCE.md` for details and direct launch equivalents.

Verification: both PowerShell scripts parsed successfully; Python TOML validation confirmed project links and local-only settings; full solution rebuild passed with **896 warnings, zero errors**, elapsed **00:00:44.07**. An initial rebuild failed because the original test server held DLLs open; its exact executable path was verified before stopping it, then the rebuild passed. No source/compiler errors remained.

Executed both new launch scripts against isolated test data on loopback port 1213, overriding both game port and status binding. Server reached Ready; `/status` reported `Persistent Space City â€” Development`; `/info` exposed the standalone repository/documentation links. OS socket queries verified both UDP and TCP listeners were `127.0.0.1:1213`. The headless verification client received a full game state, reached InGame and attached to a player entity. This update did not repeat graphical rendering verification; window-title configuration was checked directly, while the original milestone includes a graphical connection test.

Local evidence: `milestone0-independence-build.log` (initial lock failure), `milestone0-independence-build-retry.log` (successful rebuild), `milestone0-independence-server.log` and `milestone0-independence-client.log`. These remain ignored. Temporary verification processes were stopped after testing; use the new scripts for the next normal development session. No production hosting or gameplay milestone was started.

A final documentation follow-up adapted `CODE_OF_CONDUCT.md` to project ownership and moderation routing, preserving its original Django/SS14 attribution. This follow-up changes no executable code or configuration; build/connection results above remain applicable.

## Milestone 1 â€” first autonomous humanoid (2026-11-11)

Owner explicitly authorized the supplied Milestone 1 brief. Before gameplay edits, verified a clean working tree and that completed Milestone 0 commit `098c17870765388d452377beedbb8382e5d9fbe3` exactly matched standalone `origin/main`; no additional foundation push was needed. Created `codex/milestone-1` from that commit. Kept the standalone origin despite the older fork URL appearing in the brief. No merge, history rewrite or Milestone 2 work occurred.

Implemented a `MobCivilian`/`MobHuman`-derived Space City Citizen, ordinary human appearance/loadout and passenger ID, an HTN root, a server-only bounded delivery coordinator and component, and `spacecity` Debug admin commands for spawning, queuing, status, stop and reset. The prototype file also contains a normal crowbar-derived test item and destination marker using existing art. Reused HTN scheduling, NPC steering/pathfinding, collision/movement, doors/access and normal hands/container replication. A small coordinator provides bounded failure, task state/postconditions and reach/access checks absent from a bare generic pickup operator. It adds no general AI framework or engine change.

Files created: `Content.Server/_SpaceCity/Citizens/{SpaceCityCitizenComponent,SpaceCityCitizenSystem,SpaceCityCitizenOperator,SpaceCityCitizenCommand}.cs`; `Resources/Prototypes/_SpaceCity/citizen.yml`; `Content.IntegrationTests/Tests/_SpaceCity/CitizenTests.cs`; `docs/space-city/MILESTONE_1.md`. Updated architecture/roadmap/vision, this log, README and AGENTS for the approved scope and current behavior.

Verification performed:

- Full nonincremental `dotnet build SpaceStation14.slnx --no-restore --no-incremental`: success, **1,012 warnings, 0 errors**, elapsed **00:00:54.48**. The original Milestone 0 full build had 1,025 warnings. Normalized unique warning location/code/message sets contained no new warnings; `_SpaceCity` files introduce no warnings. This does not claim the existing dependency/obsolete API warnings were repaired. Earlier compile-time issues found during development were corrected before final verification.
- Final focused integration run: **5 passed, 0 failed, 0 skipped** in 16 seconds. Exercises actual simulation, not mocked pathfinding: zero-player real pickup/delivery/drop around a solid wall; authorized door; denied door with bounded failure and subsequent reachable queued work; queue bound/stop/reset/target deletion/citizen deletion; two independent clients receiving consistent movement, doors, held-item, drop and clothing states from one server.
- Initial navigation fixture failures revealed that the shared test harness sets `pathfinding.enabled=false`. The fixture enables it explicitly and checks navmesh availability; ordinary runtime defaults remain unchanged. Tests also configure inherent gravity and a power-independent test airlock with its actual access-reader source specified. The first two-client run reached its assertions but reported a dirty test-pool disposal warning; corrected cleanup and confirmed a subsequent run had no skips.
- Launched the local server and two graphical clients. Both reached InGame; inspected each view. The live spawn command produced normal humanoids; live delivery picked up/carried/dropped an actual test item, returning to idle with completed=1 and failed=0. Also assigned another delivery and exercised live status/stop/reset. Automated client replica assertions cover transient hand/door/movement states more thoroughly than the graphical snapshots.
- An automation-only pseudo-terminal/stdout-redirection combination caused an engine console `WindowWidth` invalid-handle crash when sending input. Retried with a proper interactive console and succeeded. No RobustToolbox change or exception suppression was introduced. This limitation and normal launch instructions are documented.
- The final live two-client session emitted late `MsgEntity`/collision warnings with the inherited Debug fake-lag configuration; the second live delivery still completed (completed=2, failed=0). These are recorded as a limit on the smoke test, not silently treated as a clean latency/soak result.

Build/test/client/observation logs are ignored local evidence; final TRX is under ignored `Content.IntegrationTests/TestResults/`; the live server file log is under ignored `bin/Content.Server/logs/`. Verification processes were stopped at handoff. Full population/soak testing, arbitrary workstation use, multi-grid navigation and persistence were not attempted.

Known limits: same-grid explicit targets, bounded queue, retained held item on cancellation/failure, no reservations, no automatic resumption after player takeover, and configurable progress/deadline thresholds that may reject crowded or very slow routes. Ordinary physiology remains inherited from the humanoid prototype; no new needs model was introduced. Detailed behavior, admin commands and manual visual rechecks are in `MILESTONE_1.md`.

Milestone 1 completion criteria are verified. Commit the work to the Milestone 1 development branch only; publication/merge and Milestone 2 require the owner's instructions. Recommended Milestone 2: a minimal citizen identity/needs/schedule selector that reuses the tested execution path, keeping professions and persistence deferred.
