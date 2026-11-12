using Content.Server._SpaceCity.Citizens;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._SpaceCity.Work;

/// <summary>Server-thread ownership and lifecycle of a small, bounded delivery board.</summary>
public sealed partial class SpaceCityWorkOrderSystem : EntitySystem
{
    [Dependency] private SpaceCityCitizenSystem _execution = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;

    public const int MaxOrders = 512;
    private readonly Dictionary<EntityUid, SpaceCityWorkOrderComponent> _orders = new();
    // Priority first (descending), then creation sequence. No periodic sorting/replanning.
    private readonly SortedDictionary<(int Priority, long Sequence), EntityUid> _available = new();
    private readonly Dictionary<EntityUid, EntityUid> _claims = new();
    private readonly Dictionary<EntityUid, EntityUid> _openItems = new();
    private long _sequence;
    private TimeSpan _nextValidation;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceCityCitizenComponent, CitizenTaskResolvedEvent>(OnTaskResolved);
        SubscribeLocalEvent<SpaceCityCitizenComponent, CitizenRemovedEvent>(OnCitizenShutdown);
        SubscribeLocalEvent<SpaceCityWorkOrderComponent, ComponentShutdown>(OnOrderShutdown);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public IEnumerable<KeyValuePair<EntityUid, SpaceCityWorkOrderComponent>> Orders => _orders;
    public bool TryGet(EntityUid uid, out SpaceCityWorkOrderComponent order) => _orders.TryGetValue(uid, out order!);

    private static (int, long) Key(SpaceCityWorkOrderComponent order) => (-(int)order.Priority, order.CreationSequence);

    public bool TryCreate(EntityUid item, EntityUid destination, int priority, out EntityUid uid, out string result)
    {
        uid = default;
        if (priority is < 1 or > 3 || _orders.Count >= MaxOrders || _openItems.ContainsKey(item) ||
            item == destination || TerminatingOrDeleted(item) || TerminatingOrDeleted(destination) ||
            !HasComp<ItemComponent>(item) || !TryComp(item, out TransformComponent? itemTransform) ||
            !TryComp(destination, out TransformComponent? destinationTransform) || itemTransform.Anchored ||
            itemTransform.GridUid is not { } grid || grid != destinationTransform.GridUid ||
            _containers.IsEntityInContainer(item) || _containers.IsEntityInContainer(destination))
        {
            result = "Order rejected: use a loose movable item and a destination on one grid, priority 1..3, " +
                     "with no other open order for that item (512 retained orders per round maximum).";
            return false;
        }

        uid = Spawn(null, MapCoordinates.Nullspace);
        var order = AddComp<SpaceCityWorkOrderComponent>(uid);
        order.Item = item;
        order.Destination = destination;
        order.Grid = grid;
        order.ItemId = GetNetEntity(item);
        order.DestinationId = GetNetEntity(destination);
        order.Priority = (SpaceCityWorkPriority)priority;
        order.CreationSequence = ++_sequence;
        _orders.Add(uid, order);
        _available.Add(Key(order), uid);
        _openItems.Add(item, uid);
        result = $"Created delivery order {GetNetEntity(uid)} (priority {priority}).";
        Log.Info(result);
        return true;
    }

    public bool TryClaim(EntityUid citizen, out EntityUid orderUid)
    {
        orderUid = default;
        if (_claims.ContainsKey(citizen) || TerminatingOrDeleted(citizen) ||
            !TryComp<SpaceCityCitizenComponent>(citizen, out var execution) || execution.Stopped ||
            execution.CurrentTask != null || execution.Tasks.Count != 0 ||
            !TryComp(citizen, out TransformComponent? transform))
            return false;
        // Called only by the selector on the server thread; claiming/submission cannot race another selector.
        foreach (var (_, candidate) in _available)
        {
            var order = _orders[candidate];
            if (transform.GridUid != order.Grid || !TargetsValid(order))
                continue;
            orderUid = candidate;
            break;
        }
        if (!orderUid.IsValid())
            return false;

        var selected = _orders[orderUid];
        selected.Status = SpaceCityWorkStatus.Claimed;
        selected.AssignedCitizen = citizen;
        selected.LastCitizenId = GetNetEntity(citizen);
        selected.Assignments++;
        _available.Remove(Key(selected));
        _claims.Add(citizen, orderUid);
        if (TryComp<SpaceCityAutonomyComponent>(citizen, out var autonomy))
            autonomy.SelectedOrder = orderUid;
        if (!_execution.Assign(citizen, selected.Destination, selected.Item, out var reason, orderUid))
        {
            FinalizeOrder(orderUid, selected, SpaceCityWorkStatus.Failed, $"Executor rejected submission: {reason}");
            return false;
        }
        Log.Info($"Delivery order {GetNetEntity(orderUid)} claimed by citizen {selected.LastCitizenId}");
        return true;
    }

    public void CancelForCitizen(EntityUid citizen, string reason)
    {
        if (_claims.TryGetValue(citizen, out var uid))
            Cancel(uid, reason);
    }

    public bool Cancel(EntityUid uid, string reason)
    {
        if (!_orders.TryGetValue(uid, out var order) || IsTerminal(order.Status))
            return false;
        ResolveAndAbort(uid, order, SpaceCityWorkStatus.Cancelled, reason);
        return true;
    }

    public static bool IsTerminal(SpaceCityWorkStatus status) => status is
        SpaceCityWorkStatus.Completed or SpaceCityWorkStatus.Failed or SpaceCityWorkStatus.Cancelled;

    private bool TargetsValid(SpaceCityWorkOrderComponent order)
    {
        if (TerminatingOrDeleted(order.Grid) || TerminatingOrDeleted(order.Item) || TerminatingOrDeleted(order.Destination) ||
            !TryComp(order.Item, out TransformComponent? item) || !TryComp(order.Destination, out TransformComponent? destination) ||
            item.GridUid != order.Grid || destination.GridUid != order.Grid || _containers.IsEntityInContainer(order.Destination))
            return false;
        // A claimed item in this citizen's hand is available to its own executor, not to others.
        return (order.AssignedCitizen is { } citizen && !TerminatingOrDeleted(citizen) && _hands.IsHolding(citizen, order.Item)) ||
               (!_containers.IsEntityInContainer(order.Item) && !item.Anchored);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextValidation)
            return;
        _nextValidation = _timing.CurTime + TimeSpan.FromSeconds(1);
        foreach (var (uid, order) in _orders)
        {
            if (!IsTerminal(order.Status) && !TargetsValid(order))
                ResolveAndAbort(uid, order, SpaceCityWorkStatus.Failed, "Item, destination or grid deleted/moved/unavailable");
        }
    }

    private void OnTaskResolved(EntityUid citizen, SpaceCityCitizenComponent component, ref CitizenTaskResolvedEvent args)
    {
        if (args.Task.WorkOrder is not { } uid || !_orders.TryGetValue(uid, out var order) ||
            order.Status != SpaceCityWorkStatus.Claimed || order.AssignedCitizen != citizen)
            return;
        var status = args.Outcome switch
        {
            CitizenTaskOutcome.Completed => SpaceCityWorkStatus.Completed,
            CitizenTaskOutcome.Failed => SpaceCityWorkStatus.Failed,
            _ => SpaceCityWorkStatus.Cancelled,
        };
        FinalizeOrder(uid, order, status, args.Reason);
    }

    private void ResolveAndAbort(EntityUid uid, SpaceCityWorkOrderComponent order, SpaceCityWorkStatus status, string reason)
    {
        var citizen = order.AssignedCitizen;
        // Clear ownership before cancellation events can re-enter this system.
        FinalizeOrder(uid, order, status, reason);
        if (citizen is { } owner && !TerminatingOrDeleted(owner))
            _execution.CancelWork(owner, uid, reason);
    }

    private void FinalizeOrder(EntityUid uid, SpaceCityWorkOrderComponent order, SpaceCityWorkStatus status, string reason)
    {
        _available.Remove(Key(order));
        _openItems.Remove(order.Item);
        if (order.AssignedCitizen is { } citizen)
        {
            _claims.Remove(citizen);
            if (TryComp<SpaceCityAutonomyComponent>(citizen, out var autonomy))
            {
                autonomy.SelectedOrder = null;
                autonomy.LastDecision = $"Order {GetNetEntity(uid)} {status}: {reason}";
            }
        }
        order.AssignedCitizen = null;
        order.Status = status;
        order.ResolvedAt = _timing.CurTime;
        order.LastResult = reason;
        Log.Info($"Delivery order {GetNetEntity(uid)} {status}; claim released from {order.LastCitizenId}: {reason}");
    }

    private void OnCitizenShutdown(EntityUid citizen, SpaceCityCitizenComponent component, ref CitizenRemovedEvent args)
    {
        if (_claims.TryGetValue(citizen, out var uid) && _orders.TryGetValue(uid, out var order))
            FinalizeOrder(uid, order, SpaceCityWorkStatus.Failed, "Citizen removed");
    }

    private void OnOrderShutdown(EntityUid uid, SpaceCityWorkOrderComponent component, ComponentShutdown args)
    {
        if (!_orders.ContainsKey(uid))
            return;
        if (!IsTerminal(component.Status))
            FinalizeOrder(uid, component, SpaceCityWorkStatus.Failed, "Order entity explicitly removed");
        _orders.Remove(uid);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        // The game ticker then flushes all entities. Do not move items during world teardown.
        _orders.Clear();
        _available.Clear();
        _claims.Clear();
        _openItems.Clear();
        _sequence = 0;
    }
}
