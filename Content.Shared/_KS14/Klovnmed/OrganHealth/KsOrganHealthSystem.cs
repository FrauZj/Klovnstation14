using System.Diagnostics.CodeAnalysis;
using Content.Shared._KS14.PopupLocalization;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Metabolism;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._KS14.Klovnmed.OrganHealth;

/// <summary>
///     Damage, healing and failure for every organ with <see cref="KsOrganHealthComponent"/>, ported from tgstation's
///         organs (code/modules/surgery/organs/_organ.dm). tg ticks every 2 seconds; every number here is per second.
///         What an organ does when damaged, failed or missing is data on its prototype, and so is the liver's toxin
///         filtering (code/modules/reagents/chemistry/holder/mob_life.dm): no organ has a component or system of its own.
/// </summary>
public sealed partial class KsOrganHealthSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private BodyHierarchySystem _bodyHierarchySystem = default!;
    [Dependency] private HungerSystem _hungerSystem = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private SharedEntityEffectsSystem _entityEffectsSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private StatusEffectsSystem _statusEffectsSystem = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
    private static readonly ProtoId<MetabolismStagePrototype> DigestionStage = "Digestion";
    private static readonly ProtoId<OrganCategoryPrototype> StomachCategory = "Stomach";

    /// <summary>
    ///     A 15u syringe of a toxin with a power of 1 does 1 damage a second to the organ filtering it. tg's baseline.
    /// </summary>
    private const float ToxinUnitsPerDamage = 15f;

    /// <summary>
    ///     Reused to walk a body's expected organs, since their effects can change which ones it expects.
    /// </summary>
    private readonly List<(ProtoId<OrganCategoryPrototype> Category, EntProtoId Prototype)> _expected = new();

    [SubscribeLocalEvent]
    private void OnOrganInserted(Entity<KsOrganHealthComponent> entity, ref OrganGotInsertedEvent args)
    {
        // The body component is networked, so only the server adds to it.
        if (_netManager.IsClient)
            return;

        if (args.OrganComponent.Category is not { } category || MetaData(entity).EntityPrototype is not { } prototype)
            return;

        if (!EnsureComp<KsOrganHealthBodyComponent>(args.Target, out var bodyComponent))
            bodyComponent.NextUpdate = _gameTiming.CurTime + UpdateInterval;

        bodyComponent.ExpectedOrgans[category] = prototype.ID;
        Dirty(args.Target, bodyComponent);
    }

    /// <summary>
    ///     A failed metabolizer organ that freezes, or one missing a working organ it requires, only metabolizes
    ///         self-consuming reagents. A working required organ with a toxin filter removes small amounts of toxin first.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnMetabolizeAttempt(Entity<MetabolizerComponent> entity, ref KsMetabolizeReagentAttemptEvent args)
    {
        if (args.Reagent.KsSelfConsuming)
            return;

        if (TryComp<KsOrganHealthComponent>(entity, out var healthComponent) && healthComponent is { Failing: true, FreezeMetabolismWhenFailing: true })
        {
            args.Outcome = KsMetabolizeReagentOutcome.Freeze;
            return;
        }

        if (!TryComp<KsMetabolismRequiresOrgansComponent>(entity, out var requiresComponent) ||
            !TryComp<KsOrganHealthBodyComponent>(args.Body, out var bodyComponent))
            return;

        foreach (var category in requiresComponent.Organs)
        {
            // A body that never had one doesn't need one.
            if (!bodyComponent.ExpectedOrgans.ContainsKey(category))
                continue;

            if (!TryGetOrgan(args.Body, category, out var requiredOrgan) || requiredOrgan.Value.Comp is { Failing: true, FreezeMetabolismWhenFailing: true })
            {
                args.Outcome = KsMetabolizeReagentOutcome.Freeze;
                return;
            }

            if (IsToxinFiltered(args.Body, requiredOrgan.Value.Comp, args.Reagent))
                args.Outcome = KsMetabolizeReagentOutcome.Filter;
        }
    }

    [SubscribeLocalEvent]
    private void OnReagentTransferred(Entity<KsOrganHealthBodyComponent> entity, ref KsReagentTransferredEvent args)
    {
        if (args.Stage != DigestionStage || args.Reagent.KsIngestedOrganDamage is not { } organDamage)
            return;

        foreach (var (category, perUnit) in organDamage)
        {
            TryChangeOrganDamage(entity, category, perUnit * (float)args.Amount);
        }
    }

    [SubscribeLocalEvent]
    private void OnBodyExamined(Entity<KsOrganHealthBodyComponent> entity, ref ExaminedEvent args)
    {
        foreach (var category in entity.Comp.ExpectedOrgans.Keys)
        {
            if (!TryGetOrgan(entity, category, out var organ) || !organ.Value.Comp.Failing)
                continue;

            var stageIndex = GetStageIndex(organ.Value.Comp, organ.Value.Comp.FailureTime);
            if (stageIndex >= 0 && organ.Value.Comp.FailureStages[stageIndex].Examine is { } examine)
                args.PushMarkup(Loc.GetString(examine, ("target", Identity.Entity(entity, EntityManager))));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Organ damage and what failed organs do are dealt by the server. Prediction only needs to know which organs
        //     have failed, which is networked, to know what to freeze.
        if (_netManager.IsClient)
            return;

        var now = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<KsOrganHealthBodyComponent>();
        while (query.MoveNext(out var bodyUid, out var bodyComponent))
        {
            if (now < bodyComponent.NextUpdate)
                continue;

            bodyComponent.NextUpdate += UpdateInterval;

            if (_mobStateSystem.IsDead(bodyUid))
                continue;

            _expected.Clear();
            foreach (var (category, prototype) in bodyComponent.ExpectedOrgans)
            {
                _expected.Add((category, prototype));
            }

            foreach (var (category, prototype) in _expected)
            {
                if (TerminatingOrDeleted(bodyUid))
                    break;

                if (TryGetOrgan(bodyUid, category, out var organ))
                    UpdateOrgan(bodyUid, category, organ.Value);
                else
                    UpdateMissing(bodyUid, category, prototype);
            }
        }
    }

    private void UpdateOrgan(EntityUid bodyUid, ProtoId<OrganCategoryPrototype> category, Entity<KsOrganHealthComponent> organ)
    {
        var healthComponent = organ.Comp;

        if (healthComponent.ToxinFilter is { } filter && !healthComponent.Failing)
            UpdateToxinFilter(bodyUid, organ, filter);

        var healing = GetHealing(bodyUid, healthComponent);
        if (healing > 0f)
            SetDamage(organ.AsNullable(), healthComponent.Damage - healing);

        var seconds = (float)UpdateInterval.TotalSeconds;
        var previousFailureTime = healthComponent.FailureTime;
        healthComponent.FailureTime = healthComponent.Failing
            ? healthComponent.FailureTime + seconds
            : MathF.Max(0f, healthComponent.FailureTime - seconds / 2f);

        if (healthComponent.FailureTime != previousFailureTime)
            DirtyField(organ, healthComponent, nameof(KsOrganHealthComponent.FailureTime));

        foreach (var symptom in healthComponent.Symptoms)
        {
            if (healthComponent.Damage >= symptom.MinDamage)
                _entityEffectsSystem.ApplyEffects(bodyUid, symptom.Effects);
        }

        if (!healthComponent.Failing)
        {
            healthComponent.CurrentStage = -1;
            return;
        }

        if (IsStable(bodyUid, category))
            return;

        var stageIndex = GetStageIndex(healthComponent, healthComponent.FailureTime);
        if (stageIndex < 0)
            return;

        var stage = healthComponent.FailureStages[stageIndex];
        if (stageIndex != healthComponent.CurrentStage)
        {
            healthComponent.CurrentStage = stageIndex;
            _entityEffectsSystem.ApplyEffects(bodyUid, stage.Start);
        }

        _entityEffectsSystem.ApplyEffects(bodyUid, stage.Effects);
    }

    private void UpdateMissing(EntityUid bodyUid, ProtoId<OrganCategoryPrototype> category, EntProtoId prototype)
    {
        if (IsStable(bodyUid, category))
            return;

        if (!ProtoMan.TryIndex(prototype, out var organPrototype) ||
            !organPrototype.TryComp<KsOrganHealthComponent>(out var healthComponent, Factory))
            return;

        _entityEffectsSystem.ApplyEffects(bodyUid, healthComponent.MissingEffects);
    }

    /// <summary>
    ///     tg: every toxin over the organ's tolerance damages it by <c>(units / 15) * toxpwr * liver_damage_multiplier /
    ///         liver_resistance</c> a second, counting what is still in the stomach, and a damaged organ hurts now and
    ///         then while they do.
    /// </summary>
    private void UpdateToxinFilter(EntityUid bodyUid, Entity<KsOrganHealthComponent> organ, KsToxinFilter filter)
    {
        if (!_solutionContainerSystem.TryGetSolution(bodyUid, BloodstreamComponent.DefaultBloodSolutionName, out _, out var bloodSolution))
            return;

        TryGetStomachSolution(bodyUid, out var stomachSolution);

        var tolerance = GetToxinTolerance(organ.Comp, filter);
        var damage = 0f;
        foreach (var (reagent, quantity) in bloodSolution.Contents)
        {
            if (!ProtoMan.TryIndex<ReagentPrototype>(reagent.Prototype, out var reagentPrototype) || reagentPrototype.KsToxin is not { } toxin)
                continue;

            var amount = (float)quantity;
            if (stomachSolution != null)
                amount += (float)stomachSolution.GetTotalPrototypeQuantity(reagentPrototype.ID);

            if (amount <= tolerance * toxin.ToleranceMultiplier)
                continue;

            damage += amount / ToxinUnitsPerDamage * toxin.Power * toxin.DamageMultiplier / filter.Resistance;
        }

        if (damage <= 0f)
            return;

        ChangeDamage(organ.AsNullable(), MathF.Min(damage, filter.MaxDamage));

        // tg: SPT_PROB(damage / 6), a percentage a second, so the worse the damage the likelier it hurts.
        if (filter.PainMessage is { } painMessage &&
            organ.Comp.Damage > organ.Comp.LowThreshold &&
            _robustRandom.Prob(MathF.Min(1f, organ.Comp.Damage / 600f)))
            _popupSystem.PopupEntity(KsPopupMessage.Create(painMessage), bodyUid, bodyUid, type: PopupType.SmallCaution);
    }

    /// <summary>
    ///     Whether an organ's toxin filter removes a reagent before it does anything: it is a toxin, and there is no more
    ///         of it in the blood and stomach together than the organ's tolerance.
    /// </summary>
    private bool IsToxinFiltered(EntityUid bodyUid, KsOrganHealthComponent organ, ReagentPrototype reagent)
    {
        if (organ.ToxinFilter is not { } filter || reagent.KsToxin is not { } toxin)
            return false;

        return GetToxinAmount(bodyUid, reagent.ID) <= GetToxinTolerance(organ, filter) * toxin.ToleranceMultiplier;
    }

    /// <summary>
    ///     How much of each toxin an organ filters out right now. tg scales it with the organ's health, to 0 once failed.
    /// </summary>
    public static float GetToxinTolerance(KsOrganHealthComponent organ, KsToxinFilter filter)
    {
        return filter.Tolerance * MathF.Max(0f, 1f - organ.Damage / organ.MaxHealth);
    }

    /// <summary>
    ///     How much of a toxin a filtering organ sees: what is in the blood plus what is still in the stomach.
    /// </summary>
    private float GetToxinAmount(EntityUid bodyUid, ProtoId<ReagentPrototype> reagent)
    {
        var amount = 0f;

        if (_solutionContainerSystem.TryGetSolution(bodyUid, BloodstreamComponent.DefaultBloodSolutionName, out _, out var bloodSolution))
            amount += (float)bloodSolution.GetTotalPrototypeQuantity(reagent);

        if (TryGetStomachSolution(bodyUid, out var stomachSolution))
            amount += (float)stomachSolution.GetTotalPrototypeQuantity(reagent);

        return amount;
    }

    private bool TryGetStomachSolution(EntityUid bodyUid, [NotNullWhen(true)] out Solution? solution)
    {
        solution = null;
        return _bodyHierarchySystem.TryGetOrgan(bodyUid, StomachCategory, out var stomach) &&
               _solutionContainerSystem.TryGetSolution(stomach.Value.Owner, StomachSystem.DefaultSolutionName, out _, out solution);
    }

    /// <summary>
    ///     tg heals faster the fuller the stomach is, up to five times as fast when full. A failed organ never heals on
    ///         its own.
    /// </summary>
    private float GetHealing(EntityUid bodyUid, KsOrganHealthComponent organ)
    {
        if (organ.Robotic || organ.Failing || organ.Damage <= 0f || organ.Damage >= organ.HealingMaxDamage)
            return 0f;

        var satiety = 0f;
        if (TryComp<HungerComponent>(bodyUid, out var hungerComponent))
        {
            satiety = _hungerSystem.GetHungerThreshold(hungerComponent) switch
            {
                HungerThreshold.Overfed => 1f,
                HungerThreshold.Okay => 0.5f,
                _ => 0f,
            };
        }

        return organ.Healing * (1f + 4f * satiety);
    }

    /// <summary>
    ///     The stage a failing organ is in, by how long it has been failing. -1 if it hasn't reached the first.
    /// </summary>
    private static int GetStageIndex(KsOrganHealthComponent organ, float failureTime)
    {
        var index = -1;
        for (var i = 0; i < organ.FailureStages.Count; i++)
        {
            if (organ.FailureStages[i].Time <= failureTime)
                index = i;
        }

        return index;
    }

    /// <summary>
    ///     Whether a status effect is holding off what this body's failed or missing organ of a category does.
    /// </summary>
    public bool IsStable(EntityUid bodyUid, ProtoId<OrganCategoryPrototype> category)
    {
        if (!_statusEffectsSystem.TryEffectsWithComp<KsStableOrgansStatusEffectComponent>(bodyUid, out var effects))
            return false;

        foreach (var effect in effects)
        {
            if (effect.Comp1.Organs == null || effect.Comp1.Organs.Contains(category))
                return true;
        }

        return false;
    }

    public bool TryGetOrgan(EntityUid bodyUid, ProtoId<OrganCategoryPrototype> category, [NotNullWhen(true)] out Entity<KsOrganHealthComponent>? organ)
    {
        organ = null;

        if (!_bodyHierarchySystem.TryGetOrgan(bodyUid, category, out var found) ||
            !TryComp<KsOrganHealthComponent>(found.Value.Owner, out var healthComponent))
            return false;

        organ = (found.Value.Owner, healthComponent);
        return true;
    }

    /// <summary>
    ///     Changes the damage of a body's organ of a category by <paramref name="amount"/>; negative heals.
    /// </summary>
    /// <returns>False if the body has no such organ.</returns>
    public bool TryChangeOrganDamage(EntityUid bodyUid, ProtoId<OrganCategoryPrototype> category, float amount)
    {
        if (!TryGetOrgan(bodyUid, category, out var organ))
            return false;

        ChangeDamage(organ.Value.AsNullable(), amount);
        return true;
    }

    /// <summary>
    ///     Changes the damage of every organ in a body by <paramref name="amount"/>; negative heals.
    /// </summary>
    public void ChangeAllOrganDamage(EntityUid bodyUid, float amount)
    {
        if (!TryComp<KsOrganHealthBodyComponent>(bodyUid, out var bodyComponent))
            return;

        foreach (var category in bodyComponent.ExpectedOrgans.Keys)
        {
            if (TryGetOrgan(bodyUid, category, out var organ))
                ChangeDamage(organ.Value.AsNullable(), amount);
        }
    }

    public void ChangeDamage(Entity<KsOrganHealthComponent?> organ, float amount)
    {
        if (!Resolve(organ, ref organ.Comp, logMissing: false))
            return;

        SetDamage(organ, organ.Comp.Damage + amount);
    }

    public void SetDamage(Entity<KsOrganHealthComponent?> organ, float damage)
    {
        if (!Resolve(organ, ref organ.Comp, logMissing: false))
            return;

        var healthComponent = organ.Comp;
        damage = Math.Clamp(damage, 0f, healthComponent.MaxHealth);
        if (damage == healthComponent.Damage)
            return;

        var previous = healthComponent.Damage;
        healthComponent.Damage = damage;
        DirtyField(organ, healthComponent, nameof(KsOrganHealthComponent.Damage));

        ShowThresholdMessage((organ, healthComponent), previous, damage);
    }

    /// <summary>
    ///     Sets how long the organ has been failing, which picks its failure stage.
    /// </summary>
    public void SetFailureTime(Entity<KsOrganHealthComponent?> organ, float seconds)
    {
        if (!Resolve(organ, ref organ.Comp, logMissing: false))
            return;

        organ.Comp.FailureTime = MathF.Max(0f, seconds);
        DirtyField(organ, organ.Comp, nameof(KsOrganHealthComponent.FailureTime));
    }

    /// <summary>
    ///     tg: <c>check_damage_thresholds</c>. The owner feels it as damage crosses a threshold, either way, unless they
    ///         are too far gone to notice.
    /// </summary>
    private void ShowThresholdMessage(Entity<KsOrganHealthComponent> organ, float previous, float damage)
    {
        if (_netManager.IsClient || !_bodyHierarchySystem.TryGetBody(organ.Owner, out var bodyUid))
            return;

        if (_mobStateSystem.IsCritical(bodyUid.Value) || _mobStateSystem.IsDead(bodyUid.Value))
            return;

        var healthComponent = organ.Comp;
        LocId? message = null;

        if (damage > previous)
        {
            if (previous < healthComponent.MaxHealth && damage >= healthComponent.MaxHealth)
                message = healthComponent.NowFailing;
            else if (previous < healthComponent.HighThreshold && damage >= healthComponent.HighThreshold)
                message = healthComponent.HighThresholdPassed;
            else if (previous < healthComponent.LowThreshold && damage >= healthComponent.LowThreshold)
                message = healthComponent.LowThresholdPassed;
        }
        else
        {
            if (previous >= healthComponent.MaxHealth && damage < healthComponent.MaxHealth)
                message = healthComponent.NowFixed;
            else if (previous >= healthComponent.HighThreshold && damage < healthComponent.HighThreshold)
                message = healthComponent.HighThresholdCleared;
            else if (previous >= healthComponent.LowThreshold && damage < healthComponent.LowThreshold)
                message = healthComponent.LowThresholdCleared;
        }

        if (message is { } id)
            _popupSystem.PopupEntity(KsPopupMessage.Create(id), bodyUid.Value, bodyUid.Value, type: PopupType.MediumCaution);
    }

    public KsOrganHealthStatus GetStatus(KsOrganHealthComponent organ)
    {
        if (organ.Failing)
            return KsOrganHealthStatus.Failing;

        if (organ.Damage > organ.HighThreshold)
            return KsOrganHealthStatus.Severe;

        return organ.Damage > organ.LowThreshold ? KsOrganHealthStatus.Mild : KsOrganHealthStatus.Healthy;
    }

    /// <summary>
    ///     Every organ of a body that isn't healthy, as a health analyzer shows them.
    /// </summary>
    public List<KsOrganHealthEntry> GetUnhealthyOrgans(EntityUid bodyUid)
    {
        var entries = new List<KsOrganHealthEntry>();
        if (!TryComp<KsOrganHealthBodyComponent>(bodyUid, out var bodyComponent))
            return entries;

        foreach (var category in bodyComponent.ExpectedOrgans.Keys)
        {
            var status = TryGetOrgan(bodyUid, category, out var organ)
                ? GetStatus(organ.Value.Comp)
                : KsOrganHealthStatus.Missing;

            if (status != KsOrganHealthStatus.Healthy)
                entries.Add(new KsOrganHealthEntry(category.Id, status));
        }

        return entries;
    }
}
