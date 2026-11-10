using Content.Shared.Body;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._KS14.Klovnmed.OrganHealth;

/// <summary>
///     A status effect that holds off what failed or missing organs do to the body. It heals nothing, and reagents a
///         failed organ would metabolize stay frozen. tg: <c>TRAIT_STABLELIVER</c>, <c>TRAIT_STABLEHEART</c>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class KsStableOrgansStatusEffectComponent : Component
{
    /// <summary>
    ///     The organs held off. Null for all of them.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<OrganCategoryPrototype>>? Organs;
}
