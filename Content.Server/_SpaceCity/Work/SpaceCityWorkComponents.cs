namespace Content.Server._SpaceCity.Work;

/// <summary>Opt-in selection state; execution remains on SpaceCityCitizenComponent.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class SpaceCityAutonomyComponent : Component
{
    [DataField] public bool Enabled;
    [ViewVariables] public EntityUid? SelectedOrder;
    [ViewVariables] public string LastDecision = "Autonomous selection disabled";
    [AutoPausedField] public TimeSpan NextSelection;
}

public enum SpaceCityWorkType : byte { ItemDelivery }
public enum SpaceCityWorkStatus : byte { Available, Claimed, Completed, Failed, Cancelled }
public enum SpaceCityWorkPriority : byte { Low = 1, Normal = 2, High = 3 }

/// <summary>Session-local nullspace entity: failure records survive target/map deletion.</summary>
[RegisterComponent]
public sealed partial class SpaceCityWorkOrderComponent : Component
{
    [ViewVariables] public SpaceCityWorkType Type = SpaceCityWorkType.ItemDelivery;
    [ViewVariables] public EntityUid Item;
    [ViewVariables] public EntityUid Destination;
    [ViewVariables] public EntityUid Grid;
    [ViewVariables] public NetEntity ItemId;
    [ViewVariables] public NetEntity DestinationId;
    [ViewVariables] public SpaceCityWorkPriority Priority;
    [ViewVariables] public long CreationSequence;
    [ViewVariables] public SpaceCityWorkStatus Status = SpaceCityWorkStatus.Available;
    [ViewVariables] public EntityUid? AssignedCitizen;
    [ViewVariables] public NetEntity? LastCitizenId;
    [ViewVariables] public int Assignments;
    [ViewVariables] public string LastResult = "Available";
    [ViewVariables] public TimeSpan? ResolvedAt;
}
