using System;
using ArcaneVR.Spell;
using UnityEngine;

namespace ArcaneVR.Combat
{
    /// <summary>
    /// Unified combat handler. Processes spell-boss collision, applies elemental effects, manages player and boss HP. Fires OnPlayerHit and OnBossHit events.
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        [SerializeField] private float currentMana = 4f;
        [SerializeField] private float maxMana = 4f;

        public event Action<float> OnPlayerHit;
        public event Action<float, ElementType> OnBossHit;

        public bool TryConsumeMana(int slots)
        {
            if (slots <= 0)
                return true;

            if (currentMana < slots)
                return false;

            currentMana = Mathf.Max(0f, currentMana - slots);
            return true;
        }

        public void RefundMana(float slots)
        {
            if (slots <= 0f)
                return;

            currentMana = Mathf.Clamp(currentMana + slots, 0f, maxMana);
        }

        public void ApplyBossHit(SpellProjectile projectile)
        {
            if (projectile == null)
                return;

            OnBossHit?.Invoke(projectile.damage, projectile.element);
        }
    }
}
