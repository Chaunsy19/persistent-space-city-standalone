using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using Content.Server._SpaceCity.Citizens;
using Content.Server.NPC.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Gravity;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.UnitTesting.Pool;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests._SpaceCity;

/// <summary>Real server HTN/steering/physics/hands, with no attached player driving the citizen.</summary>
[TestFixture]
public sealed class CitizenTests : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: SpaceCityTestDoor
  parent: Airlock
  components:
  - type: ApcPowerReceiver
    needsPower: false
  - type: AccessReader
    access: [[Maintenance]]
    containerAccessProvider: null
""";

    private async Task<TestMapData> Arena()
    {
        // The global integration harness disables graph generation for unrelated tests.
        await OverrideCVar(Side.Server, CCVars.PathfindingEnabled, true);
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = -2; x <= 10; x++)
            for (var y = -3; y <= 3; y++)
                maps.SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
            SEntMan.EnsureComponent<GravityComponent>(map.Grid).Enabled = true;
            SEntMan.GetComponent<GravityComponent>(map.Grid).Inherent = true;
        });
        await Pair.RunTicksSync(60);
        await Server.WaitAssertion(() =>
        {
            var pathfinding = Server.System<Content.Server.NPC.Pathfinding.PathfindingSystem>();
            Assert.That(pathfinding.GetPoly(new EntityCoordinates(map.Grid, 0.5f, 0.5f)), Is.Not.Null, "Missing start navmesh");
            Assert.That(pathfinding.GetPoly(new EntityCoordinates(map.Grid, 7.5f, 0.5f)), Is.Not.Null, "Missing destination navmesh");
        });
        return map;
    }

    private async Task RunFor(int seconds)
    {
        // Batches allow asynchronous path jobs to finish between simulation steps.
        for (var i = 0; i < seconds * 4; i++)
            await Pair.RunTicksSync(15);
    }

    [Test]
    public async Task DeliveryUsesRealHandsWithoutPlayers()
    {
        var map = await Arena();
        EntityUid citizen = default, item = default, destination = default;
        await Server.WaitAssertion(() =>
        {
            citizen = SEntMan.SpawnAttachedTo("SpaceCityCitizen", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            item = SEntMan.SpawnAttachedTo("SpaceCityTestItem", new EntityCoordinates(map.Grid, 2.5f, 0.5f));
            destination = SEntMan.SpawnAttachedTo("SpaceCityDestination", new EntityCoordinates(map.Grid, 7.5f, 0.5f));
            SEntMan.SpawnAttachedTo("WallSolid", new EntityCoordinates(map.Grid, 4.5f, 0.5f));
            Assert.That(Server.System<SpaceCityCitizenSystem>().Assign(citizen, destination, item, out var reason), Is.True, reason);
        });
        var observedPickup = false;
        var observedDetour = false;
        for (var i = 0; i < 60; i++)
        {
            await Pair.RunTicksSync(15);
            await Server.WaitPost(() => observedPickup |= Server.System<SharedHandsSystem>().IsHolding(citizen, item));
            await Server.WaitPost(() => observedDetour |= Math.Abs(SEntMan.GetComponent<TransformComponent>(citizen).LocalPosition.Y - 0.5f) > 0.6f);
        }
        await Server.WaitAssertion(() =>
        {
            var state = SEntMan.GetComponent<SpaceCityCitizenComponent>(citizen);
            Assert.That(observedPickup, Is.True, "Item was never actually held");
            Assert.That(observedDetour, Is.True, "Citizen did not navigate around the solid obstacle");
            Assert.That(state.CompletedTasks, Is.EqualTo(1), state.LastResult);
            Assert.That(state.FailedTasks, Is.Zero, state.LastResult);
            Assert.That(Server.System<SharedHandsSystem>().IsHolding(citizen, item), Is.False);
            var delta = SEntMan.GetComponent<TransformComponent>(item).LocalPosition - new Vector2(7.5f, 0.5f);
            Assert.That(delta.Length(), Is.LessThan(1.5f));
        });
        await Server.WaitPost(() => Server.System<SharedMapSystem>().DeleteMap(map.MapId));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task DoorTraversalRespectsAccess(bool authorized)
    {
        var map = await Arena();
        EntityUid citizen = default, door = default;
        await Server.WaitAssertion(() =>
        {
            // A full partition prevents pathfinding around the access-controlled door.
            for (var y = -3; y <= 3; y++)
                if (y != 0)
                    SEntMan.SpawnAttachedTo("WallSolid", new EntityCoordinates(map.Grid, 4.5f, y + 0.5f));
            door = SEntMan.SpawnAttachedTo("SpaceCityTestDoor", new EntityCoordinates(map.Grid, 4.5f, 0.5f));
            citizen = SEntMan.SpawnAttachedTo("SpaceCityCitizen", new EntityCoordinates(map.Grid, 1.5f, 0.5f));
            var state = SEntMan.GetComponent<SpaceCityCitizenComponent>(citizen);
            state.StuckSeconds = 2;
            state.LegTimeoutSeconds = 10;
            var inventory = Server.System<InventorySystem>();
            Assert.That(inventory.TryGetSlotEntity(citizen, "id", out var card), Is.True);
            Assert.That(Server.System<SharedAccessSystem>().TrySetTags(card!.Value,
                authorized ? new[] { new Robust.Shared.Prototypes.ProtoId<Content.Shared.Access.AccessLevelPrototype>("Maintenance") } : []), Is.True);
            var destination = SEntMan.SpawnAttachedTo("SpaceCityDestination", new EntityCoordinates(map.Grid, 7.5f, 0.5f));
            Assert.That(Server.System<AccessReaderSystem>().IsAllowed(citizen, door), Is.EqualTo(authorized));
            Assert.That(Server.System<SpaceCityCitizenSystem>().Assign(citizen, destination, null, out _), Is.True);
            if (!authorized)
            {
                var recovery = SEntMan.SpawnAttachedTo("SpaceCityDestination", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
                Assert.That(Server.System<SpaceCityCitizenSystem>().Assign(citizen, recovery, null, out _), Is.True);
            }
        });
        var opened = false;
        for (var i = 0; i < 80; i++)
        {
            await Pair.RunTicksSync(15);
            await Server.WaitPost(() => opened |= SEntMan.GetComponent<DoorComponent>(door).State != DoorState.Closed);
        }
        await Server.WaitAssertion(() =>
        {
            var state = SEntMan.GetComponent<SpaceCityCitizenComponent>(citizen);
            Assert.That(opened, Is.EqualTo(authorized));
            Assert.That(state.CompletedTasks, Is.EqualTo(1), state.LastResult);
            Assert.That(state.FailedTasks, Is.EqualTo(authorized ? 0 : 1), state.LastResult);
            Assert.That(state.CurrentTask, Is.Null);
            Assert.That(SEntMan.HasComponent<NPCSteeringComponent>(citizen), Is.False);
            if (!authorized)
                Assert.That(SEntMan.GetComponent<TransformComponent>(citizen).LocalPosition.X, Is.LessThan(4f));
        });
        await Server.WaitPost(() => Server.System<SharedMapSystem>().DeleteMap(map.MapId));
    }

    [Test]
    public async Task RemovedTargetStopResetAndQueueLimit()
    {
        var map = await Arena();
        EntityUid citizen = default, target = default;
        await Server.WaitAssertion(() =>
        {
            citizen = SEntMan.SpawnAttachedTo("SpaceCityCitizen", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            target = SEntMan.SpawnAttachedTo("SpaceCityDestination", new EntityCoordinates(map.Grid, 9.5f, 0.5f));
            var system = Server.System<SpaceCityCitizenSystem>();
            for (var i = 0; i < 8; i++)
                Assert.That(system.Assign(citizen, target, null, out _), Is.True);
            Assert.That(system.Assign(citizen, target, null, out _), Is.False);
            system.Stop(citizen, true);
            Assert.That(system.Assign(citizen, target, null, out _), Is.False);
            Assert.That(SEntMan.GetComponent<SpaceCityCitizenComponent>(citizen).Tasks, Is.Empty);
            system.Stop(citizen, false);
            Assert.That(system.Assign(citizen, target, null, out _), Is.True);
        });
        await RunFor(1);
        await Server.WaitPost(() => SEntMan.DeleteEntity(target));
        await RunFor(2);
        await Server.WaitAssertion(() =>
        {
            var state = SEntMan.GetComponent<SpaceCityCitizenComponent>(citizen);
            Assert.That(state.FailedTasks, Is.EqualTo(1));
            Assert.That(state.CurrentTask, Is.Null);
            Assert.That(SEntMan.HasComponent<NPCSteeringComponent>(citizen), Is.False);
            SEntMan.DeleteEntity(citizen);
        });
        await RunFor(1);
        await Server.WaitPost(() => Server.System<SharedMapSystem>().DeleteMap(map.MapId));
    }

    [Test]
    public async Task TwoClientsObserveTheSameDelivery()
    {
        var map = await Arena();
        await Pair.Connect();
        // Borrow a second independent client instance; its own server is unused after initialization.
        await using var other = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Destructive = true });
        await other.Client.Connect(Server);

        async Task Pump(int ticks)
        {
            await Pair.RunTicksSync(ticks);
            await other.Client.WaitRunTicks(ticks);
            await other.Client.WaitIdleAsync();
        }

        for (var i = 0; i < 30; i++)
            await Pump(5);

        EntityUid citizen = default, item = default, destination = default, door = default;
        NetEntity citizenNet = default, itemNet = default, doorNet = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.PlayerMan.Sessions.Length, Is.EqualTo(2));
            foreach (var session in Server.PlayerMan.Sessions)
            {
                var observer = SEntMan.SpawnAttachedTo("MobObserver", new EntityCoordinates(map.Grid, 3.5f, -1.5f));
                Server.PlayerMan.SetAttachedEntity(session, observer);
            }
            for (var y = -3; y <= 3; y++)
                if (y != 0)
                    SEntMan.SpawnAttachedTo("WallSolid", new EntityCoordinates(map.Grid, 4.5f, y + 0.5f));
            door = SEntMan.SpawnAttachedTo("SpaceCityTestDoor", new EntityCoordinates(map.Grid, 4.5f, 0.5f));
            citizen = SEntMan.SpawnAttachedTo("SpaceCityCitizen", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            item = SEntMan.SpawnAttachedTo("SpaceCityTestItem", new EntityCoordinates(map.Grid, 2.5f, 0.5f));
            destination = SEntMan.SpawnAttachedTo("SpaceCityDestination", new EntityCoordinates(map.Grid, 7.5f, 0.5f));
            Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(citizen, "id", out var card), Is.True);
            Server.System<SharedAccessSystem>().TrySetTags(card!.Value, ["Maintenance"]);
            citizenNet = SEntMan.GetNetEntity(citizen);
            itemNet = SEntMan.GetNetEntity(item);
            doorNet = SEntMan.GetNetEntity(door);
        });
        for (var i = 0; i < 20; i++)
            await Pump(5);
        await Server.WaitAssertion(() => Assert.That(Server.System<SpaceCityCitizenSystem>().Assign(citizen, destination, item, out var reason), Is.True, reason));

        var held = new bool[2];
        var opened = new bool[2];
        var moved = new bool[2];
        var clients = new[] { Client, other.Client };
        for (var tick = 0; tick < 150; tick++)
        {
            await Pump(5);
            for (var index = 0; index < 2; index++)
            {
                var observerIndex = index;
                var client = clients[index];
                await client.WaitPost(() =>
                {
                    var entities = client.EntMan;
                    if (!entities.TryGetEntity(citizenNet, out var c) || c == null ||
                        !entities.TryGetEntity(itemNet, out var it) || it == null ||
                        !entities.TryGetEntity(doorNet, out var d) || d == null)
                        return;
                    held[observerIndex] |= client.System<SharedHandsSystem>().IsHolding(c.Value, it.Value);
                    opened[observerIndex] |= entities.GetComponent<DoorComponent>(d.Value).State != DoorState.Closed;
                    moved[observerIndex] |= entities.GetComponent<TransformComponent>(c.Value).LocalPosition.X > 5f;
                });
            }
        }

        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<SpaceCityCitizenComponent>(citizen).CompletedTasks, Is.EqualTo(1)));
        for (var index = 0; index < 2; index++)
        {
            Assert.That(held[index], Is.True, $"Client {index} never received held-item state");
            Assert.That(opened[index], Is.True, $"Client {index} never received the open door");
            Assert.That(moved[index], Is.True, $"Client {index} never saw movement through the door");
            var client = clients[index];
            await client.WaitAssertion(() =>
            {
                var c = client.EntMan.GetEntity(citizenNet);
                var it = client.EntMan.GetEntity(itemNet);
                Assert.That(client.System<SharedHandsSystem>().IsHolding(c, it), Is.False);
                Assert.That(client.EntMan.GetComponent<TransformComponent>(it).LocalPosition.X, Is.GreaterThan(6f));
                Assert.That(client.System<InventorySystem>().TryGetSlotEntity(c, "jumpsuit", out _), Is.True);
            });
        }
        await other.Client.WaitPost(() => other.Client.Resolve<IClientNetManager>().ClientDisconnect("Verification complete"));
        for (var i = 0; i < 5; i++)
            await Pump(5);
        await Pair.Disconnect();
        await Server.WaitPost(() => Server.System<SharedMapSystem>().DeleteMap(map.MapId));
        await other.CleanReturnAsync();
    }
}
