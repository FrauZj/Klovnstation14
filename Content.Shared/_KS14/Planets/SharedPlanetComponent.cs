using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._KS14.Planets;

[NetworkedComponent]
public abstract partial class SharedPlanetComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("stage")]
    public PlanetStage Stage = PlanetStage.Added;
}

[Serializable, NetSerializable]
public sealed class PlanetComponentState : ComponentState
{
    public PlanetStage Stage;
}
