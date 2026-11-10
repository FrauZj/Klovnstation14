using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityConditions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._KS14.EntityConditions.Conditions;

/// <summary>
///     Returns true if the entity's damage of the condition's groups and types, added together, is within a minimum and
///         maximum. tg's medicine often heals by how hurt the patient is, as salicylic acid only heals bruising quickly
///         once it is severe.
/// </summary>
/// <remarks>
///     Reads damage through <see cref="DamageableSystem"/>'s numeric queries, which upstream has marked obsolete with no
///         replacement yet. Port this to the replacement when an upstream merge brings one.
/// </remarks>
/// <inheritdoc cref="EntityConditionSystem{T, TCondition}"/>
public sealed partial class KsDamageEntityConditionSystem : EntityConditionSystem<DamageableComponent, KsDamageCondition>
{
    [Dependency] private DamageableSystem _damageableSystem = default!;

    protected override void Condition(Entity<DamageableComponent> entity, ref EntityConditionEvent<KsDamageCondition> args)
    {
        var damage = FixedPoint2.Zero;

        var damagePerGroup = _damageableSystem.GetDamagePerGroup(entity.AsNullable());
        foreach (var group in args.Condition.Groups)
        {
            if (damagePerGroup.TryGetValue(group, out var groupDamage))
                damage += groupDamage;
        }

        var damagePerType = _damageableSystem.GetAllDamage(entity.AsNullable()).DamageDict;
        foreach (var type in args.Condition.Types)
        {
            if (damagePerType.TryGetValue(type, out var typeDamage))
                damage += typeDamage;
        }

        if (damage >= args.Condition.Min && damage <= args.Condition.Max)
            args.Result = true;
    }
}

/// <inheritdoc cref="EntityCondition"/>
public sealed partial class KsDamageCondition : EntityConditionBase<KsDamageCondition>
{
    [DataField]
    public List<ProtoId<DamageGroupPrototype>> Groups = new();

    [DataField]
    public List<ProtoId<DamageTypePrototype>> Types = new();

    [DataField]
    public FixedPoint2 Min = FixedPoint2.Zero;

    [DataField]
    public FixedPoint2 Max = FixedPoint2.MaxValue;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        var names = new List<string>();
        foreach (var group in Groups)
        {
            if (prototype.Resolve(group, out var groupPrototype))
                names.Add(groupPrototype.LocalizedName);
        }

        foreach (var type in Types)
        {
            if (prototype.Resolve(type, out var typePrototype))
                names.Add(typePrototype.LocalizedName);
        }

        return Loc.GetString("entity-condition-guidebook-ks-damage",
            ("damage", string.Join(", ", names)),
            ("max", Max == FixedPoint2.MaxValue ? (float)int.MaxValue : (float)Max),
            ("min", (float)Min));
    }
}
