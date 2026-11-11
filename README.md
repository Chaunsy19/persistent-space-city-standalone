# Persistent Space City

An independent multiplayer space-city simulation based on Space Station 14 and RobustToolbox. Players inhabit individual characters alongside autonomous citizens in a functioning society. This is not a colony-management game.

**Status: Milestone 0 — development foundation complete.** Citizen behavior and world persistence are planned features, not implemented yet. Milestone 1 requires owner approval.

## Project

- [Source](https://github.com/Chaunsy19/persistent-space-city-standalone)
- [Issues](https://github.com/Chaunsy19/persistent-space-city-standalone/issues)
- [Vision](docs/space-city/PROJECT_VISION.md)
- [Architecture](docs/space-city/ARCHITECTURE.md)
- [Roadmap](docs/space-city/ROADMAP.md)
- [Development log](docs/space-city/DEVELOPMENT_LOG.md)
- [Contribution guidelines](CONTRIBUTING.md)

This is the project's standalone repository, maintained by Chaunsy19. Submit project issues and pull requests here. The default branch is `main`; `milestone-0` marks the foundation checkpoint. The earlier GitHub fork is preserved separately and is not the publishing destination.

## Development on Windows

Install Git, Python 3 and the .NET SDK selected by `global.json` (currently .NET 10, with roll-forward enabled). From PowerShell:

```powershell
git clone --recurse-submodules https://github.com/Chaunsy19/persistent-space-city-standalone.git
cd persistent-space-city-standalone
python RUN_THIS.py
dotnet restore SpaceStation14.slnx
dotnet build SpaceStation14.slnx --no-restore
```

Then open two PowerShell consoles in the checkout:

```powershell
# First console: wait for the server to report Ready.
.\run-space-city-server.ps1
```

```powershell
# Second console: automatically connects to the local server.
.\run-space-city-client.ps1
```

The launch scripts use existing Content.Server/Content.Client entry points. Server identity and project links are in `space-city.server.toml`. It binds only to `127.0.0.1:1212`, disables UPnP and hub advertising, and stores runtime data in ignored `.space-city-local/server/`. The client uses self-contained local data. Rebuild after code changes; stop an existing server before starting another on the same port. If script execution is restricted, use the equivalent `dotnet run` commands in [the independence notes](docs/space-city/INDEPENDENCE.md).

## Project ownership and dependencies

The content repository has no `upstream` remote and is not in GitHub's fork network. Its original commit history and attribution are retained. RobustToolbox and its recursive submodules remain pinned external dependencies; their source URLs identify their maintainers and do not direct project changes to them. See [the repository independence notes](docs/space-city/INDEPENDENCE.md).

Inherited GitHub Actions are disabled in this repository until a project-specific CI configuration is reviewed. Original artwork remains in use during this foundation stage; the game/window and development server names have been updated. A full visual rebrand is future work.

## Attribution and licenses

Based on Space Station 14 by Space Wizards Federation and contributors, running on RobustToolbox. Original code history, [MIT license notice](LICENSE.TXT), and per-asset license/credit metadata are retained. Engine and third-party dependencies have their own notices. Asset licenses vary; consult each asset's metadata rather than treating all resources as MIT-licensed.

[Original development documentation](https://docs.spacestation14.com/en/general-development/setup/setting-up-a-development-environment.html) remains a technical reference; project-specific instructions above take precedence for this repository.
