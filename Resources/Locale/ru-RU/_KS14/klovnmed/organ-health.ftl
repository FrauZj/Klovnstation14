## Здоровье органов: см. KsOrganHealthSystem.

ks-organ-category-Brain = мозг
ks-organ-category-Eyes = глаза
ks-organ-category-Tongue = язык
ks-organ-category-Appendix = аппендикс
ks-organ-category-Ears = уши
ks-organ-category-Lungs = лёгкие
ks-organ-category-Heart = сердце
ks-organ-category-Stomach = желудок
ks-organ-category-Liver = печень

ks-organ-guidebook-Brain = мозг
ks-organ-guidebook-Eyes = глаза
ks-organ-guidebook-Tongue = язык
ks-organ-guidebook-Appendix = аппендикс
ks-organ-guidebook-Ears = уши
ks-organ-guidebook-Lungs = лёгкие
ks-organ-guidebook-Heart = сердце
ks-organ-guidebook-Stomach = желудок
ks-organ-guidebook-Liver = печень
ks-organ-guidebook-all = все органы
ks-organ-guidebook-random = случайный орган

ks-health-analyzer-organ-mild = [color=#ffcc33]{ CAPITALIZE($organ) }: лёгкие повреждения.[/color]
ks-health-analyzer-organ-severe = [color=#ff9933]{ CAPITALIZE($organ) }: тяжёлые повреждения! Сами не заживут.[/color]
ks-health-analyzer-organ-failing = [color=#cc3333]{ CAPITALIZE($organ) }: отказ органа![/color]
ks-health-analyzer-organ-missing = [color=#cc3333]{ CAPITALIZE($organ) }: орган отсутствует![/color]

entity-effect-guidebook-ks-adjust-organ-damage =
    { $chance ->
        [1] { $deltasign ->
                [1] Повреждает
               *[-1] Лечит
            }
       *[other] { $deltasign ->
                [1] повредить
               *[-1] вылечить
            }
    } { $organ } на {NATURALFIXED($amount, 2)}

entity-effect-guidebook-ks-adjust-metabolite =
    { $chance ->
        [1] { $deltasign ->
                [1] Добавляет
               *[-1] Выводит
            }
       *[other] { $deltasign ->
                [1] добавить
               *[-1] вывести
            }
    } {NATURALFIXED($amount, 2)} ед. {$reagent} { $deltasign ->
        [1] в печень
       *[-1] из печени
    }

## Сердце

ks-organ-heart-low = В груди вспыхивает и тут же гаснет колючая боль...
ks-organ-heart-high = Что-то в груди болит, и боль не утихает. Вы замечаете, что дышите гораздо чаще обычного.
ks-organ-heart-fixed = Ваше сердце снова начинает биться.
ks-organ-heart-high-cleared = Боль в груди утихает, дыхание становится спокойнее.
ks-organ-heart-failure = Вы чувствуете страшную боль в груди, словно сердце остановилось!
ks-organ-heart-failure-others = { CAPITALIZE($entity) } хватается за грудь, словно сердце вот-вот остановится!

## Лёгкие

ks-organ-lungs-low = Вам не хватает воздуха.
ks-organ-lungs-high = Грудь будто что-то сдавливает, дыхание становится частым и поверхностным.
ks-organ-lungs-fixed = Лёгкие снова могут держать воздух.
ks-organ-lungs-high-cleared = Грудь отпускает, дыхание успокаивается.
ks-organ-lungs-low-cleared = Вы снова дышите нормально.
ks-organ-lungs-failure = { CAPITALIZE($entity) } хватается за горло, пытаясь вдохнуть!

## Желудок

ks-organ-stomach-low = Желудок пронзает боль и отступает. Есть сейчас что-то не хочется.
ks-organ-stomach-high = Желудок не перестаёт болеть - о еде даже думать не хочется!
ks-organ-stomach-high-cleared = Боль в желудке пока утихла, но есть всё ещё не хочется.
ks-organ-stomach-low-cleared = Последние приступы боли в желудке прошли.
ks-organ-stomach-failure = Желудок сводит от боли, еда в нём не держится!

## Глаза

ks-organ-eyes-low = Дальние предметы будто расплываются.
ks-organ-eyes-high = Всё вокруг становится куда менее чётким.
ks-organ-eyes-failing = Вас окутывает тьма - вы слепнете!
ks-organ-eyes-fixed = Вы снова различаете цвета и очертания.
ks-organ-eyes-high-cleared = Зрение снова более-менее в порядке.
ks-organ-eyes-low-cleared = Зрение полностью прояснилось.

## Уши

ks-organ-ears-low = В ушах время от времени начинает звенеть.
ks-organ-ears-low-cleared = Звон в ушах утих.
ks-organ-ears-ringing = У вас звенит в ушах.

## Мозг

ks-organ-brain-low = У вас кружится голова.
ks-organ-brain-high = Вы хуже владеете своими мыслями.
ks-organ-brain-failing = Последняя искра жизни в вашем мозгу угасает...

## Аппендикс

ks-organ-appendix-failing = Внизу живота справа взрывается боль!
ks-organ-appendix-fixed = Боль в животе утихла.

## Печень

ks-organ-liver-low = Внизу живота ноет.
ks-organ-liver-high = Низ живота будто горит!
ks-organ-liver-pain = Вы чувствуете тупую боль в животе.
ks-organ-liver-failure-1 = Вы чувствуете колющую боль в животе!
ks-organ-liver-failure-2 = У вас жжёт в животе!
ks-organ-liver-failure-3 = Вы чувствуете жгучую кислоту в горле!
ks-organ-liver-failure-4 = Вы теряете сознание от невыносимой боли!
ks-organ-liver-failure-5 = Кажется, ваши внутренности сейчас расплавятся!
ks-organ-liver-drool = { CAPITALIZE($entity) } пускает слюни.
ks-organ-liver-examine-1 = [color=yellow]Белки глаз слегка пожелтели.[/color]
ks-organ-liver-examine-2 = [color=yellow]Белки глаз совсем жёлтые, видно, что { $target } страдает.[/color]
ks-organ-liver-examine-3 = [color=red]Глаза совсем жёлтые и заплывают гноем. Долго { $target } не протянет.[/color]

entity-condition-guidebook-ks-damage =
    у пациента { $max ->
        [2147483648] не меньше { NATURALFIXED($min, 2) }
        *[other] { $min ->
                [0] не больше { NATURALFIXED($max, 2) }
                *[other] от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) }
            }
    } урона ({ $damage })

## Реагенты

reagent-name-higadrite = хигадрит
reagent-desc-higadrite = Сдерживает последствия отказа или отсутствия печени, пока действует. Ничего не лечит, но работает и без печени.
reagent-name-syriniver = сиринивер
reagent-desc-syriniver = Сильное противоядие, выводящее из крови другие вещества ценой здоровья печени. Передозировка с 6 ед.
reagent-name-inacusiate = инакузиат
reagent-desc-inacusiate = Быстро восстанавливает повреждённые уши.
reagent-name-helbital = хелбитал
reagent-desc-helbital = Названное в честь скандинавской богини Хель, это лекарство лечит ушибы тем лучше, чем ближе пациент к смерти. Пациенту не при смерти оно «помогает» лечиться, вызывая удушье. Быстро расходуется. Передозировка ослепляет и отравляет.
reagent-name-libital = либитал
reagent-desc-libital = Средство от ушибов. Слегка повреждает печень, как его ни вводи.
reagent-name-salicylic-acid = салициловая кислота
reagent-desc-salicylic-acid = Ускоряет заживление сильных ушибов. Быстро лечит тяжёлые ушибы и медленно - лёгкие. Передозировка усугубляет имеющиеся ушибы.
reagent-name-aiuri = айури
reagent-desc-aiuri = Применяется при ожогах. Слегка повреждает глаза.
reagent-name-lenturi = лентури
reagent-desc-lenturi = Применяется при ожогах. Повреждает желудок.
reagent-name-oxandrolone = оксандролон
reagent-desc-oxandrolone = Ускоряет заживление сильных ожогов. Быстро лечит тяжёлые ожоги и медленно - лёгкие. Передозировка усугубляет имеющиеся ожоги.
reagent-name-convermol = конвермол
reagent-desc-convermol = Устраняет кислородное голодание, производя немного токсичных побочных продуктов. При передозировке токсины образуются в любом случае.
reagent-name-salbutamol = сальбутамол
reagent-desc-salbutamol = Быстро устраняет кислородное голодание.
reagent-name-seiver = сейвер
reagent-desc-seiver = Лечит отравление, а при сильном охлаждении пациента - и облучение. Повреждает сердце.
reagent-name-granibitaluri = гранибиталури
reagent-desc-granibitaluri = Ускоряет заживление небольших ушибов и ожогов, но бесполезно при тяжёлых травмах. Очень большие дозы токсичны и изнашивают печень.
