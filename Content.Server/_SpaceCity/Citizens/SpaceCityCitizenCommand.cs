using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._SpaceCity.Citizens;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class SpaceCityCitizenCommand : LocalizedEntityCommands
{
    [Dependency] private SpaceCityCitizenSystem _citizens = default!;

    public override string Command => "spacecity";
    public override string Description => "Assign, inspect, stop or reset a Space City Citizen.";
    public override string Help => "spacecity task <citizen NetEntity> <destination NetEntity> [item NetEntity]\n" +
                          "spacecity spawn <anchor NetEntity> (creates an idle citizen plus test item and marker nearby)\n" +
                          "spacecity status|stop|reset <citizen NetEntity>\n" +
                          "Spawn SpaceCityCitizen, SpaceCityDestination and SpaceCityTestItem with F5. IDs are shown in VV.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 2 && args[0] == "spawn")
        {
            if (!TryEntity(args[1], out var anchor) ||
                !EntityManager.TryGetComponent(anchor, out TransformComponent? transform) || transform.GridUid == null)
            {
                shell.WriteError("Spawn anchor must exist on a grid. Choose an open, safe floor area.");
                return;
            }

            var coordinates = transform.Coordinates;
            var spawned = EntityManager.SpawnAttachedTo("SpaceCityCitizen", coordinates.Offset(new System.Numerics.Vector2(1, 0)));
            var item = EntityManager.SpawnAttachedTo("SpaceCityTestItem", coordinates.Offset(new System.Numerics.Vector2(2, 0)));
            var destination = EntityManager.SpawnAttachedTo("SpaceCityDestination", coordinates.Offset(new System.Numerics.Vector2(4, 0)));
            shell.WriteLine($"Spawned citizen={EntityManager.GetNetEntity(spawned)}, " +
                            $"item={EntityManager.GetNetEntity(item)}, destination={EntityManager.GetNetEntity(destination)}. " +
                            "Idle until assigned. Check that the spawn area is clear.");
            return;
        }

        if (args.Length < 2 || !TryEntity(args[1], out var uid) ||
            !EntityManager.TryGetComponent<SpaceCityCitizenComponent>(uid, out var citizen))
        {
            shell.WriteError(Help);
            return;
        }

        switch (args[0])
        {
            case "task" when args.Length is 3 or 4:
                if (!TryEntity(args[2], out var destination) ||
                    (args.Length == 4 && !TryEntity(args[3], out _)))
                {
                    shell.WriteError("Target not found. Use network entity IDs, not local client UIDs.");
                    return;
                }
                EntityUid? item = null;
                if (args.Length == 4 && TryEntity(args[3], out var parsedItem))
                    item = parsedItem;
                if (_citizens.Assign(uid, destination, item, out var result))
                    shell.WriteLine(result);
                else
                    shell.WriteError(result);
                break;
            case "stop" when args.Length == 2:
            case "reset" when args.Length == 2:
                _citizens.Stop(uid, args[0] == "stop");
                shell.WriteLine(citizen.LastResult);
                break;
            case "status" when args.Length == 2:
                shell.WriteLine($"State={citizen.Activity}, queue={citizen.Tasks.Count}, " +
                                $"attempt={citizen.Attempts}, carrying={citizen.Carrying}, " +
                                $"completed={citizen.CompletedTasks}, failed={citizen.FailedTasks}; {citizen.LastResult}");
                break;
            default:
                shell.WriteError(Help);
                break;
        }
    }

    private bool TryEntity(string text, out EntityUid uid)
    {
        uid = default;
        if (!NetEntity.TryParse(text, out var net) || !EntityManager.TryGetEntity(net, out var entity) || entity == null)
            return false;
        uid = entity.Value;
        return EntityManager.EntityExists(uid);
    }
}
