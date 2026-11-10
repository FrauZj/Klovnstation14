using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Metabolism;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._KS14.Klovnmed.OrganHealth;

/// <summary>
///     Raised on a metabolizer organ before it applies a reagent's effects, so the reagent can be held back or thrown away.
/// </summary>
[ByRefEvent]
public record struct KsMetabolizeReagentAttemptEvent(EntityUid Body, ReagentPrototype Reagent)
{
    public KsMetabolizeReagentOutcome Outcome = KsMetabolizeReagentOutcome.Metabolize;
}

public enum KsMetabolizeReagentOutcome : byte
{
    /// <summary>
    ///     Metabolize it as normal.
    /// </summary>
    Metabolize,

    /// <summary>
    ///     Leave it where it is with no effects, and don't count it towards the organ's reagent cap.
    /// </summary>
    Freeze,

    /// <summary>
    ///     Remove it as if metabolized, with no effects, and don't count it towards the organ's reagent cap.
    /// </summary>
    Filter,
}

/// <summary>
///     Raised on a body when one of its metabolizer organs passes a reagent it doesn't metabolize on to the next solution,
///         as the stomach does to the blood. <see cref="Amount"/> is what left the organ, before any is lost on the way.
/// </summary>
[ByRefEvent]
public readonly record struct KsReagentTransferredEvent(ProtoId<MetabolismStagePrototype> Stage, ReagentPrototype Reagent, FixedPoint2 Amount);

/// <summary>
///     An organ that isn't healthy, as a health analyzer shows it.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct KsOrganHealthEntry(string Category, KsOrganHealthStatus Status);

/// <summary>
///     How an organ reads on a health analyzer.
/// </summary>
[Serializable, NetSerializable]
public enum KsOrganHealthStatus : byte
{
    Healthy,
    Mild,
    Severe,
    Failing,
    Missing,
}
