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
        [Header("Player Health")]
        [SerializeField] private float currentHP = 100f;
        [SerializeField] private float maxHP = 100f;

        [Header("Mana")]
        [SerializeField] private float currentMana = 4f;
        [SerializeField] private float maxMana = 4f;
        [SerializeField] private float manaRegenPerSecond = 0.3333333f;
        [SerializeField] private float voiceRefundAmount = 0.5f;
        [SerializeField] private float thunderManaDamage = 1f;
        [SerializeField] private float disruptionDuration = 3f;
        [SerializeField, Range(0f, 1f)] private float disruptedRegenMultiplier = 0.25f;

        public event Action<float> OnPlayerHit;
        public event Action<float, ElementType> OnBossHit;
        public event Action<float, float> OnPlayerHealthChanged;
        public event Action OnPlayerDefeated;
        public event Action<float, float> OnManaChanged;
        public event Action<float, float> OnManaDisrupted;

        private float disruptionRemaining;
        private bool playerDefeatedNotified;

        public float CurrentHP => currentHP;
        public float MaxHP => maxHP;
        public bool IsPlayerDefeated => currentHP <= 0f;
        public float CurrentMana => currentMana;
        public float MaxMana => maxMana;
        public float ManaRegenPerSecond => manaRegenPerSecond;
        public float VoiceRefundAmount => voiceRefundAmount;
        public bool IsManaDisrupted => disruptionRemaining > 0f;
        public float DisruptionRemaining => disruptionRemaining;

        private void Awake()
        {
            currentHP = Mathf.Clamp(currentHP <= 0f ? maxHP : currentHP, 0f, maxHP);
            currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
            NotifyPlayerHealthChanged();
            NotifyManaChanged();
        }

        private void Update()
        {
            if (maxMana <= 0f)
                return;

            if (disruptionRemaining > 0f)
                disruptionRemaining = Mathf.Max(0f, disruptionRemaining - Time.deltaTime);

            var regenMultiplier = IsManaDisrupted ? disruptedRegenMultiplier : 1f;
            var regen = manaRegenPerSecond * regenMultiplier * Time.deltaTime;
            if (regen > 0f && currentMana < maxMana)
                SetMana(currentMana + regen);
        }

        public bool TryConsumeMana(int slots)
        {
            return TryConsumeMana((float)slots);
        }

        public bool TryConsumeMana(float amount)
        {
            if (amount <= 0f)
                return true;

            if (currentMana < amount)
                return false;

            SetMana(currentMana - amount);
            return true;
        }

        public void RefundMana(float slots)
        {
            if (slots <= 0f)
                return;

            SetMana(currentMana + slots);
        }

        public void RefundVoiceMana()
        {
            RefundMana(voiceRefundAmount);
        }

        public void ApplyPlayerHit(float damage)
        {
            if (damage <= 0f)
                return;

            OnPlayerHit?.Invoke(damage);
            SetPlayerHP(currentHP - damage);
        }

        public void ApplyPlayerHit(SpellHitData hitData)
        {
            if (hitData == null)
                return;

            ApplyPlayerHit(hitData.damage);

            if (hitData.IncludesElement(ElementType.Thunder))
                ApplyManaDisruption(thunderManaDamage, disruptionDuration);
        }

        public void ApplyManaDisruption(float manaDamage, float duration)
        {
            if (manaDamage > 0f)
                SetMana(currentMana - manaDamage);

            disruptionRemaining = Mathf.Max(disruptionRemaining, duration);
            OnManaDisrupted?.Invoke(Mathf.Max(0f, manaDamage), disruptionRemaining);
        }

        public void HealPlayer(float amount)
        {
            if (amount <= 0f)
                return;

            SetPlayerHP(currentHP + amount);
        }

        public void RestorePlayerHealth()
        {
            SetPlayerHP(maxHP);
        }

        public void ApplyBossHit(SpellProjectile projectile)
        {
            if (projectile == null)
                return;

            ApplyBossHit(projectile.GetHitData());
        }

        public void ApplyBossHit(SpellHitData hitData)
        {
            if (hitData == null)
                return;

            OnBossHit?.Invoke(hitData.damage, hitData.element);
        }

        private void SetMana(float value)
        {
            var nextMana = Mathf.Clamp(value, 0f, maxMana);
            if (Mathf.Approximately(currentMana, nextMana))
                return;

            currentMana = nextMana;
            NotifyManaChanged();
        }

        private void SetPlayerHP(float value)
        {
            var nextHP = Mathf.Clamp(value, 0f, maxHP);
            if (Mathf.Approximately(currentHP, nextHP))
                return;

            currentHP = nextHP;
            if (currentHP > 0f)
                playerDefeatedNotified = false;

            NotifyPlayerHealthChanged();

            if (currentHP <= 0f && !playerDefeatedNotified)
            {
                playerDefeatedNotified = true;
                OnPlayerDefeated?.Invoke();
            }
        }

        private void NotifyPlayerHealthChanged()
        {
            OnPlayerHealthChanged?.Invoke(currentHP, maxHP);
        }

        private void NotifyManaChanged()
        {
            OnManaChanged?.Invoke(currentMana, maxMana);
        }
    }
}
