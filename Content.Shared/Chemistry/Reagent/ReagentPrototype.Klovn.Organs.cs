using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.Reagent;

public sealed partial class ReagentPrototype
{
    /// <summary>
    ///     Whether this reagent still works when an organ it would need is failed or missing: when the metabolizing organ
    ///         has failed, or one that its metabolism requires has. tg: <c>self_consuming</c>.
    /// </summary>
    /// <seealso cref="Content.Shared._KS14.Klovnmed.OrganHealth.KsMetabolismRequiresOrgansComponent"/>
    [DataField("ksSelfConsuming")]
    public bool KsSelfConsuming;

    /// <summary>
    ///     Organ damage per unit of this reagent that the stomach passes on to the blood. Swallowed, it goes through these
    ///         organs first, as anything swallowed goes through the liver; injected, it never does.
    /// </summary>
    [DataField("ksIngestedOrganDamage")]
    public Dictionary<ProtoId<OrganCategoryPrototype>, float>? KsIngestedOrganDamage;

    /// <summary>
    ///     Makes this reagent a toxin, tg's <c>/datum/reagent/toxin</c>: an organ with a toxin filter, the liver, removes
    ///         small amounts of it before it does anything, and more than that damages the organ. Null for reagents that
    ///         aren't toxins.
    /// </summary>
    /// <seealso cref="Content.Shared._KS14.Klovnmed.OrganHealth.KsOrganHealthComponent.ToxinFilter"/>
    [DataField("ksToxin")]
    public KsToxinData? KsToxin;
}

/// <summary>
///     How a toxin treats the organ that filters it. See <see cref="ReagentPrototype.KsToxin"/>.
/// </summary>
[DataDefinition]
public sealed partial class KsToxinData
{
    /// <summary>
    ///     How hard this toxin is on the filtering organ while it is over the organ's tolerance. 15u at a power of 1 does
    ///         1 damage a second. 0 for toxins that are only filtered and never damage the organ. tg: <c>toxpwr</c>.
    /// </summary>
    [DataField]
    public float Power;

    /// <summary>
    ///     Multiplies the organ damage of <see cref="Power"/>. tg: <c>liver_damage_multiplier</c>.
    /// </summary>
    [DataField]
    public float DamageMultiplier = 1f;

    /// <summary>
    ///     Multiplies how much of this toxin the organ filters out. tg: <c>liver_tolerance_multiplier</c>.
    /// </summary>
    [DataField]
    public float ToleranceMultiplier = 1f;
}
