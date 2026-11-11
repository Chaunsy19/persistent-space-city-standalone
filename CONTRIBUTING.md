# Contributing to Persistent Space City

Submit issues and pull requests to https://github.com/Chaunsy19/persistent-space-city-standalone. Target `main`. This project is maintained independently of Space Wizards; do not send its changes or feature requests to the original project's repository or community channels.

Read `AGENTS.md` and `docs/space-city/` first. Work on the currently approved milestone only. Milestone 1 requires owner approval. Codex-assisted development is part of this project's workflow; contributors remain responsible for reviewing changes, verifying behavior and preserving licenses and attribution.

Use existing SS14 ECS, HTN, interaction and networking patterns. Keep custom content separate where practical, keep NPC decisions authoritative on the server, and avoid engine edits or incidental dependency upgrades. Describe the behavior changed, the reason, meaningful verification and any remaining limitations. Never include credentials, runtime databases or generated build/log files.

Build with `dotnet build SpaceStation14.slnx` after meaningful code changes and use focused behavior/integration tests when needed. Record architectural decisions and milestone results in the project documentation. Preserve original license notices and asset metadata.
