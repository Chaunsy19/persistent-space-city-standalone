# Persistent Space City development

Read `docs/space-city/DEVELOPMENT_LOG.md`, `ARCHITECTURE.md`, and `ROADMAP.md` before work. This directory is the actual game Git repository; the parent workspace has a separate, pre-existing empty repository.

- Develop only the approved milestone. Milestone 1 (first autonomous humanoid) is authorized; do not begin Milestone 2, merge branches, or publish new work without owner approval. See `docs/space-city/MILESTONE_1.md` for current testing instructions.
- Preserve upstream functionality and use SS14 ECS components, systems, prototypes, events, and existing interaction APIs.
- Put future custom code under `Content.Server/_SpaceCity`, `Content.Shared/_SpaceCity`, and `Content.Client/_SpaceCity`; custom resources under matching `_SpaceCity` resource directories. Create these only when needed.
- NPC decisions and world changes are authoritative on the server. Shared state/events must follow existing networking conventions and validate player requests.
- Extend existing HTN/navigation rather than replacing the NPC framework. Avoid RobustToolbox changes.
- Keep each change focused. Document decisions, tests, limitations, and exact revisions. Build/test after meaningful code changes; choose tests that exercise actual behavior.
- Use `global.json` and pinned recursive submodules. Do not upgrade packages or engine revisions incidentally.
- Development server must remain local: `net.bindto=127.0.0.1`, `hub.advertise=false`. No public hosting or router configuration in this milestone.
- Never commit runtime databases, client data, secrets, or logs. `origin` is `https://github.com/Chaunsy19/persistent-space-city-standalone.git`; `main` is the project branch. There is no upstream content remote. Do not create PRs against Space Wizards. Publish only when requested.
- Use `space-city.server.toml` and `run-space-city-server.ps1` / `run-space-city-client.ps1` for the project development identity. Preserve engine dependencies, history, license notices and asset credits; remaining historical/technical links are not project destinations.
