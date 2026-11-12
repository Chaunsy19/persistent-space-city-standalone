using System.Linq;
using Content.Server._SpaceCity.Work;
using Robust.Shared.Console;

namespace Content.Server._SpaceCity.Citizens;

public sealed partial class SpaceCityCitizenCommand
{
    [Dependency] private SpaceCityWorkOrderSystem _work = default!;
    [Dependency] private SpaceCityGoalSelectorSystem _goals = default!;

    private void WriteGoal(IConsoleShell shell, EntityUid citizen)
    {
        if (!EntityManager.TryGetComponent<SpaceCityAutonomyComponent>(citizen, out var autonomy))
            return;
        var selected = autonomy.SelectedOrder is { } order ? EntityManager.GetNetEntity(order).ToString() : "none";
        shell.WriteLine($"Autonomy={autonomy.Enabled}, selected order={selected}; {autonomy.LastDecision}");
    }

    private void WriteOrder(IConsoleShell shell, EntityUid uid, SpaceCityWorkOrderComponent order)
    {
        var assigned = order.AssignedCitizen is { } citizen ? EntityManager.GetNetEntity(citizen).ToString() : "none";
        shell.WriteLine($"Order={EntityManager.GetNetEntity(uid)}, type={order.Type}, priority={(int)order.Priority}, " +
                        $"sequence={order.CreationSequence}, status={order.Status}, item={order.ItemId}, " +
                        $"destination={order.DestinationId}, assigned={assigned}, last citizen={order.LastCitizenId}, " +
                        $"assignments={order.Assignments}; {order.LastResult}");
    }

    private void ExecuteOrder(IConsoleShell shell, string[] args)
    {
        if (args.Length == 2 && args[1] == "list")
        {
            var count = 0;
            foreach (var (uid, order) in _work.Orders.OrderBy(pair => pair.Value.CreationSequence))
            {
                WriteOrder(shell, uid, order);
                count++;
            }
            shell.WriteLine($"{count}/{SpaceCityWorkOrderSystem.MaxOrders} retained orders (including terminal history).");
            return;
        }
        if (args.Length is 4 or 5 && args[1] == "create" &&
            TryEntity(args[2], out var item) && TryEntity(args[3], out var destination))
        {
            var priority = 2;
            if (args.Length == 5 && !int.TryParse(args[4], out priority))
            {
                shell.WriteError("Priority must be 1, 2 or 3.");
                return;
            }
            if (_work.TryCreate(item, destination, priority, out _, out var result))
                shell.WriteLine(result);
            else
                shell.WriteError(result);
            return;
        }
        if (args.Length == 3 && TryEntity(args[2], out var orderUid) && _work.TryGet(orderUid, out var record))
        {
            if (args[1] == "status")
                WriteOrder(shell, orderUid, record);
            else if (args[1] == "cancel")
            {
                if (!_work.Cancel(orderUid, "Cancelled by administrator"))
                    shell.WriteError("Order is already terminal; its history is retained.");
                WriteOrder(shell, orderUid, record);
            }
            else
                shell.WriteError(Help);
            return;
        }
        shell.WriteError(Help);
    }
}
