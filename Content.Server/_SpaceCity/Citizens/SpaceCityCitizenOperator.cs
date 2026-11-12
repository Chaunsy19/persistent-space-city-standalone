using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._SpaceCity.Citizens;

/// <summary>
/// Runs through the existing HTN scheduler. All per-citizen state lives on the entity,
/// never on this shared prototype operator. Steering uses SS14's normal pathfinder.
/// </summary>
public sealed partial class SpaceCityCitizenOperator : HTNOperator
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private SpaceCityCitizenSystem _citizens = default!;

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_entities.HasComponent<SpaceCityCitizenComponent>(owner))
            return HTNOperatorStatus.Failed;

        _citizens.Step(owner);
        return HTNOperatorStatus.Continuing;
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        _citizens.Suspend(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
    }
}
