using Content.Shared.Body;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._KS14.Klovnmed.OrganHealth;

/// <summary>
///     Added to a body when an organ with <see cref="KsOrganHealthComponent"/> goes into it. The body then needs an organ
///         of that category, and one taken out counts as missing. Bodies that never had one, like a mothroach without a
///         liver, are left alone.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(KsOrganHealthSystem))]
public sealed partial class KsOrganHealthBodyComponent : Component
{
    /// <summary>
    ///     Every category of organ this body has held, with the prototype of the last one, whose
    ///         <see cref="KsOrganHealthComponent.MissingEffects"/> apply while it is gone.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, EntProtoId> ExpectedOrgans = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;
}
