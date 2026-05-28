using System;
using System.Collections;
using ArcaneVR.Spell;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.Combat
{
    /// <summary>
    /// Unified combat handler. Processes spell-boss collision, applies elemental effects, manages player and boss HP. Fires OnPlayerHit and OnBossHit events.
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        [SerializeField] private float currentHP = 100f;
        [SerializeField] private float maxHP = 100f;
        [SerializeField] private float currentMana = 4f;
        [SerializeField] private float maxMana = 4f;
        [SerializeField] private float manaRegenPerSecond = 0.35f;
        [SerializeField] private float voiceRefundAmount = 0.5f;
        [SerializeField] private float thunderManaDamage = 1f;
        [SerializeField] private float disruptionDuration = 3f;
        [SerializeField, Range(0f, 1f)] private float disruptedRegenMultiplier = 0.25f;
        [SerializeField] private float failedDodgeDamage = 10f;
        [SerializeField] private float failedBarrierDamage = 10f;
        [SerializeField] private float genericMeleeDamage = 10f;
        [SerializeField] private bool reloadSceneOnPlayerDeath = true;
        [SerializeField] private float deathSceneReloadDelay = 2f;

        public event Action<float> OnPlayerHit;
        public event Action<float, float> OnPlayerHealthChanged;
        public event Action OnPlayerDied;
        public event Action<float, ElementType> OnBossHit;
        public event Action<float, float> OnManaChanged;
        public event Action<float, float> OnManaDisrupted;

        private float disruptionRemaining;
        private bool isPlayerDead;
        private Coroutine deathRoutine;

        public float CurrentMana => currentMana;
        public float MaxMana => maxMana;
        public float CurrentHP => currentHP;
        public float MaxHP => maxHP;
        public float ManaRegenPerSecond => manaRegenPerSecond;
        public float VoiceRefundAmount => voiceRefundAmount;
        public float FailedDodgeDamage => failedDodgeDamage;
        public float FailedBarrierDamage => failedBarrierDamage;
        public float GenericMeleeDamage => genericMeleeDamage;
        public bool IsPlayerDead => isPlayerDead;
        public bool IsManaDisrupted => disruptionRemaining > 0f;
        public float DisruptionRemaining => disruptionRemaining;

        private void Awake()
        {
            currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
            currentHP = Mathf.Clamp(currentHP <= 0f ? maxHP : currentHP, 0f, maxHP);
            isPlayerDead = currentHP <= 0f;
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
            ApplyPlayerHit(damage, "Unknown");
        }

        public void ApplyPlayerHit(float damage, string source)
        {
            if (damage <= 0f || isPlayerDead)
                return;

            var previousHP = currentHP;
            currentHP = Mathf.Max(0f, currentHP - damage);
            NotifyPlayerHealthChanged();
            Debug.Log($"[PlayerHP] Source:{source} Damage:{damage:0.#} HP:{previousHP:0.#}->{currentHP:0.#}/{maxHP:0.#}");
            OnPlayerHit?.Invoke(damage);
            if (currentHP <= 0f)
                KillPlayer();
        }

        public void ApplyPlayerHit(SpellHitData hitData)
        {
            if (hitData == null || isPlayerDead)
                return;

            ApplyPlayerHit(hitData.damage);

            if (!isPlayerDead && hitData.IncludesElement(ElementType.Thunder))
                ApplyManaDisruption(thunderManaDamage, disruptionDuration);
        }

        public void ApplyManaDisruption(float manaDamage, float duration)
        {
            if (manaDamage > 0f)
                SetMana(currentMana - manaDamage);

            disruptionRemaining = Mathf.Max(disruptionRemaining, duration);
            OnManaDisrupted?.Invoke(Mathf.Max(0f, manaDamage), disruptionRemaining);
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

        private void NotifyManaChanged()
        {
            OnManaChanged?.Invoke(currentMana, maxMana);
        }

        private void NotifyPlayerHealthChanged()
        {
            OnPlayerHealthChanged?.Invoke(currentHP, maxHP);
        }

        private void KillPlayer()
        {
            if (isPlayerDead)
                return;

            isPlayerDead = true;
            Debug.Log("[PlayerHP] Player died");
            OnPlayerDied?.Invoke();

            // Hook game-over visuals/audio/UI here before the scene reload starts.
            if (reloadSceneOnPlayerDeath && deathRoutine == null)
                deathRoutine = StartCoroutine(ReloadSceneAfterPlayerDeath());
        }

        private IEnumerator ReloadSceneAfterPlayerDeath()
        {
            var delay = Mathf.Max(0f, deathSceneReloadDelay);
            Debug.Log($"[PlayerHP] Restarting scene in {delay:0.#}s");

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            var scene = SceneManager.GetActiveScene();
            Debug.Log($"[PlayerHP] Restarting scene: {scene.name}");
            SceneManager.LoadScene(scene.buildIndex);
        }
    }
}
