using Robust.Shared.Map;

namespace Content.Server._SpaceCity.Citizens;

/// <summary>Server-only state for the milestone's bounded, explicitly assigned delivery demo.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class SpaceCityCitizenComponent : Component
{
    [DataField] public int MaxAttempts = 2;
    [DataField] public float StuckSeconds = 6f;
    [DataField] public float LegTimeoutSeconds = 30f;

    [ViewVariables] public CitizenActivity Activity = CitizenActivity.Idle;
    [ViewVariables] public readonly Queue<CitizenTask> Tasks = new();
    [ViewVariables] public CitizenTask? CurrentTask;
    [ViewVariables] public string LastResult = "Waiting for an assigned task";
    [ViewVariables] public int Attempts;
    [ViewVariables] public int CompletedTasks;
    [ViewVariables] public int FailedTasks;
    [ViewVariables] public bool Carrying;
    [ViewVariables] public bool Stopped;
    public CitizenActivity RetryActivity;
    public EntityCoordinates LastPosition;
    [AutoPausedField] public TimeSpan NextUpdate;
    [AutoPausedField] public TimeSpan LastProgress;
    [AutoPausedField] public TimeSpan LegDeadline;
}

public enum CitizenActivity : byte
{
    Idle,
    ToItem,
    ToDestination,
    RetryWait,
    Stopped,
}

public readonly record struct CitizenTask(EntityUid Destination, EntityUid? Item, EntityUid? WorkOrder = null);

public enum CitizenTaskOutcome : byte { Completed, Failed, Cancelled }

/// <summary>Execution outcomes are published after cleanup and clearing the task state.</summary>
[ByRefEvent]
public readonly record struct CitizenTaskResolvedEvent(CitizenTask Task, CitizenTaskOutcome Outcome, string Reason);

/// <summary>Raised only for a validated manual assignment, before it enters the queue.</summary>
[ByRefEvent]
public readonly record struct CitizenManualTaskEvent;

[ByRefEvent]
public readonly record struct CitizenRemovedEvent;
