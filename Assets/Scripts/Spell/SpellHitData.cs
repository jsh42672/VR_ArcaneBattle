using System;

namespace ArcaneVR.Spell
{
    [Serializable]
    public class SpellHitData
    {
        public SpellId spellId;
        public ElementType element;
        public StatusEffect statusEffect;
        public float damage;
        public float statusDuration;
        public float statusMagnitude;
        public float statusTickInterval;

        public SpellHitData()
        {
        }

        public SpellHitData(
            SpellId spellId,
            ElementType element,
            StatusEffect statusEffect,
            float damage,
            float statusDuration,
            float statusMagnitude,
            float statusTickInterval)
        {
            this.spellId = spellId;
            this.element = element;
            this.statusEffect = statusEffect;
            this.damage = damage;
            this.statusDuration = statusDuration;
            this.statusMagnitude = statusMagnitude;
            this.statusTickInterval = statusTickInterval;
        }

        public bool IsComboSpell =>
            IsComboSpellId(spellId);

        public string DisplayName => GetDisplayName(spellId);

        public bool IncludesElement(ElementType query)
        {
            if (query == ElementType.None)
                return false;

            if (element == query)
                return true;

            return spellId switch
            {
                SpellId.Combo_FireIce => query == ElementType.Fire || query == ElementType.Ice,
                SpellId.Combo_IceThunder => query == ElementType.Ice || query == ElementType.Thunder,
                SpellId.Combo_ThunderFire => query == ElementType.Thunder || query == ElementType.Fire,
                _ => false
            };
        }

        public SpellHitData Clone()
        {
            return new SpellHitData(
                spellId,
                element,
                statusEffect,
                damage,
                statusDuration,
                statusMagnitude,
                statusTickInterval);
        }

        public static bool IsComboSpellId(SpellId id)
        {
            return id == SpellId.Combo_FireIce ||
                   id == SpellId.Combo_IceThunder ||
                   id == SpellId.Combo_ThunderFire;
        }

        public static string GetDisplayName(SpellId id)
        {
            return id switch
            {
                SpellId.Single_Pointer => "Fire",
                SpellId.Single_Wave => "Ice",
                SpellId.Single_Strike => "Thunder",
                SpellId.Combo_FireIce => "Steam Burst",
                SpellId.Combo_IceThunder => "Barrier Break",
                SpellId.Combo_ThunderFire => "Overload Flame",
                _ => id.ToString()
            };
        }

        public static bool TryGetComboElements(SpellId id, out ElementType first, out ElementType second)
        {
            switch (id)
            {
                case SpellId.Combo_FireIce:
                    first = ElementType.Fire;
                    second = ElementType.Ice;
                    return true;
                case SpellId.Combo_IceThunder:
                    first = ElementType.Ice;
                    second = ElementType.Thunder;
                    return true;
                case SpellId.Combo_ThunderFire:
                    first = ElementType.Thunder;
                    second = ElementType.Fire;
                    return true;
                default:
                    first = ElementType.None;
                    second = ElementType.None;
                    return false;
            }
        }

        public static SpellId ResolveComboSpell(ElementType first, ElementType second)
        {
            if (HasPair(first, second, ElementType.Fire, ElementType.Ice))
                return SpellId.Combo_FireIce;

            if (HasPair(first, second, ElementType.Ice, ElementType.Thunder))
                return SpellId.Combo_IceThunder;

            if (HasPair(first, second, ElementType.Thunder, ElementType.Fire))
                return SpellId.Combo_ThunderFire;

            return SpellId.None;
        }

        public static SpellHitData CreateComboHitData(SpellId id)
        {
            return id switch
            {
                SpellId.Combo_FireIce => new SpellHitData(id, ElementType.Fire, StatusEffect.Stagger, 24f, 3f, 1.2f, 0f),
                SpellId.Combo_IceThunder => new SpellHitData(id, ElementType.Ice, StatusEffect.Slow, 18f, 4f, 1f, 0f),
                SpellId.Combo_ThunderFire => new SpellHitData(id, ElementType.Thunder, StatusEffect.Burn, 34f, 5f, 3.5f, 0.75f),
                _ => new SpellHitData()
            };
        }

        private static bool HasPair(ElementType first, ElementType second, ElementType a, ElementType b)
        {
            return first == a && second == b || first == b && second == a;
        }
    }
}
