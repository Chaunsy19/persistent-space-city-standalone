# Independent project setup

Date: 2026-11-11. This is a Milestone 0 foundation update, not approval to start Milestone 1.

## Canonical source

- Repository: https://github.com/Chaunsy19/persistent-space-city-standalone
- Default development branch: `main`
- Foundation checkpoint: annotated tag `milestone-0`
- Local content remote: only `origin`, pointing to the standalone repository
- Original fork: `Chaunsy19/persistent-space-city`, preserved without modification; it is not this project's publishing destination

The standalone repository was created empty, rather than through GitHub's fork operation. Original source history is retained, but the repository is outside the original fork network. Only the project main branch and milestone tag are published; inherited upstream branches/tags are not mirrored. There is no content `upstream` remote and no PR was opened against Space Wizards. Future pushes target this project.

## Project identity

`README.md` and `CONTRIBUTING.md` describe this project and link to its own issues, source, roadmap and architecture. GitHub CODEOWNERS and issue/PR templates route work here. Codex-assisted development follows `AGENTS.md` and requires human review and meaningful verification.

`Resources/manifest.yml` sets the client window/game title to **Persistent Space City**. Existing logo/icon/splash artwork remains credited upstream artwork; custom visual branding is a later task. `space-city.server.toml` sets the development server name, stable server identity, admin-log name and project links. Use the project launch scripts to load this configuration; the original generic launch scripts and original server presets remain technical references and do not automatically load it.

Inherited GitHub Actions were disabled at repository level before the initial source push. Their original YAML remains in history/source for reference, but this project has no active inherited publishing, scheduled maintenance or PR automation. Review and enable a project-specific build workflow separately when desired.

## Local launch

Build first, then use `run-space-city-server.ps1` and `run-space-city-client.ps1` in separate consoles. They use the normal .NET content entry points, without adding a gameplay framework. The server configuration binds game UDP and status HTTP only to `127.0.0.1:1212`, disables UPnP and hub advertising, and leaves hub URLs empty. Data stays in ignored `.space-city-local/server/`; the client uses self-contained data relative to its executable.

Equivalent commands if PowerShell scripts cannot run under your execution policy (no policy change needed):

```powershell
dotnet run --project Content.Server --no-build --no-restore -- --config-file space-city.server.toml --data-dir .space-city-local/server
```

```powershell
dotnet run --project Content.Client --no-build --no-restore -- --self-contained --username CityDev --connect --connect-address ss14://127.0.0.1:1212
```

Stop an existing server before starting another on the same port. The wrappers accept additional engine arguments for testing. If overriding `net.port`, also override `status.bind` to the same loopback port. They preserve the caller's working directory and propagate the engine exit code.

## References intentionally retained

RobustToolbox remains pinned at the tested commit through `.gitmodules`; its recursive dependencies retain their original URLs. These are dependency download locations, not project publishing destinations. No engine code was changed. Technical documentation, history, source credits, `LICENSE.TXT` and per-asset license metadata remain intact. Independence does not require removing provenance or renaming engine protocols/assemblies. There are no new art assets or simulated citizen behaviors in this update.

The game still uses existing engine services and protocols, including authentication defaults. Repository independence is not a claim that every engine/network dependency has been replaced. A custom launcher, authentication service, release infrastructure, public hosting and complete visual rebrand are outside this foundation update.
