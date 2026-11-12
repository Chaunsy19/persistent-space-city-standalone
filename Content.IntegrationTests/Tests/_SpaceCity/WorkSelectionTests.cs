using System.Numerics;
using System.Linq;
using Content.Server._SpaceCity.Citizens;
using Content.Server._SpaceCity.Work;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._SpaceCity;

public sealed partial class CitizenTests
{
    // Board entities intentionally live outside maps so deleted targets retain diagnostic history.
    [TearDown]
    public async Task ClearWorkHistory()
    {
        await Server.WaitPost(() =>
        {
            foreach (var (uid, _) in Server.System<SpaceCityWorkOrderSystem>().Orders.ToArray())
                SEntMan.DeleteEntity(uid);
        });
    }

    private EntityUid Worker(EntityUid grid, float x = 0.5f, float y = 0.5f)
    {
        var uid = SEntMan.SpawnAttachedTo("SpaceCityCitizen", new EntityCoordinates(grid, x, y));
        // This floor-only navigation arena is vacuum. Isolate long work/idle tests from
        // inherited humanoid suffocation; real station citizens retain normal physiology.
        Server.System<SharedGodmodeSystem>().EnableGodmode(uid);
        Server.System<SpaceCityGoalSelectorSystem>().SetEnabled(uid, true);
        return uid;
    }

    private EntityUid Order(EntityUid grid, int priority = 2, float x = 2.5f, float y = 0.5f, string itemPrototype = "SpaceCityTestItem")
    {
        var item = SEntMan.SpawnAttachedTo(itemPrototype, new EntityCoordinates(grid, x, y));
        var destination = SEntMan.SpawnAttachedTo("SpaceCityDestination", new EntityCoordinates(grid, 7.5f, y));
        Assert.That(Server.System<SpaceCityWorkOrderSystem>().TryCreate(item, destination, priority, out var uid, out var result), Is.True, result);
        return uid;
    }

    [Test]
    public async Task AutonomousIdlePrioritySequenceAndResume()
    {
        var map = await Arena();
        EntityUid citizen = default;
        await Server.WaitPost(() => citizen = Worker(map.Grid));
        await RunFor(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<TransformComponent>(citizen).LocalPosition, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(SEntMan.GetComponent<SpaceCityCitizenComponent>(citizen).CurrentTask, Is.Null);
        });
        EntityUid low = default, first = default, second = default;
        await Server.WaitPost(() =>
        {
            low = Order(map.Grid, 1, y: -1.5f);
            first = Order(map.Grid, 3);
            second = Order(map.Grid, 3, y: 2.5f);
        });
        await RunFor(40);
        await Server.WaitAssertion(() =>
        {
            var orders = new[] { first, second, low }.Select(SEntMan.GetComponent<SpaceCityWorkOrderComponent>).ToArray();
            Assert.That(orders.All(o => o.Status == SpaceCityWorkStatus.Completed && o.Assignments == 1 && o.AssignedCitizen == null), Is.True);
            Assert.That(orders[0].ResolvedAt, Is.LessThan(orders[1].ResolvedAt));
            Assert.That(orders[1].ResolvedAt, Is.LessThan(orders[2].ResolvedAt));
            Assert.That(SEntMan.GetComponent<SpaceCityCitizenComponent>(citizen).CompletedTasks, Is.EqualTo(3));
        });
        Vector2 idle = default;
        await Server.WaitPost(() => idle = SEntMan.GetComponent<TransformComponent>(citizen).LocalPosition);
        await RunFor(5);
        EntityUid next = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Vector2.Distance(idle, SEntMan.GetComponent<TransformComponent>(citizen).LocalPosition), Is.LessThan(0.1f));
            next = Order(map.Grid);
        });
        await RunFor(14);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<SpaceCityWorkOrderComponent>(next).Status, Is.EqualTo(SpaceCityWorkStatus.Completed)));
        await Server.WaitPost(() => Server.System<SharedMapSystem>().DeleteMap(map.MapId));
    }

    [Test]
    public async Task ExclusiveClaimAndNoPriorityPreemption()
    {
        var map = await Arena();
        EntityUid a = default, b = default, low = default, high = default;
        await Server.WaitPost(() =>
        {
            a = Worker(map.Grid);
            b = Worker(map.Grid, y: -1.5f);
            foreach (var uid in new[] { a, b })
            {
                Server.System<MovementSpeedModifierSystem>().ChangeBaseSpeed(uid, 1, 1, 20);
            }
            Server.System<SpaceCityGoalSelectorSystem>().SetEnabled(b, false);
            low = Order(map.Grid, 1);
        });
        await RunFor(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<SpaceCityAutonomyComponent>(a).SelectedOrder, Is.EqualTo(low));
            high = Order(map.Grid, 3, y: 2.5f);
            Server.System<SpaceCityGoalSelectorSystem>().SetEnabled(b, true);
            var record = SEntMan.GetComponent<SpaceCityWorkOrderComponent>(low);
            Assert.That(Server.System<SpaceCityWorkOrderSystem>().TryCreate(record.Item, record.Destination, 2, out _, out _), Is.False, "Duplicate physical item accepted");
        });
        await Pair.RunTicksSync(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<SpaceCityAutonomyComponent>(a).SelectedOrder, Is.EqualTo(low), "High priority preempted active work");
        });
        await RunFor(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<SpaceCityWorkOrderComponent>(high).LastCitizenId, Is.EqualTo(SEntMan.GetNetEntity(b))));
        await RunFor(20);
        await Server.WaitAssertion(() =>
        {
            foreach (var uid in new[] { low, high })
            {
                var order = SEntMan.GetComponent<SpaceCityWorkOrderComponent>(uid);
                Assert.That(order.Assignments, Is.EqualTo(1));
                Assert.That(order.Status, Is.EqualTo(SpaceCityWorkStatus.Completed));
            }
            Assert.That(new[] { a, b }.Sum(uid => SEntMan.GetComponent<SpaceCityCitizenComponent>(uid).CompletedTasks), Is.EqualTo(2));
        });
        await Server.WaitPost(() => Server.System<SharedMapSystem>().DeleteMap(map.MapId));
    }

    [TestCase("cancel")]
    [TestCase("stop")]
    [TestCase("reset")]
    [TestCase("disable")]
    [TestCase("manual")]
    [TestCase("item-delete")]
    [TestCase("destination-delete")]
    [TestCase("citizen-delete")]
    [TestCase("map-delete")]
    public async Task ClaimCleanupAndManualPrecedence(string action)
    {
        var map = await Arena();
        EntityUid citizen = default, orderUid = default, item = default, manualDestination = default;
        await Server.WaitPost(() =>
        {
            citizen = Worker(map.Grid);
            orderUid = Order(map.Grid, x: 0.7f, itemPrototype: "Pen");
            item = SEntMan.GetComponent<SpaceCityWorkOrderComponent>(orderUid).Item;
            manualDestination = SEntMan.SpawnAttachedTo("SpaceCityDestination", new EntityCoordinates(map.Grid, -1.5f, 0.5f));
        });
        var held = false;
        for (var i = 0; i < 40 && !held; i++)
        {
            await Pair.RunTicksSync(5);
            await Server.WaitPost(() => held = Server.System<SharedHandsSystem>().IsHolding(citizen, item));
        }
        Assert.That(held, Is.True, "Never reached real pickup");
        EntityUid pending = default;
        await Server.WaitAssertion(() =>
        {
            switch (action)
            {
                case "cancel": Server.System<SpaceCityWorkOrderSystem>().Cancel(orderUid, "Test cancellation"); break;
                case "stop": Server.System<SpaceCityCitizenSystem>().Stop(citizen, true); break;
                case "reset": Server.System<SpaceCityCitizenSystem>().Stop(citizen, false); break;
                case "disable": Server.System<SpaceCityGoalSelectorSystem>().SetEnabled(citizen, false); break;
                case "manual":
                    Assert.That(Server.System<SpaceCityCitizenSystem>().Assign(citizen, manualDestination, null, out _), Is.True);
                    pending = Order(map.Grid, 3, y: 2.5f);
                    var queued = SEntMan.GetComponent<SpaceCityCitizenComponent>(citizen).Tasks.Peek();
                    Assert.That(queued.Destination, Is.EqualTo(manualDestination));
                    Assert.That(queued.WorkOrder, Is.Null);
                    Assert.That(SEntMan.GetComponent<SpaceCityAutonomyComponent>(citizen).SelectedOrder, Is.Null);
                    Assert.That(SEntMan.GetComponent<SpaceCityWorkOrderComponent>(pending).Status, Is.EqualTo(SpaceCityWorkStatus.Available));
                    break;
                case "item-delete": SEntMan.DeleteEntity(item); break;
                case "destination-delete": SEntMan.DeleteEntity(SEntMan.GetComponent<SpaceCityWorkOrderComponent>(orderUid).Destination); break;
                case "citizen-delete": SEntMan.DeleteEntity(citizen); break;
                case "map-delete": Server.System<SharedMapSystem>().DeleteMap(map.MapId); break;
            }
        });
        await RunFor(1);
        await Server.WaitAssertion(() =>
        {
            var order = SEntMan.GetComponent<SpaceCityWorkOrderComponent>(orderUid);
            Assert.That(SpaceCityWorkOrderSystem.IsTerminal(order.Status), Is.True);
            Assert.That(order.AssignedCitizen, Is.Null);
            Assert.That(order.Assignments, Is.EqualTo(1));
            Assert.That(order.LastResult, Is.Not.Empty);
            if (action is "cancel" or "stop" or "reset" or "disable" or "manual" or "destination-delete")
            {
                Assert.That(Server.System<SharedHandsSystem>().IsHolding(citizen, item), Is.False);
                var inventory = Server.System<InventorySystem>();
                Assert.That(inventory.TryGetSlotEntity(citizen, "pocket1", out var pocket) && pocket == item ||
                            inventory.TryGetSlotEntity(citizen, "pocket2", out pocket) && pocket == item, Is.True, "Cancellation did not reuse normal pocket cleanup");
            }
        });
        await RunFor(20);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<SpaceCityWorkOrderComponent>(orderUid).Assignments, Is.EqualTo(1));
            if (action == "manual")
            {
                Assert.That(SEntMan.GetComponent<SpaceCityWorkOrderComponent>(pending).Status, Is.EqualTo(SpaceCityWorkStatus.Completed));
                Assert.That(SEntMan.GetComponent<SpaceCityCitizenComponent>(citizen).CompletedTasks, Is.EqualTo(2));
            }
        });
        if (action != "map-delete")
            await Server.WaitPost(() => Server.System<SharedMapSystem>().DeleteMap(map.MapId));
    }

    [Test]
    public async Task TwoEnabledCitizensCompeteForOneOrder()
    {
        var map = await Arena();
        EntityUid a = default, b = default, orderUid = default;
        await Server.WaitPost(() =>
        {
            a = Worker(map.Grid);
            b = Worker(map.Grid, y: -1.5f);
            orderUid = Order(map.Grid);
        });
        var observedClaim = false;
        for (var i = 0; i < 80; i++)
        {
            await Pair.RunTicksSync(5);
            await Server.WaitAssertion(() =>
            {
                var order = SEntMan.GetComponent<SpaceCityWorkOrderComponent>(orderUid);
                observedClaim |= order.Status == SpaceCityWorkStatus.Claimed;
                Assert.That(order.Assignments, Is.LessThanOrEqualTo(1));
                var owners = new[] { a, b }.Count(uid =>
                    SEntMan.GetComponent<SpaceCityAutonomyComponent>(uid).SelectedOrder == orderUid);
                Assert.That(owners, Is.EqualTo(order.Status == SpaceCityWorkStatus.Claimed ? 1 : 0));
            });
        }
        await Server.WaitAssertion(() =>
        {
            Assert.That(observedClaim, Is.True);
            Assert.That(SEntMan.GetComponent<SpaceCityWorkOrderComponent>(orderUid).Status, Is.EqualTo(SpaceCityWorkStatus.Completed));
            Assert.That(new[] { a, b }.Sum(uid => SEntMan.GetComponent<SpaceCityCitizenComponent>(uid).CompletedTasks), Is.EqualTo(1));
        });
        await Server.WaitPost(() => Server.System<SharedMapSystem>().DeleteMap(map.MapId));
    }

    [Test]
    public async Task UnreachableOrderFailsOnceAndHealthyWorkContinues()
    {
        var map = await Arena();
        EntityUid citizen = default, blocked = default, healthy = default;
        await Server.WaitPost(() =>
        {
            for (var y = -3; y <= 3; y++)
                SEntMan.SpawnAttachedTo("WallSolid", new EntityCoordinates(map.Grid, 4.5f, y + 0.5f));
            citizen = Worker(map.Grid);
            var execution = SEntMan.GetComponent<SpaceCityCitizenComponent>(citizen);
            execution.StuckSeconds = 2;
            execution.LegTimeoutSeconds = 6;
            blocked = Order(map.Grid, 3);
            var item = SEntMan.SpawnAttachedTo("SpaceCityTestItem", new EntityCoordinates(map.Grid, 1.5f, -1.5f));
            var dest = SEntMan.SpawnAttachedTo("SpaceCityDestination", new EntityCoordinates(map.Grid, 2.5f, -1.5f));
            Assert.That(Server.System<SpaceCityWorkOrderSystem>().TryCreate(item, dest, 1, out healthy, out _), Is.True);
        });
        await RunFor(35);
        await Server.WaitAssertion(() =>
        {
            var order = SEntMan.GetComponent<SpaceCityWorkOrderComponent>(blocked);
            Assert.That(order.Status, Is.EqualTo(SpaceCityWorkStatus.Failed), order.LastResult);
            Assert.That(order.Assignments, Is.EqualTo(1));
            Assert.That(order.AssignedCitizen, Is.Null);
            Assert.That(SEntMan.GetComponent<SpaceCityWorkOrderComponent>(healthy).Status, Is.EqualTo(SpaceCityWorkStatus.Completed));
            Assert.That(SEntMan.GetComponent<SpaceCityCitizenComponent>(citizen).FailedTasks, Is.EqualTo(1));
        });
        await RunFor(10);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<SpaceCityWorkOrderComponent>(blocked).Assignments, Is.EqualTo(1)));
        await Server.WaitPost(() => Server.System<SharedMapSystem>().DeleteMap(map.MapId));
    }
}
