using System.Collections.Generic;
using ArcaneVR.Boss;
using ArcaneVR.Combat;
using ArcaneVR.Input;
using ArcaneVR.Spell;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("── 차지 경고 ──")]
        [SerializeField] private Color chargeWarningColor = new Color(1f, 0.5f, 0.1f, 1f);
        [SerializeField] private float chargeWarningDuration = 3f;

        [Header("── 취약 강조 ──")]
        [SerializeField] private Color weaknessColor = new Color(1f, 0.85f, 0.2f, 1f);

        [Header("── 피격 풀스크린 이펙트 ──")]
        [SerializeField] private Material fireHitMaterial;
        [SerializeField] private Material iceHitMaterial;
        [SerializeField] private Material thunderHitMaterial;
        [SerializeField] private Material noHpMaterial;
        [Tooltip("피격 이펙트 기본 속성 (씬별 보스에 맞게 설정)")]
        [SerializeField] private ElementType defaultBossElement = ElementType.Thunder;
        [SerializeField] private float screenHitDuration = 0.35f;
        [SerializeField, Range(0f, 1f)] private float noHpThreshold = 0.3f;
        [SerializeField] private float noHpAlpha = 0.22f;
        [SerializeField] private float screenOverlayDistance = 0.32f;

        private readonly Dictionary<Renderer, Color> originalRendererColors = new Dictionary<Renderer, Color>();
        private CombatManager subscribedCombatManager;
        private GolemCombatTarget subscribedGolemTarget;
        private BossAI subscribedBossAI;
        private TextMesh statusText;
        private Transform statusRoot;
        private Canvas hitEffectCanvas;
        private RawImage hitEffectImage;
        private float screenHitEndTime = -1f;
        private float chargeEndTime = -1f;
        private bool weaknessActive;
        private float healthRatio = 1f;
        private float manaRatio = 1f;
        private float hitPulseUntilTime;
        private Color hitPulseColor = Color.white;
        private string lastSpellText = "Spell: idle";
        private string lastBossText = "Boss: idle";
        private string lastCueText = "Cue: idle";
        private string lastVoiceText = "Voice: idle";
        private void Awake()
        {
            SubscribeToResolvedReferences();
            EnsureStatusText();
            RefreshText();
        }

        private void OnEnable()
        {
            SubscribeToResolvedReferences();
        }

        private void OnValidate()
        {
            if ((playerCamera == null || !playerCamera.gameObject.activeInHierarchy) && Camera.main != null)
                playerCamera = Camera.main;
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        private void LateUpdate()
        {
            EnsureStatusText();
            AttachStatusToView();
            UpdateBossHitPulse();
            UpdateHitEffectOverlay();
            RefreshText();
        }

        private void OnDestroy()
        {
            if (hitEffectCanvas != null)
                Destroy(hitEffectCanvas.gameObject);
        }

        public void OnSpellCast(SpellId spellId)
        {
            lastSpellText = $"Spell: {SpellHitData.GetDisplayName(spellId)}";
            hitPulseUntilTime = Time.time + hitPulseDuration;
            hitPulseColor = Color.white;
        }

        private void SubscribeToResolvedReferences()
        {
            if (combatManager != subscribedCombatManager)
            {
                if (subscribedCombatManager != null)
                {
                    subscribedCombatManager.OnPlayerHealthChanged -= HandlePlayerHealthChanged;
                    subscribedCombatManager.OnManaChanged -= HandleManaChanged;
                    subscribedCombatManager.OnPlayerHit -= HandlePlayerHit;
                }

                subscribedCombatManager = combatManager;
                if (subscribedCombatManager != null)
                {
                    subscribedCombatManager.OnPlayerHealthChanged += HandlePlayerHealthChanged;
                    subscribedCombatManager.OnManaChanged += HandleManaChanged;
                    subscribedCombatManager.OnPlayerHit += HandlePlayerHit;
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
                subscribedCombatManager.OnPlayerHit -= HandlePlayerHit;
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
            if (state == BossState.Charging)
                chargeEndTime = Time.unscaledTime + chargeWarningDuration;
            else
                chargeEndTime = -1f;

            weaknessActive = state == BossState.Weakness;
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

            var chargeRemaining = chargeEndTime - Time.unscaledTime;
            var chargeActive = chargeEndTime > 0f && chargeRemaining > 0f;
            if (chargeActive)
                lastCueText = $"Cue: CHARGE {chargeRemaining:0.0}s";
            else if (chargeEndTime > 0f)
                chargeEndTime = -1f;

            var hpWarning = healthRatio <= 0.35f;
            statusText.color = chargeActive ? chargeWarningColor
                : weaknessActive ? weaknessColor
                : hpWarning ? warningTextColor
                : normalTextColor;
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

        private void HandlePlayerHit(float damage)
        {
            screenHitEndTime = Time.unscaledTime + screenHitDuration;
            EnsureHitEffectOverlay();
            var mat = MaterialForElement(defaultBossElement);
            if (mat != null && hitEffectImage != null)
                hitEffectImage.material = mat;
        }

        private void EnsureHitEffectOverlay()
        {
            if (hitEffectCanvas != null || playerCamera == null)
                return;

            var canvasObj = new GameObject("Hit Effect Canvas") { hideFlags = HideFlags.DontSave };
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = playerCamera;
            canvas.planeDistance = screenOverlayDistance;
            canvas.sortingOrder = 99;
            canvasObj.AddComponent<CanvasScaler>();

            var imgObj = new GameObject("Overlay") { hideFlags = HideFlags.DontSave };
            imgObj.transform.SetParent(canvasObj.transform, false);
            var img = imgObj.AddComponent<RawImage>();
            img.rectTransform.anchorMin = Vector2.zero;
            img.rectTransform.anchorMax = Vector2.one;
            img.rectTransform.sizeDelta = Vector2.zero;
            img.color = new Color(1f, 1f, 1f, 0f);

            hitEffectCanvas = canvas;
            hitEffectImage = img;
        }

        private void UpdateHitEffectOverlay()
        {
            EnsureHitEffectOverlay();
            if (hitEffectImage == null)
                return;

            var hitActive = Time.unscaledTime < screenHitEndTime;
            var noHpActive = !hitActive && healthRatio < noHpThreshold && noHpMaterial != null;

            float targetAlpha;
            if (hitActive)
            {
                var t = Mathf.Clamp01((screenHitEndTime - Time.unscaledTime) / Mathf.Max(0.01f, screenHitDuration));
                targetAlpha = t;
            }
            else if (noHpActive)
            {
                if (hitEffectImage.material != noHpMaterial)
                    hitEffectImage.material = noHpMaterial;
                var pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.5f);
                targetAlpha = noHpAlpha * (0.6f + 0.4f * pulse);
            }
            else
            {
                targetAlpha = 0f;
            }

            var c = hitEffectImage.color;
            c.a = Mathf.MoveTowards(c.a, targetAlpha, Time.unscaledDeltaTime * 6f);
            hitEffectImage.color = c;
        }

        private Material MaterialForElement(ElementType element)
        {
            return element switch
            {
                ElementType.Fire => fireHitMaterial,
                ElementType.Ice => iceHitMaterial,
                ElementType.Thunder => thunderHitMaterial,
                _ => thunderHitMaterial
            };
        }
    }
}
