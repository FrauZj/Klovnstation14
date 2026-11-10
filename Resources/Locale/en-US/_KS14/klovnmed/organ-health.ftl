## Organ health: see KsOrganHealthSystem. Messages are tgstation's.

ks-organ-category-Brain = brain
ks-organ-category-Eyes = eyes
ks-organ-category-Tongue = tongue
ks-organ-category-Appendix = appendix
ks-organ-category-Ears = ears
ks-organ-category-Lungs = lungs
ks-organ-category-Heart = heart
ks-organ-category-Stomach = stomach
ks-organ-category-Liver = liver

ks-organ-guidebook-Brain = the brain
ks-organ-guidebook-Eyes = the eyes
ks-organ-guidebook-Tongue = the tongue
ks-organ-guidebook-Appendix = the appendix
ks-organ-guidebook-Ears = the ears
ks-organ-guidebook-Lungs = the lungs
ks-organ-guidebook-Heart = the heart
ks-organ-guidebook-Stomach = the stomach
ks-organ-guidebook-Liver = the liver
ks-organ-guidebook-all = every organ
ks-organ-guidebook-random = a random organ

ks-health-analyzer-organ-mild = [color=#ffcc33]{ CAPITALIZE($organ) }: mildly damaged.[/color]
ks-health-analyzer-organ-severe = [color=#ff9933]{ CAPITALIZE($organ) }: severely damaged! It won't heal on its own.[/color]
ks-health-analyzer-organ-failing = [color=#cc3333]{ CAPITALIZE($organ) }: non-functional![/color]
ks-health-analyzer-organ-missing = [color=#cc3333]{ CAPITALIZE($organ) }: missing![/color]

entity-effect-guidebook-ks-adjust-organ-damage =
    { $chance ->
        [1] { $deltasign ->
                [1] Damages
               *[-1] Heals
            }
       *[other] { $deltasign ->
                [1] damage
               *[-1] heal
            }
    } { $organ } by {NATURALFIXED($amount, 2)}

entity-effect-guidebook-ks-adjust-metabolite =
    { $chance ->
        [1] { $deltasign ->
                [1] Adds
               *[-1] Removes
            }
       *[other] { $deltasign ->
                [1] add
               *[-1] remove
            }
    } {NATURALFIXED($amount, 2)}u of {$reagent} { $deltasign ->
        [1] to
       *[-1] from
    } the liver

## Heart

ks-organ-heart-low = Prickles of pain appear then die out from within your chest...
ks-organ-heart-high = Something inside your chest hurts, and the pain isn't subsiding. You notice yourself breathing far faster than before.
ks-organ-heart-fixed = Your heart begins to beat again.
ks-organ-heart-high-cleared = The pain in your chest has died down, and your breathing becomes more relaxed.
ks-organ-heart-failure = You feel a terrible pain in your chest, as if your heart has stopped!
ks-organ-heart-failure-others = { CAPITALIZE(THE($entity)) } clutches at { POSS-ADJ($entity) } chest as if { POSS-ADJ($entity) } heart is stopping!

## Lungs

ks-organ-lungs-low = You feel short of breath.
ks-organ-lungs-high = You feel some sort of constriction around your chest as your breathing becomes shallow and rapid.
ks-organ-lungs-fixed = Your lungs seem to once again be able to hold air.
ks-organ-lungs-high-cleared = The constriction around your chest loosens as your breathing calms down.
ks-organ-lungs-low-cleared = You can breathe normally again.
ks-organ-lungs-failure = { CAPITALIZE(THE($entity)) } grabs { POSS-ADJ($entity) } throat, struggling for breath!

## Stomach

ks-organ-stomach-low = Your stomach flashes with pain before subsiding. Food doesn't seem like a good idea right now.
ks-organ-stomach-high = Your stomach flares up with constant pain - you can hardly stomach the idea of food right now!
ks-organ-stomach-high-cleared = The pain in your stomach dies down for now, but food still seems unappealing.
ks-organ-stomach-low-cleared = The last bouts of pain in your stomach have died out.
ks-organ-stomach-failure = Your stomach reels in pain as you're incapable of holding down all that food!

## Eyes

ks-organ-eyes-low = Distant objects become somewhat less tangible.
ks-organ-eyes-high = Everything starts to look a lot less clear.
ks-organ-eyes-failing = Darkness envelopes you, as your eyes go blind!
ks-organ-eyes-fixed = Color and shapes are once again perceivable.
ks-organ-eyes-high-cleared = Your vision functions passably once more.
ks-organ-eyes-low-cleared = Your vision is cleared of any ailment.

## Ears

ks-organ-ears-low = Your ears begin to resonate with an internal ring sometimes.
ks-organ-ears-low-cleared = The ringing in your ears has died down.
ks-organ-ears-ringing = Your ears ring.

## Brain

ks-organ-brain-low = You feel lightheaded.
ks-organ-brain-high = You feel less in control of your thoughts.
ks-organ-brain-failing = The last spark of life in your brain fizzles out...

## Appendix

ks-organ-appendix-failing = An explosion of pain erupts in your lower right abdomen!
ks-organ-appendix-fixed = The pain in your abdomen has subsided.

## Liver

ks-organ-liver-low = Your lower abdomen feels sore.
ks-organ-liver-high = Your lower abdomen feels like it's on fire!
ks-organ-liver-pain = You feel a dull pain in your abdomen.
ks-organ-liver-failure-1 = You feel a stabbing pain in your abdomen!
ks-organ-liver-failure-2 = You feel a burning sensation in your gut!
ks-organ-liver-failure-3 = You feel painful acid in your throat!
ks-organ-liver-failure-4 = Overwhelming pain knocks you out!
ks-organ-liver-failure-5 = You feel as if your guts are about to melt!
ks-organ-liver-drool = { CAPITALIZE(THE($entity)) } drools.
ks-organ-liver-examine-1 = [color=yellow]{ CAPITALIZE(POSS-ADJ($target)) } eyes are slightly yellow.[/color]
ks-organ-liver-examine-2 = [color=yellow]{ CAPITALIZE(POSS-ADJ($target)) } eyes are completely yellow, and { SUBJECT($target) } { CONJUGATE-BE($target) } visibly suffering.[/color]
ks-organ-liver-examine-3 = [color=red]{ CAPITALIZE(POSS-ADJ($target)) } eyes are completely yellow and swelling with pus. { CAPITALIZE(SUBJECT($target)) } won't be alive for much longer.[/color]

entity-condition-guidebook-ks-damage =
    the patient has { $max ->
        [2147483648] at least {NATURALFIXED($min, 2)}
        *[other] { $min ->
                    [0] at most {NATURALFIXED($max, 2)}
                    *[other] between {NATURALFIXED($min, 2)} and {NATURALFIXED($max, 2)}
                 }
    } { $damage } damage

## Reagents

reagent-name-higadrite = higadrite
reagent-desc-higadrite = Holds off what a failed or missing liver does to the body for as long as it lasts. It heals nothing, but works without a liver.
reagent-name-syriniver = syriniver
reagent-desc-syriniver = A strong antitoxin that flushes other chemicals out of the blood, at the cost of the liver. Overdoses at 6u.
reagent-name-inacusiate = inacusiate
reagent-desc-inacusiate = Rapidly repairs damage to the ears.
reagent-name-helbital = helbital
reagent-desc-helbital = Named after the Norse goddess Hel, this medicine heals bruises better the closer the patient is to death. Patients who aren't near death will find it "aids" their healing by suffocating them. Burns through quickly. Overdosing blinds and poisons.
reagent-name-libital = libital
reagent-desc-libital = A bruise reliever. Does minor liver damage, however it gets into the body.
reagent-name-salicylic-acid = salicylic acid
reagent-desc-salicylic-acid = Stimulates the healing of severe bruises. Rapidly heals severe bruising and slowly heals minor ones. Overdose will worsen existing bruising.
reagent-name-aiuri = aiuri
reagent-desc-aiuri = Used to treat burns. Does minor eye damage.
reagent-name-lenturi = lenturi
reagent-desc-lenturi = Used to treat burns. Damages the stomach.
reagent-name-oxandrolone = oxandrolone
reagent-desc-oxandrolone = Stimulates the healing of severe burns. Rapidly heals severe burns and slowly heals minor ones. Overdose will worsen existing burns.
reagent-name-convermol = convermol
reagent-desc-convermol = Restores oxygen deprivation while producing a lesser amount of toxic byproducts. Overdose causes toxic byproducts regardless of oxygen deprivation.
reagent-name-salbutamol = salbutamol
reagent-desc-salbutamol = Rapidly restores oxygen deprivation.
reagent-name-seiver = seiver
reagent-desc-seiver = Heals toxins, and radiation too while the patient is very cold. Damages the heart.
reagent-name-granibitaluri = granibitaluri
reagent-desc-granibitaluri = Speeds up the healing of small bruises and burns, but is ineffective at treating severe injuries. Extremely large doses are toxic, and wear out the liver.
