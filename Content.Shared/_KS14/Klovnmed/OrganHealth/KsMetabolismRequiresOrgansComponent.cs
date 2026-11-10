using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._KS14.Klovnmed.OrganHealth;

/// <summary>
///     On a metabolizer organ whose reagents also need other organs working, as the heart's need the liver. While one of
///         them is missing or failed, only <see cref="Content.Shared.Chemistry.Reagent.ReagentPrototype.KsSelfConsuming"/>
///         reagents are metabolized. A working one with a <see cref="KsOrganHealthComponent.ToxinFilter"/> removes small
///         amounts of toxin first.
/// </summary>
[RegisterComponent]
public sealed partial class KsMetabolismRequiresOrgansComponent : Component
{
    [DataField(required: true)]
    public HashSet<ProtoId<OrganCategoryPrototype>> Organs = new();
}
