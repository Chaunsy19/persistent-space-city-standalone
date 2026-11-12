# Roadmap

Each milestone must be approved independently. These are proposed acceptance boundaries, not authorization to implement them.

| Milestone | Deliverable | Minimum meaningful verification |
| --- | --- | --- |
| 0 — Development foundation | Fork, recursive submodules, dependencies, successful build, local server/client connection, architecture documentation | Full Debug solution build; connected graphical client in local development map; recorded revisions and issues |
| 1 — Autonomous humanoid (verified on development branch) | One custom humanoid prototype using existing HTN and steering; bounded delivery cycle and admin controls | Ten integration cases pass, including authorized/denied door routes, recovery to another task, real pickup/drop, deletion/reset, canceled-item storage/drop and two client replicas. Two graphical clients connected; live citizen delivery and appearance checked. No permanent world save. |
| 2 — Needs and schedules | Server-owned citizen identity, limited needs, shift/rest schedule and interruptible tasks | Need changes cause observable behavior; emergencies and unavailable resources cancel/replan; measure multiple NPCs with zero players |
| 3 — Functional janitor | Discover and clean actual puddles using existing tools, refill/dispose as needed | Puddle volume decreases through normal interactions; competing workers avoid duplicating work; inaccessible spills do not trap planning |
| 4 — Functional cook | Obtain real ingredients, select a bounded recipe, load appliance, cook and retrieve food | Ingredients consumed and actual appliance state changes; handle power loss, missing ingredients and occupied equipment; no direct meal spawning by AI |
| 5 — Basic medical NPC | Bounded triage and approved treatments using real supplies | Treat supported injuries, respect dose/availability, stop on invalid or unsafe conditions; specialized medibot behavior is reference only |
| 6 — World persistence | Versioned, consistent snapshot plus durable identity/player association and restart orchestration | Restart round-trip of construction, machinery, nested inventories, generated items, citizens, health/needs/jobs, resources, player body association; interrupted-save recovery and migration tests |
| 7 — Player integration and expansion | Durable player participation, citizen service interactions and controlled station growth | Two-client access/ownership checks, reconnect/restart consistency, expansion navigation and infrastructure recovery; population performance budget |

Milestones 0 and 1 were verified on 2026-11-11. Milestone 1 is on `codex/milestone-1`, based on completed foundation commit `098c17870765388d452377beedbb8382e5d9fbe3`. Do not merge or begin Milestone 2 without owner approval. Proposed sequence defers durable world implementation to Milestone 6 while requiring stable identity and serializable data choices earlier.
