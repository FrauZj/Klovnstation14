#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._KS14.CCVar;
using Content.Shared._KS14.EntityConditions.Conditions;
using Content.Shared._KS14.EntityEffects.Effects;
using Content.Shared._KS14.Klovnmed;
using Content.Shared._KS14.Klovnmed.OrganHealth;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Tests._KS14.Medical;

/// <summary>
///     Every organ can be damaged and fail, and does what its prototype says when it does. The liver also filters
///         toxins and, failed or missing, freezes reagents in the blood, and medicine costs the organs tg's does. See
///         <see cref="KsOrganHealthSystem"/>.
/// </summary>
[TestFixture]
[TestOf(typeof(KsOrganHealthSystem))]
public sealed class KsOrganHealthTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private static readonly EntProtoId HumanProto = "MobHuman";
    private static readonly EntProtoId DionaProto = "MobDiona";
    private static readonly EntProtoId BlindnessStatusEffect = "StatusEffectBlindness";

    private static readonly ProtoId<OrganCategoryPrototype> Liver = "Liver";
    private static readonly ProtoId<OrganCategoryPrototype> Heart = "Heart";
    private static readonly ProtoId<OrganCategoryPrototype> Eyes = "Eyes";
    private static readonly ProtoId<OrganCategoryPrototype> Brain = "Brain";
    private static readonly ProtoId<OrganCategoryPrototype> Stomach = "Stomach";
    private static readonly ProtoId<OrganCategoryPrototype> Appendix = "Appendix";
    private static readonly ProtoId<OrganCategoryPrototype> Tongue = "Tongue";

    private static readonly ProtoId<ReagentPrototype> Toxin = "Toxin";
    private static readonly ProtoId<ReagentPrototype> Ethanol = "Ethanol";
    private static readonly ProtoId<ReagentPrototype> Bicaridine = "Bicaridine";
    private static readonly ProtoId<ReagentPrototype> Higadrite = "Higadrite";
    private static readonly ProtoId<ReagentPrototype> Stellibinin = "Stellibinin";
    private static readonly ProtoId<ReagentPrototype> Ethylredoxrazine = "Ethylredoxrazine";
    private static readonly ProtoId<ReagentPrototype> Dylovene = "Dylovene";
    private static readonly ProtoId<ReagentPrototype> Mannitol = "Mannitol";
    private static readonly ProtoId<ReagentPrototype> Libital = "Libital";
    private static readonly ProtoId<ReagentPrototype> Aiuri = "Aiuri";
    private static readonly ProtoId<ReagentPrototype> Lenturi = "Lenturi";
    private static readonly ProtoId<ReagentPrototype> Seiver = "Seiver";
    private static readonly ProtoId<ReagentPrototype> SalicylicAcid = "SalicylicAcid";
    private static readonly ProtoId<ReagentPrototype> Granibitaluri = "Granibitaluri";
    private static readonly ProtoId<ReagentPrototype> Cryoxadone = "Cryoxadone";

    /// <summary>
    ///     Between them: organ damage to one organ and to all of them, a metabolite, and <c>KsDamageCondition</c> over
    ///         one damage group and over two.
    /// </summary>
    private static readonly ProtoId<ReagentPrototype>[] GuidebookReagents =
        [Libital, Cryoxadone, Ethanol, Ethylredoxrazine, SalicylicAcid, Granibitaluri];

    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";
    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    /// <summary>
    ///     Medicine whose cost, as in tg, is damage to an organ while it is in the blood.
    /// </summary>
    private static readonly object[] OrganCostMedicine =
    [
        new object[] { Libital, Liver },
        new object[] { Aiuri, Eyes },
        new object[] { Lenturi, Stomach },
        new object[] { Seiver, Heart },
    ];

    /// <summary>
    ///     Spawns a mob in breathable air, so that nothing but what a test does damages it: the bare test map is vacuum,
    ///         whose barotrauma is brute damage and whose suffocation is asphyxiation.
    /// </summary>
    private async Task<EntityUid> Spawn(EntProtoId prototype)
    {
        var server = Pair.Server;
        var testMap = await Pair.CreateTestMap();
        var mob = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int)Gas.Oxygen] = 21.824779f;
            moles[(int)Gas.Nitrogen] = 82.10312f;
            server.EntMan.System<AtmosphereSystem>().SetMapAtmosphere(testMap.MapUid, space: false, new GasMixture(moles, Atmospherics.T20C));

            mob = server.EntMan.SpawnEntity(prototype, new EntityCoordinates(testMap.Grid, 0.5f, 0.5f));
        });

        await Pair.RunTicksSync(5);
        return mob;
    }

    private Task<EntityUid> SpawnHuman() => Spawn(HumanProto);

    /// <summary>
    ///     Reads something on the server's game thread.
    /// </summary>
    private async Task<T> Read<T>(Func<T> read)
    {
        var value = default(T)!;
        await Pair.Server.WaitPost(() => value = read());
        return value;
    }

    /// <summary>
    ///     Only call on the server's game thread.
    /// </summary>
    private Entity<KsOrganHealthComponent> GetOrgan(EntityUid body, ProtoId<OrganCategoryPrototype> category)
    {
        Assert.That(Pair.Server.EntMan.System<KsOrganHealthSystem>().TryGetOrgan(body, category, out var organ), Is.True,
            $"the body has a {category}");
        return organ!.Value;
    }

    /// <summary>
    ///     Sets an organ's damage to what <paramref name="damage"/> picks from it, and returns that.
    /// </summary>
    private Task<float> SetOrganDamage(EntityUid body, ProtoId<OrganCategoryPrototype> category, Func<KsOrganHealthComponent, float> damage)
    {
        return Read(() =>
        {
            var organ = GetOrgan(body, category);
            var amount = damage(organ.Comp);
            Pair.Server.EntMan.System<KsOrganHealthSystem>().SetDamage(organ.AsNullable(), amount);
            return amount;
        });
    }

    private Task FailOrgan(EntityUid body, ProtoId<OrganCategoryPrototype> category) =>
        SetOrganDamage(body, category, organ => organ.MaxHealth);

    /// <summary>
    ///     Damages an organ just past where it heals on its own, so only what the test does changes it.
    /// </summary>
    private Task<float> DamagePastNaturalHealing(EntityUid body, ProtoId<OrganCategoryPrototype> category)
    {
        return SetOrganDamage(body, category, organ =>
        {
            var damage = organ.HealingMaxDamage + 1f;
            Assert.That(damage, Is.LessThan(organ.MaxHealth), $"the {category} can be damaged past healing without failing");
            return damage;
        });
    }

    private Task<float> OrganDamage(EntityUid body, ProtoId<OrganCategoryPrototype> category) =>
        Read(() => GetOrgan(body, category).Comp.Damage);

    private async Task Inject(EntityUid body, ProtoId<ReagentPrototype> reagent, float amount)
    {
        var server = Pair.Server;
        await server.WaitPost(() =>
        {
            var added = server.EntMan.System<SharedBloodstreamSystem>().TryAddToBloodstream(body, new Solution(reagent, FixedPoint2.New(amount)));
            Assert.That(added, Is.True, $"{reagent} went into the blood");
        });
    }

    private async Task AddMetabolite(EntityUid body, ProtoId<ReagentPrototype> reagent, float amount)
    {
        var server = Pair.Server;
        await server.WaitPost(() =>
        {
            var solutionSystem = server.EntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutionSystem.TryGetSolution(body, BloodstreamComponent.DefaultMetabolitesSolutionName, out var metabolites, out _), Is.True);
            Assert.That(solutionSystem.TryAddReagent(metabolites!.Value, reagent, FixedPoint2.New(amount)), Is.True);
        });
    }

    private async Task DealDamage(EntityUid body, ProtoId<DamageTypePrototype> type, float amount)
    {
        var server = Pair.Server;
        await server.WaitPost(() =>
        {
            var damage = new DamageSpecifier();
            damage.DamageDict.Add(type, FixedPoint2.New(amount));
            server.EntMan.System<DamageableSystem>().TryChangeDamage(body, damage, ignoreResistances: true);
        });
    }

    private float BloodQuantity(EntityUid body, ProtoId<ReagentPrototype> reagent)
    {
        Assert.That(Pair.Server.EntMan.System<SharedSolutionContainerSystem>()
            .TryGetSolution(body, BloodstreamComponent.DefaultBloodSolutionName, out _, out var blood), Is.True);

        return (float)blood!.GetTotalPrototypeQuantity(reagent);
    }

    private float MetaboliteQuantity(EntityUid body, ProtoId<ReagentPrototype> reagent)
    {
        Assert.That(Pair.Server.EntMan.System<SharedSolutionContainerSystem>()
            .TryGetSolution(body, BloodstreamComponent.DefaultMetabolitesSolutionName, out _, out var metabolites), Is.True);

        return (float)metabolites!.GetTotalPrototypeQuantity(reagent);
    }

    private float Damage(EntityUid body, ProtoId<DamageTypePrototype> type)
    {
        var damage = Pair.Server.EntMan.System<DamageableSystem>().GetAllDamage(body);
        return damage.DamageDict.TryGetValue(type, out var amount) ? (float)amount : 0f;
    }

    [Test]
    public async Task ToxinUnderToleranceIsFiltered()
    {
        var human = await SpawnHuman();
        var tolerance = await Read(() => GetOrgan(human, Liver).Comp.ToxinFilter?.Tolerance);
        Assert.That(tolerance, Is.Not.Null, "the liver filters toxins");

        await Inject(human, Toxin, tolerance!.Value * 2f / 3f);
        await RunSeconds(5);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(BloodQuantity(human, Toxin), Is.Zero, "the liver removed the toxin");
            Assert.That(Damage(human, Poison), Is.Zero, "toxin under the liver's tolerance does nothing");
            Assert.That(GetOrgan(human, Liver).Comp.Damage, Is.Zero);
        });
    }

    [Test]
    public async Task ToxinOverToleranceDamagesTheLiver()
    {
        var human = await SpawnHuman();
        var tolerance = await Read(() => GetOrgan(human, Liver).Comp.ToxinFilter!.Tolerance);
        await Inject(human, Toxin, tolerance * 3f + 1f);
        await RunSeconds(3);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Damage(human, Poison), Is.GreaterThan(0f), "toxin over the tolerance works");
            Assert.That(GetOrgan(human, Liver).Comp.Damage, Is.GreaterThan(0f), "toxin over the tolerance damages the liver");
        });
    }

    [Test]
    public async Task FailedLiverFreezesMedicineButNotSelfConsuming()
    {
        var human = await SpawnHuman();
        await FailOrgan(human, Liver);
        await Inject(human, Bicaridine, 5f);
        await Inject(human, Higadrite, 5f);
        await RunSeconds(4);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(GetOrgan(human, Liver).Comp.Failing, Is.True);
            Assert.That(BloodQuantity(human, Bicaridine), Is.EqualTo(5f).Within(0.01f), "bicaridine is frozen in the blood");
            Assert.That(BloodQuantity(human, Higadrite), Is.LessThan(5f), "higadrite is self-consuming and works anyway");
        });
    }

    [Test]
    public async Task LiverFailureStagesArePausedByHigadrite()
    {
        var human = await SpawnHuman();
        var server = Pair.Server;

        await FailOrgan(human, Liver);
        await server.WaitPost(() =>
        {
            var liver = GetOrgan(human, Liver);
            var poisoningStage = liver.Comp.FailureStages.First(stage => stage.Effects.Length > 0);
            server.EntMan.System<KsOrganHealthSystem>().SetFailureTime(liver.AsNullable(), poisoningStage.Time + 5f);
        });
        await RunSeconds(3);

        var poisonBefore = 0f;
        await server.WaitAssertion(() =>
        {
            poisonBefore = Damage(human, Poison);
            Assert.That(poisonBefore, Is.GreaterThan(0f), "a liver failing for long enough poisons its owner");
        });

        await Inject(human, Higadrite, 10f);
        await RunSeconds(2); // let it reach the heart
        await server.WaitPost(() => poisonBefore = Damage(human, Poison));
        await RunSeconds(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(Damage(human, Poison), Is.EqualTo(poisonBefore).Within(0.01f), "higadrite holds off the liver's failure");
        });
    }

    [Test]
    public async Task MissingLiverFreezesMedicineAndPoisons()
    {
        var human = await SpawnHuman();
        var server = Pair.Server;

        await server.WaitPost(() => server.EntMan.DeleteEntity(GetOrgan(human, Liver)));
        await Inject(human, Bicaridine, 5f);
        await RunSeconds(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.System<KsOrganHealthSystem>().TryGetOrgan(human, Liver, out _), Is.False);
            Assert.That(BloodQuantity(human, Bicaridine), Is.EqualTo(5f).Within(0.01f), "bicaridine is frozen without a liver");
            Assert.That(Damage(human, Poison), Is.GreaterThan(0f), "a missing liver poisons its owner");
        });
    }

    [Test]
    public async Task StellibininHealsTheLiver()
    {
        var human = await SpawnHuman();
        var damage = await DamagePastNaturalHealing(human, Liver);
        await Inject(human, Stellibinin, 10f);
        await RunSeconds(4);

        Assert.That(await OrganDamage(human, Liver), Is.LessThan(damage - 2f), "stellibinin heals the liver");
    }

    [Test]
    public async Task OrgansOnlyHealOnTheirOwnWhenLightlyDamaged()
    {
        var light = await SpawnHuman();
        var heavy = await SpawnHuman();

        var lightDamage = await SetOrganDamage(light, Appendix, organ => organ.HealingMaxDamage / 2f);
        var heavyDamage = await DamagePastNaturalHealing(heavy, Appendix);
        await RunSeconds(4);

        Assert.That(await OrganDamage(light, Appendix), Is.LessThan(lightDamage), "a lightly damaged organ heals on its own");
        Assert.That(await OrganDamage(heavy, Appendix), Is.EqualTo(heavyDamage), "a badly damaged organ needs medicine");
    }

    [Test]
    public async Task BicaridineHurtsTheLiverOnlyWhenSwallowed()
    {
        var injected = await SpawnHuman();
        var swallowed = await SpawnHuman();
        var server = Pair.Server;

        await Inject(injected, Bicaridine, 10f);
        await server.WaitPost(() =>
        {
            Assert.That(server.EntMan.System<BodyHierarchySystem>().TryGetOrgan(swallowed, Stomach, out var stomach), Is.True);
            var added = server.EntMan.System<StomachSystem>().TryTransferSolution(stomach!.Value.Owner, new Solution(Bicaridine, FixedPoint2.New(10)));
            Assert.That(added, Is.True, "bicaridine went into the stomach");
        });

        await RunSeconds(4);

        Assert.That(await OrganDamage(injected, Liver), Is.Zero, "injected bicaridine never goes through the liver");
        Assert.That(await OrganDamage(swallowed, Liver), Is.GreaterThan(0f), "swallowed bicaridine goes through the liver on its way to the blood");
    }

    /// <summary>
    ///     The other side of <see cref="BicaridineHurtsTheLiverOnlyWhenSwallowed"/>: medicine whose cost is in the blood
    ///         pays it however it got there.
    /// </summary>
    [TestCaseSource(nameof(OrganCostMedicine))]
    public async Task MedicineCostsItsOrganWhenInjected(ProtoId<ReagentPrototype> reagent, ProtoId<OrganCategoryPrototype> category)
    {
        var human = await SpawnHuman();
        var damage = await DamagePastNaturalHealing(human, category);
        await Inject(human, reagent, 10f);
        await RunSeconds(4);

        Assert.That(await OrganDamage(human, category), Is.GreaterThan(damage), $"injected {reagent} damages the {category}");
    }

    [Test]
    public async Task EthanolDamagesTheLiver()
    {
        var human = await SpawnHuman();
        var damage = await DamagePastNaturalHealing(human, Liver);
        await AddMetabolite(human, Ethanol, 60f);
        await RunSeconds(5);

        Assert.That(await OrganDamage(human, Liver), Is.GreaterThan(damage), "the liver is damaged by the ethanol it works on");
    }

    [Test]
    public async Task EthylredoxrazineClearsEthanolFromTheLiver()
    {
        var human = await SpawnHuman();
        await AddMetabolite(human, Ethanol, 10f);
        await Inject(human, Ethylredoxrazine, 5f);
        await RunSeconds(3);

        await Pair.Server.WaitAssertion(() =>
        {
            // The liver alone gets through 0.05u a second.
            Assert.That(MetaboliteQuantity(human, Ethanol), Is.LessThan(8f), "ethylredoxrazine clears ethanol out of the liver");
        });
    }

    [Test]
    public async Task DyloveneOverdoseHurtsTheLiver()
    {
        var dose = await SpawnHuman();
        var overdose = await SpawnHuman();

        await Inject(dose, Dylovene, 10f);
        await Inject(overdose, Dylovene, 30f);
        await RunSeconds(3);

        Assert.That(await OrganDamage(dose, Liver), Is.Zero, "a normal dose of dylovene leaves the liver alone");
        Assert.That(await OrganDamage(overdose, Liver), Is.GreaterThan(0f), "a dylovene overdose damages the liver");
    }

    /// <summary>
    ///     Salicylic acid's healing depends on how bruised the patient is, through <c>KsDamageCondition</c>.
    /// </summary>
    [Test]
    public async Task SalicylicAcidHealsSevereBruisingFaster()
    {
        var severe = await SpawnHuman();
        var minor = await SpawnHuman();

        const float severeDamage = 60f;
        const float minorDamage = 10f;
        await DealDamage(severe, Blunt, severeDamage);
        await DealDamage(minor, Blunt, minorDamage);
        await Inject(severe, SalicylicAcid, 5f);
        await Inject(minor, SalicylicAcid, 5f);
        await RunSeconds(4);

        await Pair.Server.WaitAssertion(() =>
        {
            var severeHealed = severeDamage - Damage(severe, Blunt);
            var minorHealed = minorDamage - Damage(minor, Blunt);

            Assert.That(minorHealed, Is.GreaterThan(0f), "salicylic acid heals minor bruising");
            Assert.That(severeHealed, Is.GreaterThan(minorHealed * 2f), "salicylic acid heals severe bruising much faster");
        });
    }

    [Test]
    public async Task FailedHeartStopsButKeepsMetabolizing()
    {
        var human = await SpawnHuman();
        await FailOrgan(human, Heart);
        await Inject(human, Bicaridine, 5f);
        await RunSeconds(3);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Damage(human, Asphyxiation), Is.GreaterThan(0f), "a failed heart is cardiac arrest");
            Assert.That(BloodQuantity(human, Bicaridine), Is.LessThan(5f), "reagents still work with a failed heart, as in tg");
        });
    }

    [Test]
    public async Task FailedEyesAreBlind()
    {
        var human = await SpawnHuman();
        var server = Pair.Server;

        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.System<StatusEffectsSystem>().HasStatusEffect(human, BlindnessStatusEffect), Is.False));

        await FailOrgan(human, Eyes);
        await RunSeconds(2);

        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.System<StatusEffectsSystem>().HasStatusEffect(human, BlindnessStatusEffect), Is.True,
                "failed eyes are blind"));
    }

    [Test]
    public async Task MannitolHealsTheBrain()
    {
        var human = await SpawnHuman();
        var damage = await SetOrganDamage(human, Brain, organ => organ.MaxHealth / 2f);
        await Inject(human, Mannitol, 10f);
        await RunSeconds(4);

        // The brain never heals on its own.
        Assert.That(await OrganDamage(human, Brain), Is.LessThan(damage), "mannitol heals the brain");
    }

    [Test]
    public async Task AnalyzerListsUnhealthyOrgans()
    {
        var human = await SpawnHuman();
        var server = Pair.Server;

        await SetOrganDamage(human, Liver, organ => (organ.HighThreshold + organ.MaxHealth) / 2f);
        await FailOrgan(human, Appendix);
        await server.WaitPost(() => server.EntMan.DeleteEntity(GetOrgan(human, Tongue)));
        await Pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var organs = server.EntMan.System<KsOrganHealthSystem>().GetUnhealthyOrgans(human).ToDictionary(e => e.Category, e => e.Status);
            Assert.That(organs.GetValueOrDefault(Liver.Id), Is.EqualTo(KsOrganHealthStatus.Severe));
            Assert.That(organs.GetValueOrDefault(Appendix.Id), Is.EqualTo(KsOrganHealthStatus.Failing));
            Assert.That(organs.GetValueOrDefault(Tongue.Id), Is.EqualTo(KsOrganHealthStatus.Missing));
            Assert.That(organs.ContainsKey(Heart.Id), Is.False, "healthy organs aren't listed");
        });
    }

    /// <summary>
    ///     The guidebook describes every reagent effect. Ours, and effects under our condition, need their strings in
    ///         every culture, or the guidebook shows the raw key.
    /// </summary>
    [TestCase("en-US")]
    [TestCase("ru-RU")]
    public async Task OrganEffectsHaveGuidebookText(string culture)
    {
        await Client.WaitPost(() =>
        {
            Client.ResolveDependency<IConfigurationManager>().SetCVar(KsCCVars.ClientLocale, culture);
            var prototypeManager = Client.ResolveDependency<IPrototypeManager>();
            var entitySystemManager = Client.ResolveDependency<IEntitySystemManager>();

            var checkedLines = 0;
            foreach (var reagentId in GuidebookReagents)
            {
                var reagent = prototypeManager.Index(reagentId);
                foreach (var entry in reagent.Metabolisms!.Metabolisms.Values)
                {
                    foreach (var effect in entry.Effects)
                    {
                        if (effect is not (KsAdjustOrganDamage or KsAdjustMetabolite) &&
                            effect.Conditions?.Any(condition => condition is KsDamageCondition) != true)
                            continue;

                        var line = reagent.GuidebookReagentEffectDescription(prototypeManager, entitySystemManager, effect, entry.MetabolismRate);
                        Assert.That(line, Is.Not.Null.And.Not.Contain("-guidebook-"), $"{reagentId} has a {culture} guidebook line with a missing string");
                        checkedLines++;
                    }
                }
            }

            Assert.That(checkedLines, Is.GreaterThan(0), "there were organ effects to check");
        });
    }

    [Test]
    public async Task BodyWithoutALiverMetabolizesNormally()
    {
        var diona = await Spawn(DionaProto);
        await Inject(diona, Bicaridine, 5f);
        await RunSeconds(4);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Pair.Server.EntMan.System<KsOrganHealthSystem>().TryGetOrgan(diona, Liver, out _), Is.False);
            Assert.That(BloodQuantity(diona, Bicaridine), Is.LessThan(5f), "a diona never had a liver and doesn't need one");
        });
    }
}
