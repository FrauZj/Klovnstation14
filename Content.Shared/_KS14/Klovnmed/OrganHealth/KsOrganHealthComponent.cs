using Content.Shared.EntityEffects;
using Robust.Shared.GameStates;

namespace Content.Shared._KS14.Klovnmed.OrganHealth;

/// <summary>
///     An organ that can be damaged, heals on its own while only lightly damaged, and fails at <see cref="MaxHealth"/>.
///         A failed organ stops metabolizing, and runs through its <see cref="FailureStages"/> while it stays failed.
///         Ported from tgstation's organs (code/modules/surgery/organs/_organ.dm).
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(KsOrganHealthSystem))]
public sealed partial class KsOrganHealthComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float Damage;

    /// <summary>
    ///     The organ fails once <see cref="Damage"/> reaches this.
    /// </summary>
    [DataField]
    public float MaxHealth = 100f;

    /// <summary>
    ///     Damage over which the organ reads as mildly damaged. tg: <c>low_threshold</c>.
    /// </summary>
    [DataField]
    public float LowThreshold = 10f;

    /// <summary>
    ///     Damage over which the organ reads as severely damaged. tg: <c>high_threshold</c>.
    /// </summary>
    [DataField]
    public float HighThreshold = 45f;

    /// <summary>
    ///     Damage healed per second on an empty stomach, up to five times this when well fed. tg:
    ///         <c>healing_factor * maxHealth</c>.
    /// </summary>
    [DataField]
    public float Healing = 0.05f;

    /// <summary>
    ///     The organ only heals on its own while its damage is below this. Past it, it needs medicine.
    /// </summary>
    [DataField]
    public float HealingMaxDamage = 45f;

    /// <summary>
    ///     Robotic organs never heal on their own.
    /// </summary>
    [DataField]
    public bool Robotic;

    /// <summary>
    ///     How long the organ has been failing, in seconds. It picks the failure stage, and counts back down at half speed
    ///         once the organ works again.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float FailureTime;

    /// <summary>
    ///     Whether this organ, failed, stops metabolizing everything but self-consuming reagents. tg's liver does; its
    ///         heart and stomach keep their metabolism and do other things.
    /// </summary>
    [DataField]
    public bool FreezeMetabolismWhenFailing;

    /// <summary>
    ///     Makes the organ filter toxins, as tg's liver does: small amounts are removed before they do anything to an
    ///         organ that requires this one, and more than that damages this one. Null for organs that don't.
    /// </summary>
    /// <seealso cref="KsMetabolismRequiresOrgansComponent"/>
    [DataField]
    public KsToxinFilter? ToxinFilter;

    /// <summary>
    ///     What a damaged organ does to its body, failed or not: each applies every second while damage is at or over its
    ///         <see cref="KsOrganSymptom.MinDamage"/>.
    /// </summary>
    [DataField]
    public List<KsOrganSymptom> Symptoms = new();

    /// <summary>
    ///     What a failed organ does to its body, by how long it has been failing.
    /// </summary>
    [DataField]
    public List<KsOrganFailureStage> FailureStages = new();

    /// <summary>
    ///     Applied to the body every second while an organ like this is missing from it. Kept on the organ, and read from
    ///         its prototype, since a missing organ isn't there to ask.
    /// </summary>
    [DataField]
    public EntityEffect[] MissingEffects = [];

    /// <summary>
    ///     Shown to the owner as damage crosses <see cref="LowThreshold"/>, <see cref="HighThreshold"/> and
    ///         <see cref="MaxHealth"/>, either way. tg: <c>low_threshold_passed</c> and the like.
    /// </summary>
    [DataField]
    public LocId? LowThresholdPassed, HighThresholdPassed, NowFailing, NowFixed, HighThresholdCleared, LowThresholdCleared;

    /// <summary>
    ///     The failure stage last applied, or -1 if none has been yet. Lets each stage's start effects run once.
    /// </summary>
    [ViewVariables]
    public int CurrentStage = -1;

    [ViewVariables]
    public bool Failing => Damage >= MaxHealth;
}

/// <summary>
///     How an organ filters reagents that are <see cref="Content.Shared.Chemistry.Reagent.ReagentPrototype.KsToxin"/>.
///         See <see cref="KsOrganHealthComponent.ToxinFilter"/>.
/// </summary>
[DataDefinition]
public sealed partial class KsToxinFilter
{
    /// <summary>
    ///     How many units of each toxin, in the blood and stomach together, a healthy organ removes before they do
    ///         anything. Shrinks with damage, to 0 once the organ fails. tg: <c>toxTolerance</c>.
    /// </summary>
    [DataField]
    public float Tolerance = 3f;

    /// <summary>
    ///     Divides the damage the organ takes from toxins over its tolerance. tg: <c>liver_resistance</c>.
    /// </summary>
    [DataField]
    public float Resistance = 1f;

    /// <summary>
    ///     The most damage toxins do to the organ in a second. tg: <c>MAX_TOXIN_LIVER_DAMAGE</c>.
    /// </summary>
    [DataField]
    public float MaxDamage = 2f;

    /// <summary>
    ///     Shown to the owner now and then while toxins are damaging the organ and it is past its low threshold.
    /// </summary>
    [DataField]
    public LocId? PainMessage;
}

/// <summary>
///     Part of a damaged organ's <see cref="KsOrganHealthComponent.Symptoms"/>.
/// </summary>
[DataDefinition]
public sealed partial class KsOrganSymptom
{
    [DataField(required: true)]
    public float MinDamage;

    /// <summary>
    ///     Applied to the body every second while the organ's damage is at or over <see cref="MinDamage"/>.
    /// </summary>
    [DataField(required: true)]
    public EntityEffect[] Effects = [];
}

/// <summary>
///     Part of a failing organ's <see cref="KsOrganHealthComponent.FailureStages"/>.
/// </summary>
[DataDefinition]
public sealed partial class KsOrganFailureStage
{
    /// <summary>
    ///     Seconds of failure after which this stage starts. It lasts until the next one does.
    /// </summary>
    [DataField]
    public float Time;

    /// <summary>
    ///     Applied to the body once, as the stage starts.
    /// </summary>
    [DataField]
    public EntityEffect[] Start = [];

    /// <summary>
    ///     Applied to the body every second during the stage.
    /// </summary>
    [DataField]
    public EntityEffect[] Effects = [];

    /// <summary>
    ///     Shown when examining the body during this stage.
    /// </summary>
    [DataField]
    public LocId? Examine;
}
