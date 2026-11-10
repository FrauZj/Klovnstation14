using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._KS14.EntityEffects.Effects;

/// <summary>
///     Adds to or removes from a reagent in the body's metabolites solution, where the liver works on it. AdjustReagent
///         only reaches the solution being metabolized, which for medicine is the blood.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class KsAdjustMetaboliteEntityEffectSystem : EntityEffectSystem<BloodstreamComponent, KsAdjustMetabolite>
{
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    protected override void Effect(Entity<BloodstreamComponent> entity, ref EntityEffectEvent<KsAdjustMetabolite> args)
    {
        if (!_solutionContainer.TryGetSolution(entity.Owner, BloodstreamComponent.DefaultMetabolitesSolutionName, out var solution, out _))
            return;

        var amount = args.Effect.Amount * args.Scale;
        if (amount > 0)
            _solutionContainer.TryAddReagent(solution.Value, args.Effect.Reagent.Id, amount, out _);
        else if (amount < 0)
            _solutionContainer.RemoveReagent(solution.Value, args.Effect.Reagent.Id, -amount);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class KsAdjustMetabolite : EntityEffectBase<KsAdjustMetabolite>
{
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField(required: true)]
    public FixedPoint2 Amount;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("entity-effect-guidebook-ks-adjust-metabolite",
            ("chance", Probability),
            ("deltasign", MathF.Sign(Amount.Float())),
            ("reagent", prototype.Index(Reagent).LocalizedName),
            ("amount", MathF.Abs(Amount.Float())));
    }
}
