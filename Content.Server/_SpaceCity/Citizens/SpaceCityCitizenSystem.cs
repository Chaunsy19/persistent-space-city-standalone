using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.ActionBlocker;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._SpaceCity.Citizens;

/// <summary>
/// Small delivery coordinator invoked by HTN, not a second planner/pathfinder.
/// No entity searches: tasks reference specific admin-assigned targets.
/// </summary>
public sealed partial class SpaceCityCitizenSystem : EntitySystem
{
    [Dependency] private NPCSteeringSystem _steering = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceCityCitizenComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(EntityUid uid, SpaceCityCitizenComponent component, ComponentShutdown args)
    {
        _steering.Unregister(uid);
        component.Tasks.Clear();
        component.CurrentTask = null;
    }

    public bool Assign(EntityUid uid, EntityUid destination, EntityUid? item, out string result)
    {
        if (!TryComp<SpaceCityCitizenComponent>(uid, out var citizen) || citizen.Stopped)
        {
            result = "Not an active citizen (use reset to resume a stopped citizen).";
            return false;
        }

        if (citizen.Tasks.Count >= 8 || destination == uid || item == uid || item == destination ||
            !ValidTarget(uid, destination) || (item is { } target && !ValidTarget(uid, target)))
        {
            result = "Invalid task: targets must exist on the citizen's grid, outside containers; queue limit is 8.";
            return false;
        }

        citizen.Tasks.Enqueue(new CitizenTask(destination, item));
        result = "Task queued.";
        return true;
    }

    private bool ValidTarget(EntityUid uid, EntityUid target)
    {
        return !TerminatingOrDeleted(target) &&
               TryComp(uid, out TransformComponent? owner) &&
               TryComp(target, out TransformComponent? goal) &&
               owner.GridUid != null && owner.GridUid == goal.GridUid &&
               !_containers.IsEntityInContainer(target);
    }

    public void Stop(EntityUid uid, bool stopped)
    {
        if (!TryComp<SpaceCityCitizenComponent>(uid, out var citizen))
            return;

        _steering.Unregister(uid);
        var cleanup = PutAwayTaskItem(uid, citizen);
        citizen.Tasks.Clear();
        citizen.CurrentTask = null;
        citizen.Carrying = false;
        citizen.Stopped = stopped;
        citizen.Attempts = 0;
        citizen.Activity = stopped ? CitizenActivity.Stopped : CitizenActivity.Idle;
        citizen.LastResult = (stopped ? "Stopped" : "Reset; waiting for a new task") + cleanup;
        citizen.NextUpdate = _timing.CurTime;
    }

    /// <summary>Player attachment, incapacitation or HTN shutdown cancels this demo's task safely.</summary>
    public void Suspend(EntityUid uid)
    {
        if (!TerminatingOrDeleted(uid) && HasComp<SpaceCityCitizenComponent>(uid))
            Stop(uid, true);
    }

    public void Step(EntityUid uid)
    {
        if (!TryComp<SpaceCityCitizenComponent>(uid, out var citizen) ||
            citizen.Stopped || _timing.CurTime < citizen.NextUpdate)
            return;

        citizen.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        if (citizen.CurrentTask == null)
        {
            if (!citizen.Tasks.TryDequeue(out var next))
                return;

            citizen.CurrentTask = next;
            citizen.Carrying = false;
            citizen.Attempts = 0;
            StartLeg(uid, citizen, next.Item == null ? CitizenActivity.ToDestination : CitizenActivity.ToItem);
            return;
        }

        var task = citizen.CurrentTask.Value;
        if (!ValidTarget(uid, task.Destination) ||
            (task.Item is { } item && (TerminatingOrDeleted(item) ||
                (citizen.Carrying ? !_hands.IsHolding(uid, item) : !ValidTarget(uid, item)))))
        {
            Finish(uid, citizen, false, "Target removed, moved off-grid, contained, or item no longer held");
            return;
        }

        if (citizen.Activity == CitizenActivity.RetryWait)
        {
            StartLeg(uid, citizen, citizen.RetryActivity);
            return;
        }

        var target = citizen.Activity == CitizenActivity.ToItem ? task.Item!.Value : task.Destination;
        var targetCoordinates = Transform(target).Coordinates;
        if (_timing.CurTime >= citizen.LegDeadline)
        {
            Retry(uid, citizen, "Navigation leg deadline exceeded");
            return;
        }
        if (!TryComp<NPCSteeringComponent>(uid, out var steering))
        {
            Retry(uid, citizen, "Steering was interrupted");
            return;
        }

        // Follow a target if it was moved while we approached, without requesting a path every update.
        if (!steering.Coordinates.TryDistance(EntityManager, targetCoordinates, out var moved) || moved > 0.5f)
        {
            _steering.Unregister(uid);
            ConfigureSteering(uid, targetCoordinates);
            return;
        }

        var coordinates = Transform(uid).Coordinates;
        if (coordinates.TryDistance(EntityManager, targetCoordinates, out var distance) && distance <= 0.8f)
        {
            // Hands.TryPickup alone does not enforce interaction range/line of sight. Check both here.
            if (!_interaction.InRangeUnobstructed(uid, target))
            {
                Retry(uid, citizen, "Target is obstructed at interaction distance");
                return;
            }

            _steering.Unregister(uid);
            if (citizen.Activity == CitizenActivity.ToItem)
            {
                if (!_interaction.CanAccess(uid, target) || !_hands.TryPickupAnyHand(uid, target) ||
                    !_hands.IsHolding(uid, target))
                {
                    Finish(uid, citizen, false, "Pickup rejected by the normal hands/interaction checks");
                    return;
                }

                citizen.Carrying = true;
                citizen.Attempts = 0;
                StartLeg(uid, citizen, CitizenActivity.ToDestination);
                return;
            }

            if (task.Item is { } carried &&
                (!_hands.TryDrop(uid, carried) || _hands.IsHolding(uid, carried) ||
                    _containers.IsEntityInContainer(carried)))
            {
                Finish(uid, citizen, false, "Drop rejected or item remained contained");
                return;
            }

            Finish(uid, citizen, true, task.Item == null ? "Destination reached" : "Item picked up and delivered");
            return;
        }

        // Net displacement, not distance-to-goal: valid detours may initially move away from the goal.
        if (coordinates.TryDistance(EntityManager, citizen.LastPosition, out var progress) && progress >= 0.3f)
        {
            citizen.LastPosition = coordinates;
            citizen.LastProgress = _timing.CurTime;
        }

        if (steering.Status == SteeringStatus.NoPath)
            Retry(uid, citizen, $"Engine steering reported no path at {coordinates} (failed paths={steering.FailedPathCount})");
        else if (_timing.CurTime - citizen.LastProgress >= TimeSpan.FromSeconds(Math.Clamp(citizen.StuckSeconds, 1, 15)))
            Retry(uid, citizen, $"No movement progress at {coordinates}");
    }

    private void ConfigureSteering(EntityUid uid, EntityCoordinates coordinates)
    {
        var steering = _steering.Register(uid, coordinates);
        steering.Range = 0.7f;
        // Normal access checks remain in SS14 steering/door systems. No pry, smash or climb fallback.
        steering.Flags = PathFlags.Interact;
    }

    private void StartLeg(EntityUid uid, SpaceCityCitizenComponent citizen, CitizenActivity activity)
    {
        var task = citizen.CurrentTask!.Value;
        var target = activity == CitizenActivity.ToItem ? task.Item!.Value : task.Destination;
        if (TerminatingOrDeleted(target))
        {
            Finish(uid, citizen, false, "Target deleted before navigation began");
            return;
        }

        citizen.Activity = activity;
        citizen.Attempts++;
        citizen.LastPosition = Transform(uid).Coordinates;
        citizen.LastProgress = _timing.CurTime;
        citizen.LegDeadline = _timing.CurTime + TimeSpan.FromSeconds(Math.Clamp(citizen.LegTimeoutSeconds, 2, 60));
        citizen.LastResult = $"{activity}: attempt {citizen.Attempts}";
        ConfigureSteering(uid, Transform(target).Coordinates);
        Log.Info($"Citizen {ToPrettyString(uid)}: {citizen.LastResult}");
    }

    private void Retry(EntityUid uid, SpaceCityCitizenComponent citizen, string reason)
    {
        _steering.Unregister(uid);
        if (citizen.Attempts >= Math.Clamp(citizen.MaxAttempts, 1, 3))
        {
            Finish(uid, citizen, false, reason);
            return;
        }

        citizen.RetryActivity = citizen.Activity;
        citizen.Activity = CitizenActivity.RetryWait;
        citizen.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(2);
        citizen.LastResult = $"Retry waiting: {reason}";
        Log.Warning($"Citizen {ToPrettyString(uid)}: {citizen.LastResult}");
    }

    private void Finish(EntityUid uid, SpaceCityCitizenComponent citizen, bool success, string result)
    {
        _steering.Unregister(uid);
        if (!success)
            result += PutAwayTaskItem(uid, citizen);
        if (success)
            citizen.CompletedTasks++;
        else
            citizen.FailedTasks++;
        citizen.LastResult = result;
        citizen.CurrentTask = null;
        citizen.Carrying = false;
        citizen.Activity = CitizenActivity.Idle;
        citizen.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(2);
        if (success)
            Log.Info($"Citizen {ToPrettyString(uid)} completed task: {result}");
        else
            Log.Warning($"Citizen {ToPrettyString(uid)} abandoned task: {result}");
    }

    /// <summary>
    /// Clean up only the active task's item. Never rearrange unrelated hand contents, force
    /// insertion into a full container, or bypass the actor's normal removal/action checks.
    /// Successful deliveries still drop at their destination through the ordinary delivery path.
    /// </summary>
    private string PutAwayTaskItem(EntityUid uid, SpaceCityCitizenComponent citizen)
    {
        if (citizen.CurrentTask?.Item is not { } item || TerminatingOrDeleted(item) ||
            !_hands.IsHolding(uid, item))
            return "";

        if (_hands.CanDrop(uid, item) && _actionBlocker.CanInteract(uid, item) &&
            _inventory.TryGetSlots(uid, out var slots))
        {
            foreach (var slot in slots)
            {
                if ((slot.SlotFlags & SlotFlags.POCKET) != 0 &&
                    _inventory.TryEquip(uid, item, slot.Name, silent: true) && !_hands.IsHolding(uid, item))
                    return $"; task item stored in {slot.Name}";
            }

            // Only directly worn storage is considered, not bags on the floor or recursive nested bags.
            foreach (var slot in slots)
            {
                if (!_inventory.TryGetSlotEntity(uid, slot.Name, out var worn) ||
                    !TryComp<StorageComponent>(worn, out var storage) ||
                    !_interaction.CanAccess(uid, worn.Value) ||
                    !_storage.CanInsert(worn.Value, item, out _, storage) ||
                    !_storage.PlayerInsertEntityInWorld((worn.Value, storage), uid, item))
                    continue;

                if (TerminatingOrDeleted(item) || !_hands.IsHolding(uid, item))
                    return $"; task item stored in worn {slot.Name} storage";
            }
        }

        if (_hands.TryDrop(uid, item) && !_hands.IsHolding(uid, item))
            return "; task item dropped at citizen's feet";

        return "; task item retained because normal storage/drop checks rejected cleanup";
    }
}
