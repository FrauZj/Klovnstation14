using Content.Shared._KS14.Klovnmed.OrganHealth;
using Content.Shared.Body;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._KS14.EntityEffects.Effects;

/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class KsAdjustOrganDamageEntityEffectSystem : EntityEffectSystem<BodyComponent, KsAdjustOrganDamage>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private KsOrganHealthSystem _organHealth = default!;

    private readonly List<ProtoId<OrganCategoryPrototype>> _present = new();

    protected override void Effect(Entity<BodyComponent> entity, ref EntityEffectEvent<KsAdjustOrganDamage> args)
    {
        var amount = args.Effect.Amount * args.Scale;

        if (args.Effect.RandomOrgans is { } randomOrgans)
        {
            _present.Clear();
            foreach (var organ in randomOrgans)
            {
                if (_organHealth.TryGetOrgan(entity, organ, out _))
                    _present.Add(organ);
            }

            if (_present.Count > 0)
                _organHealth.TryChangeOrganDamage(entity, _random.Pick(_present), amount);

            return;
        }

        if (args.Effect.Organ is { } single)
            _organHealth.TryChangeOrganDamage(entity, single, amount);
        else
            _organHealth.ChangeAllOrganDamage(entity, amount);
    }
}

/// <summary>
///     Damages the body's organs, or heals them if negative. See <see cref="KsOrganHealthComponent"/>.
/// </summary>
/// <inheritdoc cref="EntityEffect"/>
public sealed partial class KsAdjustOrganDamage : EntityEffectBase<KsAdjustOrganDamage>
{
    /// <summary>
    ///     The organ to damage. Null for every organ, unless <see cref="RandomOrgans"/> is set.
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype>? Organ;

    /// <summary>
    ///     If set, one of these organs that the body has, picked at random, instead of <see cref="Organ"/>.
    /// </summary>
    [DataField]
    public List<ProtoId<OrganCategoryPrototype>>? RandomOrgans;

    [DataField(required: true)]
    public float Amount;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        string organ;
        if (RandomOrgans != null)
            organ = Loc.GetString("ks-organ-guidebook-random");
        else if (Organ is { } single)
            organ = Loc.GetString($"ks-organ-guidebook-{single.Id}");
        else
            organ = Loc.GetString("ks-organ-guidebook-all");

        return Loc.GetString("entity-effect-guidebook-ks-adjust-organ-damage",
            ("chance", Probability),
            ("deltasign", MathF.Sign(Amount)),
            ("organ", organ),
            ("amount", MathF.Abs(Amount)));
    }
}
