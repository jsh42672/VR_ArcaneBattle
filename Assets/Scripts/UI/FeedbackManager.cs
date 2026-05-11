using System.Collections.Generic;
using ArcaneVR.Boss;
using ArcaneVR.Combat;
using ArcaneVR.Input;
using ArcaneVR.Spell;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.UI
{
    /// <summary>
    /// Runtime feedback hub for the prototype battle loop.
    /// Keeps UI asset-light by using TextMesh and simple renderer color pulses.
    /// </summary>
    [DefaultExecutionOrder(90)]
    public class FeedbackManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private SpellCaster spellCaster;
        [SerializeField] private VoiceRecognizer voiceRecognizer;
        [SerializeField] private GolemCombatTarget golemTarget;
        [SerializeField] private BossAI bossAI;
        [SerializeField] private Camera playerCamera;

        [Header("World Status")]
        [SerializeField] private bool createWorldStatusText = true;
        [SerializeField] private Vector3 statusLocalPosition = new Vector3(0.38f, -0.28f, 1.9f);
        [SerializeField] private float statusCharacterSize = 0.011f;
        [SerializeField] private Color normalTextColor = new Color(0.82f, 0.94f, 1f, 1f);
        [SerializeField] private Color warningTextColor = new Color(1f, 0.55f, 0.35f, 1f);

        [Header("Hit Pulse")]
        [SerializeField] private bool pulseBossRenderers = true;
        [SerializeField] private float hitPulseDuration = 0.22f;
        [SerializeField] private Color defaultHitPulseColor = Color.white;

        private readonly Dictionary<Renderer, Color> originalRendererColors = new Dictionary<Renderer, Color>();
        private CombatManager subscribedCombatManager;
        private GolemCombatTarget subscribedGolemTarget;
        private BossAI subscribedBossAI;
        private TextMesh statusText;
        private Transform statusRoot;
        private float healthRatio = 1f;
        private float manaRatio = 1f;
        private float hitPulseUntilTime;
        private Color hitPulseColor = Color.white;
        private string lastSpellText = "Spell: idle";
        private string lastBossText = "Boss: idle";
        private string lastCueText = "Cue: idle";
        private string lastVoiceText = "Voice: idle";
        private float nextReferenceRefreshTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForArcaneScenes()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (!IsArcaneRuntimeScene(sceneName))
                return;

            if (FindAnyObjectByType<FeedbackManager>() != null)
                return;

            var host = GameObject.Find("Arcane Test Hub") ??
                       GameObject.Find("BattleManager") ??
                       new GameObject("FeedbackManager");

            host.AddComponent<FeedbackManager>();
        }

        private static bool IsArcaneRuntimeScene(string sceneName)
        {
            return sceneName == "Main" ||
                   sceneName == "World" ||
                   sceneName == "World_main" ||
                   sceneName == "BattleSceen2" ||
                   sceneName == "BattleScene2" ||
                   sceneName == "Tutorial" ||
                   sceneName.EndsWith("Coloseum");
        }

        private void Awake()
        {
            ResolveReferences();
            SubscribeToResolvedReferences();
            EnsureStatusText();
            RefreshText();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeToResolvedReferences();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        private void LateUpdate()
        {
            if (Time.time >= nextReferenceRefreshTime)
            {
                nextReferenceRefreshTime = Time.time + 0.5f;
                ResolveReferences();
                SubscribeToResolvedReferences();
            }

            EnsureStatusText();
            AttachStatusToView();
            UpdateBossHitPulse();
            RefreshText();
        }

        public void OnSpellCast(SpellId spellId)
        {
            lastSpellText = $"Spell: {SpellHitData.GetDisplayName(spellId)}";
            hitPulseUntilTime = Time.time + hitPulseDuration;
            hitPulseColor = Color.white;
        }

        private void ResolveReferences()
        {
            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();

            if (spellCaster == null)
                spellCaster = FindAnyObjectByType<SpellCaster>();

            if (voiceRecognizer == null)
                voiceRecognizer = FindAnyObjectByType<VoiceRecognizer>();

            if (golemTarget == null)
                golemTarget = FindAnyObjectByType<GolemCombatTarget>();

            if (bossAI == null)
                bossAI = FindAnyObjectByType<BossAI>();

            if (playerCamera == null)
                playerCamera = Camera.main;
        }

        private void SubscribeToResolvedReferences()
        {
            if (combatManager != subscribedCombatManager)
            {
                if (subscribedCombatManager != null)
                {
                    subscribedCombatManager.OnPlayerHealthChanged -= HandlePlayerHealthChanged;
                    subscribedCombatManager.OnManaChanged -= HandleManaChanged;
                }

                subscribedCombatManager = combatManager;
                if (subscribedCombatManager != null)
                {
                    subscribedCombatManager.OnPlayerHealthChanged += HandlePlayerHealthChanged;
                    subscribedCombatManager.OnManaChanged += HandleManaChanged;
                    HandlePlayerHealthChanged(subscribedCombatManager.CurrentHP, subscribedCombatManager.MaxHP);
                    HandleManaChanged(subscribedCombatManager.CurrentMana, subscribedCombatManager.MaxMana);
                }
            }

            if (golemTarget != subscribedGolemTarget)
            {
                if (subscribedGolemTarget != null)
                {
                    subscribedGolemTarget.OnElementStatusChanged -= HandleBossStatusChanged;
                    subscribedGolemTarget.OnSpellDamageApplied -= HandleBossDamageApplied;
                    subscribedGolemTarget.OnCombatCueChanged -= HandleCombatCueChanged;
                }

                subscribedGolemTarget = golemTarget;
                if (subscribedGolemTarget != null)
                {
                    subscribedGolemTarget.OnElementStatusChanged += HandleBossStatusChanged;
                    subscribedGolemTarget.OnSpellDamageApplied += HandleBossDamageApplied;
                    subscribedGolemTarget.OnCombatCueChanged += HandleCombatCueChanged;
                    HandleBossStatusChanged(subscribedGolemTarget.GetStatusSnapshot());
                }
            }

            if (bossAI != subscribedBossAI)
            {
                if (subscribedBossAI != null)
                    subscribedBossAI.OnStateChanged -= HandleBossStateChanged;

                subscribedBossAI = bossAI;
                if (subscribedBossAI != null)
                {
                    subscribedBossAI.OnStateChanged += HandleBossStateChanged;
                    HandleBossStateChanged(subscribedBossAI.CurrentState);
                }
            }
        }

        private void UnsubscribeAll()
        {
            if (subscribedCombatManager != null)
            {
                subscribedCombatManager.OnPlayerHealthChanged -= HandlePlayerHealthChanged;
                subscribedCombatManager.OnManaChanged -= HandleManaChanged;
            }

            if (subscribedGolemTarget != null)
            {
                subscribedGolemTarget.OnElementStatusChanged -= HandleBossStatusChanged;
                subscribedGolemTarget.OnSpellDamageApplied -= HandleBossDamageApplied;
                subscribedGolemTarget.OnCombatCueChanged -= HandleCombatCueChanged;
            }

            if (subscribedBossAI != null)
                subscribedBossAI.OnStateChanged -= HandleBossStateChanged;

            subscribedCombatManager = null;
            subscribedGolemTarget = null;
            subscribedBossAI = null;
        }

        private void HandlePlayerHealthChanged(float current, float max)
        {
            healthRatio = max <= 0f ? 1f : Mathf.Clamp01(current / max);
        }

        private void HandleManaChanged(float current, float max)
        {
            manaRatio = max <= 0f ? 1f : Mathf.Clamp01(current / max);
        }

        private void HandleBossStateChanged(BossState state)
        {
            lastBossText = $"Boss: {state}";
        }

        private void HandleBossStatusChanged(BossElementStatusSnapshot snapshot)
        {
            var hpRatio = snapshot.maxHealth <= 0f ? 0f : snapshot.currentHealth / snapshot.maxHealth;
            var status = snapshot.combatCue;
            if (snapshot.isBarrierActive)
                status = $"Barrier {snapshot.barrierRemaining:0.0}s";
            else if (snapshot.isWeakExposed)
                status = $"Weak {snapshot.weakRemaining:0.0}s";
            else if (snapshot.isChargeCounterWindowOpen)
                status = $"Charge {snapshot.chargeCounterRemaining:0.0}s";

            lastCueText = $"Cue: {status} HP {hpRatio * 100f:0}%";
        }

        private void HandleCombatCueChanged(string cue)
        {
            lastCueText = $"Cue: {cue}";
        }

        private void HandleBossDamageApplied(SpellHitData hitData, float rawDamage, float finalDamage)
        {
            if (hitData != null)
            {
                lastSpellText = $"Hit: {hitData.DisplayName} {finalDamage:0.#}";
                hitPulseColor = ColorForElement(hitData.element);
            }
            else
            {
                lastSpellText = $"Hit: {finalDamage:0.#}";
                hitPulseColor = defaultHitPulseColor;
            }

            hitPulseUntilTime = Time.time + hitPulseDuration;
        }

        private void EnsureStatusText()
        {
            if (!createWorldStatusText || statusText != null)
                return;

            var root = new GameObject("Arcane Feedback Text");
            statusRoot = root.transform;
            statusText = root.AddComponent<TextMesh>();
            statusText.anchor = TextAnchor.UpperLeft;
            statusText.alignment = TextAlignment.Left;
            statusText.characterSize = statusCharacterSize;
            statusText.fontSize = 52;
            statusText.color = normalTextColor;
            statusText.text = "ARCANE FEEDBACK";
        }

        private void AttachStatusToView()
        {
            if (statusRoot == null || playerCamera == null)
                return;

            statusRoot.SetParent(playerCamera.transform, false);
            statusRoot.localPosition = statusLocalPosition;
            statusRoot.localRotation = Quaternion.identity;
        }

        private void RefreshText()
        {
            if (statusText == null)
                return;

            if (voiceRecognizer != null)
                lastVoiceText = voiceRecognizer.ShortStatusText;

            var hpWarning = healthRatio <= 0.35f;
            statusText.color = hpWarning ? warningTextColor : normalTextColor;
            statusText.text =
                $"HP {healthRatio * 100f:0}%  MANA {manaRatio * 100f:0}%\n" +
                $"{lastBossText}\n" +
                $"{lastCueText}\n" +
                $"{lastSpellText}\n" +
                $"Voice: {lastVoiceText}";
        }

        private void UpdateBossHitPulse()
        {
            if (!pulseBossRenderers || golemTarget == null)
                return;

            var renderers = golemTarget.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            var active = Time.time < hitPulseUntilTime;
            var t = active ? Mathf.Clamp01((hitPulseUntilTime - Time.time) / Mathf.Max(0.01f, hitPulseDuration)) : 0f;

            foreach (var targetRenderer in renderers)
            {
                if (targetRenderer == null || targetRenderer.sharedMaterial == null)
                    continue;

                if (!originalRendererColors.ContainsKey(targetRenderer))
                {
                    var mat = targetRenderer.material;
                    originalRendererColors[targetRenderer] = mat.HasProperty("_BaseColor")
                        ? mat.GetColor("_BaseColor")
                        : mat.color;
                }

                var material = targetRenderer.material;
                var baseColor = originalRendererColors[targetRenderer];
                var finalColor = active ? Color.Lerp(baseColor, hitPulseColor, t) : baseColor;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", finalColor);
                else
                    material.color = finalColor;
            }
        }

        private static Color ColorForElement(ElementType element)
        {
            return element switch
            {
                ElementType.Fire => new Color(1f, 0.28f, 0.08f, 1f),
                ElementType.Ice => new Color(0.35f, 0.8f, 1f, 1f),
                ElementType.Thunder => new Color(1f, 0.9f, 0.25f, 1f),
                _ => Color.white
            };
        }
    }
}
