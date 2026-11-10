---
name: organ-health
description: How organs and medicine work in this repo (Klovnmed organ health) and how to port more of either from tgstation - the one generic organ model every organ shares (KsOrganHealth - damage, thresholds, natural healing, symptoms, failure stages, missing effects, messages), how metabolism depends on organs (frozen reagents, self-consuming reagents, required organs, the liver's toxin filter, swallowed versus injected), converting tg's numbers (2-second ticks, metabolization_ratio works out to 5X a unit), the tier method that sets a healing chem's numbers from how hard it is to make (measured against bicaridine), recipes, and testing. Use when adding or changing what an organ does, porting a tg organ, organ effect or chem, adding medicine or any reagent that touches organs, or tuning organ damage, healing or medicine strength.
---

# Organ health and medicine

Organs and medicine follow tgstation. The organ model, its numbers and its messages are tg's, ported 1:1 per second.
Medicine is tg's chems, with tg's costs, but healing strength comes from this fork's tiers (below), not from tg.

**The one rule: every organ uses the same model.** There is one component, `KsOrganHealthComponent`, and one system,
`KsOrganHealthSystem`. What an organ does when damaged, failed or missing is YAML on its prototype, built from existing
`EntityEffect`s. What a reagent does to organs is YAML on the reagent. No organ gets a component or system of its own.
The liver used to, and was folded into `toxinFilter` for exactly this reason. When tg does something no existing effect
or field can express, add a **generic** piece any organ or reagent can use (a field on `KsOrganHealthComponent`, an
`EntityEffect` like `KsAdjustOrganDamage`, a condition like `KsDamageCondition`), and only then.

**Don't invent organ mechanics tg doesn't have.** tg has no kidneys, so kidneys have no organ health.

## Files

| What | Where |
| --- | --- |
| The model | `Content.Shared/_KS14/Klovnmed/OrganHealth/KsOrganHealthComponent.cs`, `KsOrganHealthSystem.cs` |
| Per-body bookkeeping (organs it expects) | `KsOrganHealthBodyComponent.cs` |
| Metabolism hooks | `KsOrganHealthEvents.cs`, `KsMetabolismRequiresOrgansComponent.cs`, the `KS14` blocks in `Content.Shared/Metabolism/MetabolizerSystem.cs` |
| Stable-organ status effect (tg `TRAIT_STABLELIVER`) | `KsStableOrgansStatusEffectComponent.cs`, `Resources/Prototypes/_KS14/Entities/StatusEffects/body.yml` |
| Reagent fields (`ksSelfConsuming`, `ksToxin`, `ksIngestedOrganDamage`) | `Content.Shared/Chemistry/Reagent/ReagentPrototype.Klovn.Organs.cs` |
| Effects and conditions | `Content.Shared/_KS14/EntityEffects/Effects/KsAdjustOrganDamage.cs`, `KsAdjustMetabolite.cs`, `Content.Shared/_KS14/EntityConditions/Conditions/KsDamageCondition.cs` |
| Each organ's behaviour | `Resources/Prototypes/_KS14/Body/organ_health.yml`, one `KsBaseOrganHealth<Organ>` each, parented in by `Resources/Prototypes/Body/base_organs.yml` (`# KS14` markers) |
| Existing reagents' organ effects | `Resources/Prototypes/_KS14/Reagents/Partials/organs.yml` (partials; the folder is registered in `KsEntryPoint.PartialPrototypePaths`) |
| Ported medicine and recipes | `Resources/Prototypes/_KS14/Reagents/medicine.yml`, `Resources/Prototypes/_KS14/Recipes/Reactions/medicine.yml` |
| Text | `Resources/Locale/{en-US,ru-RU}/_KS14/klovnmed/organ-health.ftl` |
| Health analyzer | `GetUnhealthyOrgans` into `HealthAnalyzerUiState.Organs`, shown in `HealthAnalyzerControl.xaml.cs` |
| Tests | `Content.IntegrationTests/Tests/_KS14/Medical/KsOrganHealthTest.cs` |
| tg source | a checkout is at `D:/tg/tgstation-master`. Organs: `code/modules/surgery/organs/_organ.dm` and `internal/<organ>/`. Chems: `code/modules/reagents/chemistry/reagents/` (`cat2_medicine_reagents.dm`, `medicine_reagents.dm`, `toxin_reagents.dm`), recipes in `.../recipes/`. Read DM with the `dreammaker` skill. |

## The model, against tg's

| tg (`_organ.dm` and the organ's file) | `KsOrganHealthComponent` field |
| --- | --- |
| `maxHealth` | `maxHealth`; the organ is `Failing` at it (tg `ORGAN_FAILING`) |
| `low_threshold`, `high_threshold` | `lowThreshold`, `highThreshold` (health analyzer: mild, severe) |
| `healing_factor * maxHealth` a second | `healing` |
| up to 4 more of that from `satiety` | hunger instead: Okay x3, Overfed x5 (`GetHealing`; an approximation) |
| (none) | `healingMaxDamage`: no natural healing past it, it needs medicine. Ours |
| `IS_ROBOTIC_ORGAN`: never heals | `robotic` |
| `failure_time` and `organ_failure()`'s switch on it | `failureTime` and `failureStages` (`time`, `start` once, `effects` each second, `examine`) |
| `failure_time--` each tick once working again | the same: it counts back at half speed |
| damaged-but-working behaviour in `on_life` | `symptoms` (`minDamage`, `effects` each second) |
| what a body does without the organ | `missingEffects`, read from the organ's prototype, since a missing organ isn't there to ask |
| `low_threshold_passed`, `now_failing`, `now_fixed`, ... | `lowThresholdPassed`, `nowFailing`, `nowFixed`, ... (LocIds) |
| liverless metabolism (`liverless = TRUE`) | `freezeMetabolismWhenFailing`, plus `KsMetabolismRequiresOrgans` on the organ that needs it |
| `self_consuming` | `ksSelfConsuming` on the reagent |
| `TRAIT_STABLELIVER`, `TRAIT_STABLEHEART` | a status effect with `KsStableOrgans` (`StatusEffectKsStableLiver`, given by higadrite) |
| the liver's `toxTolerance`, `liver_resistance`, `MAX_TOXIN_LIVER_DAMAGE`, pain message | `toxinFilter` (`tolerance`, `resistance`, `maxDamage`, `painMessage`) |
| `/datum/reagent/toxin` with `toxpwr`, `liver_damage_multiplier`, `liver_tolerance_multiplier` | `ksToxin: { power, damageMultiplier, toleranceMultiplier }` on the reagent |
| `adjust_organ_loss(SLOT, x)` in a reagent | `!type:KsAdjustOrganDamage { organ, amount }` (`randomOrgans` for tg's `pick(...)`, no organ for all) |

A body only expects organ categories it has held, so a diona (no liver) is never "missing" one.

## Converting tg's numbers

- **tg mobs tick every 2 seconds.** `seconds_per_tick` is 2, so anything written `X * seconds_per_tick` is X a second.
  Every number here is per second. `SPT_PROB(P, seconds_per_tick)` is P percent a second.
- **Reagent effects:** this tg version has `metabolization_ratio = REM * volume metabolized`, with
  `REM = 1 / (REAGENTS_METABOLISM * 2) = 2.5`. So `X * metabolization_ratio * seconds_per_tick` is **5X a unit** at any
  rate, and 5X times the rate a second. tg's default rate is 0.2u a second, and c2 chems run at half that. Older tg code
  multiplies by a constant `REM` of 0.5 instead: check which you are reading.
- **Copy organ numbers 1:1 per second**: health, thresholds, healing, and the organ damage a chem does a second
  (libital's 0.15 liver a second, as tg's). Organs are ported 1:1, so a dose costs an organ what it costs in tg.
- **Don't copy healing.** tg medicine heals about four times what ours does a unit (libital 15 brute a unit, bicaridine
  4). Healing comes from the tiers below.
- Messages are tg's text, in both locale files.

## Porting an organ behaviour

1. Read the tg organ's `on_life`, `organ_failure`, `check_damage_thresholds` overrides and any `handle_*` procs, and
   resolve every define that sets a number.
2. Write it into that organ's `KsBaseOrganHealth<Organ>` in `organ_health.yml`, with a comment naming the tg file:
   numbers into fields, failure behaviour into `failureStages`, damaged behaviour into `symptoms`, life without it into
   `missingEffects`, the messages into `*Passed`/`*Cleared` LocIds and `organ-health.ftl`.
3. Build it from existing effects: `HealthChange` (with `ignoreResistances: true`, like tg's `forced`),
   `ModifyStatusEffect` (blindness, drowsiness, forced sleep, slurring, stutter), `Vomit`, `Emote`, `PopupMessage`
   (`type: Pvs` for others, `visualType` for size), `KsAdjustOrganDamage`. `probability` on an effect is a chance a
   second.
4. A new organ category also needs its parent added in `base_organs.yml` and the `ks-organ-category-*` and
   `ks-organ-guidebook-*` strings, which the health analyzer and guidebook use.
5. Only if none of that can express it, add a generic field, effect or condition (see the rule above).

## Metabolism and organs

The pipeline: the stomach digests (`Digestion`) and passes what it doesn't metabolize to the blood at half strength,
the heart metabolizes the blood (`Bloodstream`) and passes on the rest, and the liver metabolizes that (`Metabolites`).
Before an organ applies a reagent, `KsMetabolizeReagentAttemptEvent` can:

- **Freeze** it (it stays, does nothing, and doesn't count towards the organ's reagent cap): when the metabolizing
  organ has failed and freezes, or when an organ it requires is missing or failed. The heart requires the liver, so a
  failed or missing liver freezes everything in the blood that isn't `ksSelfConsuming`, as tg's liverless metabolism
  does. Mark a reagent self-consuming only when tg does, or when it must work without a liver (cryoxadone, higadrite).
- **Filter** it (removed as if metabolized, with no effects): a working required organ with a `toxinFilter`, which is
  the liver, removes a `ksToxin` when there is no more of it, in blood and stomach together, than its tolerance. The
  tolerance shrinks with the liver's damage. Over the tolerance the toxin works, and damages the liver by
  `(units / 15) * power * damageMultiplier / resistance` a second, at most `maxDamage`.

**The liver rule.** A chem damages the liver when swallowed, not when injected, unless it damages the liver while in
the blood:
- Swallowed, a reagent passes through the liver: `ksIngestedOrganDamage` is organ damage per unit the stomach passes
  on (bicaridine costs the liver 1 a unit this way). Injected, it never touches it.
- A chem whose cost is in the blood, as tg's libital and syriniver are, pays it however it got there: write that as a
  `Bloodstream` effect.
- Toxins are the second kind: they damage the liver from the blood.
- Alcohol damage is on ethanol (`Metabolites` effects in `organs.yml`), as tg has it on the reagent.

## Medicine: the tiers

How hard a chem is to make **here** sets its tier, and the tier sets its numbers. Judge it by this fork's recipe and
dispenser (`ChemDispenser` in `Resources/Prototypes/Entities/Structures/Dispensers/chem.yml`), not by tg's.

| Tier | What makes it | Numbers |
| --- | --- | --- |
| Simple | One reaction from dispenser chemicals (helbital, seiver) | No better than bicaridine. **Either** slow and okay (about 0.25u and 1 healed a second, like bicaridine) **or** fast and weak (about 1u a second for no more healing). **And** a cost. |
| Expensive | An intermediate made first, from dispenser chemicals (bicaridine, aiuri, convermol, granibitaluri) | The same as simple. |
| Hard | Heated or rare intermediates (phenol, acetone, salicylic acid), or ingredients the dispenser lacks (silver, blood, welding fuel) | Normal healing, about 2 a second (upstream bicaridine's 0.5u and 2 a second), with tg's downside, if any. |

The reference is bicaridine: 0.25u a second, 1 brute a second, 4 a unit, overdose at 20u.

- **A cost, as synthflesh has**, for every chem that isn't hard to make: it heals one thing and costs another. Use tg's
  downside: another damage type (helbital's asphyxiation, convermol's poison), an organ (libital the liver, aiuri the
  eyes, lenturi the stomach, seiver the heart), or working only in some state (granibitaluri on small injuries). When
  the cost only makes sense for damage actually healed, gate it on there being damage (`KsDamageCondition`).
- **Hard chems keep tg's downside as it is**, often only an overdose (salicylic acid makes existing bruises worse), or
  none (salbutamol).
- **Effects that depend on the patient:** `MobStateCondition` (alive or critical), `TemperatureCondition` (cold, as in
  cryo), `KsDamageCondition` (`groups`/`types`, `min`/`max`: salicylic acid heals from 25 brute), `ReagentCondition`
  (overdoses: tg's `overdose_threshold`).
- **Recipes are tg's.** Swap a reagent we don't have for its nearest relative and say so in a comment (bromine is
  chlorine for salbutamol). tg `required_temp` above room temperature is `minTemp`, and an `is_cold_recipe` is
  `maxTemp`. Check a new recipe against existing reactions in both directions: one whose ingredients are a subset of
  it can fire first, and it can steal from one it is a subset of.
- Names and descriptions in both locale files. Describe what ours does, which can differ from tg's text.

Ported so far, and what each costs:

| Chem | Heals | Tier | Rate | Healing a second | Cost |
| --- | --- | --- | --- | --- | --- |
| Helbital | brute | simple | 1u | 1 conscious, 2 in crit | asphyxiation; overdose 35u blinds and poisons |
| Seiver | poison, radiation when cold | simple | 0.25u | 1 | heart 0.2 a second |
| Aiuri | burn | expensive | 0.25u | 1 | eyes 0.125 a second |
| Convermol | asphyxiation | expensive | 0.25u | 1 | poison, a fifth of that; overdose 35u more |
| Granibitaluri | brute and burn | expensive | 0.25u | 0.5 each, 0.2 from 40 damage | overdose 50u: liver and poison |
| Libital | brute | hard | 0.5u | 2 | liver 0.2 a second, in the blood |
| Salicylic acid | brute | hard | 0.25u | 2 from 25 brute, else 0.25 | overdose 25u worsens bruises |
| Oxandrolone | burn | hard | 0.25u | 2 from 25 burn, else 0.25 | overdose 25u worsens burns |
| Lenturi | burn | hard | 0.5u | 2 | stomach 0.2 a second |
| Salbutamol | asphyxiation | hard | 0.25u | 2 | none, as tg's |
| Syriniver | poison, flushes chems | hard | 0.15u | 2 | liver 0.4 a second; overdose 6u |

tg chems not ported yet, and why: synthflesh is touch-only in tg (patches and sprays), and touch healing here would
need a reactive group on mobs. Probital and tirimol cost stamina, which no effect can deal yet. Hercuri, pentetic acid
and penthrite need reagents this fork doesn't have.

## Testing

`KsOrganHealthTest` covers the model, the liver, the analyzer and the medicine. When adding to it:

- Derive what the test depends on from the organ or reagent (`MaxHealth`, `HealingMaxDamage`, `ToxinFilter.Tolerance`,
  the first failure stage with effects), not from today's numbers. See CONTRIBUTING's "A test bound to today's tuning".
- Organs heal on their own, by up to 0.25 a second for a fed human, which can hide a small cost. Damage the organ past
  `healingMaxDamage` first (`DamagePastNaturalHealing`) when testing something that damages it.
- Read and write entities on the server thread (`Read`, `WaitPost`, `WaitAssertion`).
- Add a chem whose cost is an organ to `OrganCostMedicine`.

```sh
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~KsOrganHealthTest"
```

Prototype loading and the YAML linter catch bad fields and ids. The reagent guidebook shows each effect's text, so a
new effect or condition needs its `entity-effect-guidebook-*` or `entity-condition-guidebook-*` string.

## What is verified and what is not

Everything about this repo's side was read from the code and is covered by the tests above. The tg numbers were read
from the tg checkout, not seen running side by side with tg. The tiers and the healing numbers that come from them are
this fork's choices, and so is mapping tg's satiety onto hunger: a normally fed body (Okay) heals its organs at three
times tg's base rate, which offsets most of a small organ cost. None of it has been playtested.
