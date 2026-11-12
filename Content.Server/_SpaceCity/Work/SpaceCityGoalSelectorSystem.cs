using Content.Server._SpaceCity.Citizens;
using Content.Server.NPC.HTN;
using Content.Shared.NPC;
using Robust.Shared.Timing;

namespace Content.Server._SpaceCity.Work;

/// <summary>Decides WHAT work to select; the existing HTN delivery executor decides HOW.</summary>
public sealed partial class SpaceCityGoalSelectorSystem : EntitySystem
{
    [Dependency] private SpaceCityWorkOrderSystem _work = default!;
    [Dependency] private IGameTiming _timing = default!;
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceCityCitizenComponent, CitizenManualTaskEvent>(OnManualTask);
        SubscribeLocalEvent<SpaceCityAutonomyComponent, ComponentShutdown>(OnAutonomyShutdown);
    }

    public void SetEnabled(EntityUid uid, bool enabled)
    {
        if (!HasComp<SpaceCityCitizenComponent>(uid))
            return;
        var autonomy = EnsureComp<SpaceCityAutonomyComponent>(uid);
        autonomy.Enabled = enabled;
        autonomy.NextSelection = _timing.CurTime;
        autonomy.LastDecision = enabled ? "Waiting for eligible work" : "Autonomous selection disabled";
        if (!enabled)
            _work.CancelForCitizen(uid, "Autonomous selection disabled");
    }

    private void OnManualTask(EntityUid uid, SpaceCityCitizenComponent component, ref CitizenManualTaskEvent args)
    {
        _work.CancelForCitizen(uid, "Manual task took precedence");
    }

    private void OnAutonomyShutdown(EntityUid uid, SpaceCityAutonomyComponent component, ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(uid))
            _work.CancelForCitizen(uid, "Autonomy component removed");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        var query = EntityQueryEnumerator<SpaceCityAutonomyComponent, SpaceCityCitizenComponent, ActiveNPCComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var autonomy, out var execution, out _, out var htn))
        {
            if (!autonomy.Enabled || execution.Stopped || !htn.Enabled || autonomy.SelectedOrder != null ||
                execution.CurrentTask != null || execution.Tasks.Count != 0 ||
                _timing.CurTime < autonomy.NextSelection)
                continue;
            autonomy.NextSelection = _timing.CurTime + TimeSpan.FromSeconds(2);
            if (_work.TryClaim(uid, out var order))
            {
                autonomy.LastDecision = $"Selected delivery order {GetNetEntity(order)}";
                Log.Info($"Citizen {GetNetEntity(uid)} selected delivery order {GetNetEntity(order)}");
            }
            else
                autonomy.LastDecision = "Idle: no eligible work";
        }
    }
}
