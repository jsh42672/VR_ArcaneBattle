using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.Combat
{
    [DefaultExecutionOrder(140)]
    public class BossCombatFeedbackController : MonoBehaviour
    {
        [SerializeField] private BossPatternCombatBridge patternBridge;
        [SerializeField] private DodgeDetector dodgeDetector;
        [SerializeField] private BarrierController barrierController;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private GolemCombatTarget golemTarget;

        [Header("Feedback")]
        [SerializeField] private bool enableAudio = true;
        [SerializeField] private bool enableText = true;
        [SerializeField] private float successVolume = 1f;
        [SerializeField] private Vector3 textWorldOffset = new Vector3(0f, 1.05f, 0f);
        [SerializeField] private float textScale = 0.05f;
        [SerializeField] private float textDuration = 0.75f;
        [SerializeField] private float successTextDuration = 1.05f;
        [SerializeField] private Color incomingColor = new Color(1f, 0.66f, 0.08f, 1f);
        [SerializeField] private Color successColor = new Color(0.2f, 1f, 0.55f, 1f);
        [SerializeField] private Color failColor = new Color(1f, 0.22f, 0.12f, 1f);

        private BossPatternCombatBridge subscribedPatternBridge;
        private DodgeDetector subscribedDodgeDetector;
        private BarrierController subscribedBarrierController;
        private CombatManager subscribedCombatManager;
        private GolemCombatTarget subscribedGolemTarget;
        private AudioSource audioSource;
        private AudioClip highWarningClip;
        private AudioClip middleWarningClip;
        private AudioClip lowWarningClip;
        private AudioClip successClip;
        private AudioClip failClip;
        private TextMesh statusText;
        private float textHideTime;
        private float lastSuccessFeedbackTime = -999f;

        public string LastFeedbackStatus { get; private set; } = "BossFeedback: idle";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForBattleScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (!IsBattleScene(sceneName) || FindAnyObjectByType<BossCombatFeedbackController>() != null)
                return;

            var host = GameObject.Find("BattleManager") ??
                       GameObject.Find("Arcane Test Hub") ??
                       new GameObject("Boss Combat Feedback");
            host.AddComponent<BossCombatFeedbackController>();
        }

        private static bool IsBattleScene(string sceneName)
        {
            return sceneName == "ElectricColoseum" ||
                   sceneName == "FireColoseum" ||
                   sceneName == "IceColoseum";
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureAudio();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            ResolveReferences();
            Subscribe();

            if (statusText != null && textHideTime > 0f && Time.time >= textHideTime)
            {
                statusText.gameObject.SetActive(false);
                textHideTime = 0f;
            }

            UpdateStatusTextTransform();
        }

        private void ResolveReferences()
        {
            if (patternBridge == null)
                patternBridge = FindAnyObjectByType<BossPatternCombatBridge>();

            if (dodgeDetector == null)
                dodgeDetector = FindAnyObjectByType<DodgeDetector>();

            if (barrierController == null)
                barrierController = FindAnyObjectByType<BarrierController>();

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();

            if (golemTarget == null)
                golemTarget = FindAnyObjectByType<GolemCombatTarget>();
        }

        private void Subscribe()
        {
            if (subscribedPatternBridge != patternBridge)
            {
                if (subscribedPatternBridge != null)
                    subscribedPatternBridge.OnAttackResponseWindowStarted -= HandleAttackStarted;

                subscribedPatternBridge = patternBridge;
                if (subscribedPatternBridge != null)
                    subscribedPatternBridge.OnAttackResponseWindowStarted += HandleAttackStarted;
            }

            if (subscribedDodgeDetector != dodgeDetector)
            {
                if (subscribedDodgeDetector != null)
                {
                    subscribedDodgeDetector.OnDodgeSuccess -= HandleDodgeSuccess;
                    subscribedDodgeDetector.OnDodgeFail -= HandleDodgeFail;
                }

                subscribedDodgeDetector = dodgeDetector;
                if (subscribedDodgeDetector != null)
                {
                    subscribedDodgeDetector.OnDodgeSuccess += HandleDodgeSuccess;
                    subscribedDodgeDetector.OnDodgeFail += HandleDodgeFail;
                }
            }

            if (subscribedBarrierController != barrierController)
            {
                if (subscribedBarrierController != null)
                    subscribedBarrierController.OnResponseWindowResolved -= HandleBarrierResolved;

                subscribedBarrierController = barrierController;
                if (subscribedBarrierController != null)
                    subscribedBarrierController.OnResponseWindowResolved += HandleBarrierResolved;
            }

            if (subscribedCombatManager != combatManager)
            {
                if (subscribedCombatManager != null)
                    subscribedCombatManager.OnPlayerHit -= HandlePlayerHit;

                subscribedCombatManager = combatManager;
                if (subscribedCombatManager != null)
                    subscribedCombatManager.OnPlayerHit += HandlePlayerHit;
            }

            if (subscribedGolemTarget != golemTarget)
            {
                if (subscribedGolemTarget != null)
                {
                    subscribedGolemTarget.OnChargeCounterSucceeded -= HandleChargeCounterSucceeded;
                    subscribedGolemTarget.OnBarrierBroken -= HandleBossBarrierBroken;
                }

                subscribedGolemTarget = golemTarget;
                if (subscribedGolemTarget != null)
                {
                    subscribedGolemTarget.OnChargeCounterSucceeded += HandleChargeCounterSucceeded;
                    subscribedGolemTarget.OnBarrierBroken += HandleBossBarrierBroken;
                }
            }
        }

        private void Unsubscribe()
        {
            if (subscribedPatternBridge != null)
                subscribedPatternBridge.OnAttackResponseWindowStarted -= HandleAttackStarted;
            if (subscribedDodgeDetector != null)
            {
                subscribedDodgeDetector.OnDodgeSuccess -= HandleDodgeSuccess;
                subscribedDodgeDetector.OnDodgeFail -= HandleDodgeFail;
            }
            if (subscribedBarrierController != null)
                subscribedBarrierController.OnResponseWindowResolved -= HandleBarrierResolved;
            if (subscribedCombatManager != null)
                subscribedCombatManager.OnPlayerHit -= HandlePlayerHit;
            if (subscribedGolemTarget != null)
            {
                subscribedGolemTarget.OnChargeCounterSucceeded -= HandleChargeCounterSucceeded;
                subscribedGolemTarget.OnBarrierBroken -= HandleBossBarrierBroken;
            }

            subscribedPatternBridge = null;
            subscribedDodgeDetector = null;
            subscribedBarrierController = null;
            subscribedCombatManager = null;
            subscribedGolemTarget = null;
        }

        private void HandleAttackStarted(BossAttackType attackType, float duration)
        {
            var message = attackType switch
            {
                BossAttackType.High => "HIGH ATTACK",
                BossAttackType.Middle => "MIDDLE ATTACK",
                BossAttackType.Low => "LOW ATTACK",
                _ => "INCOMING"
            };

            PlayClip(ResolveWarningClip(attackType), 0.85f);
            ShowStatus(message, incomingColor, Mathf.Max(textDuration, Mathf.Min(1.2f, duration)));
            LastFeedbackStatus = $"BossFeedback: incoming {attackType}";
        }

        private void HandleDodgeSuccess()
        {
            PlayPatternSuccess("DODGE");
        }

        private void HandleDodgeFail()
        {
            PlayFail("DODGE FAIL");
        }

        private void HandleBarrierResolved(bool success, string result)
        {
            if (success)
            {
                PlayPatternSuccess("BLOCK");
                return;
            }

            if (string.IsNullOrEmpty(result) || result.Contains("Fail") || result.Contains("timeout"))
                PlayFail("BLOCK FAIL");
        }

        private void HandlePlayerHit(float damage)
        {
            PlayFail($"HIT -{damage:0}");
        }

        private void HandleChargeCounterSucceeded()
        {
            PlayPatternSuccess("COUNTER");
        }

        private void HandleBossBarrierBroken()
        {
            PlayPatternSuccess("BREAK");
        }

        private void PlayPatternSuccess(string message)
        {
            var shouldPlaySound = Time.time - lastSuccessFeedbackTime > 0.18f;
            lastSuccessFeedbackTime = Time.time;

            if (shouldPlaySound)
                PlayClip(successClip ??= CreatePatternSuccessClip(), successVolume);

            ShowStatus(message, successColor, successTextDuration);
            LastFeedbackStatus = $"BossFeedback: {message.ToLowerInvariant()} success";
        }

        private void PlayFail(string message)
        {
            PlayClip(failClip ??= CreateNoiseHitClip(), 0.78f);
            ShowStatus(message, failColor, textDuration);
            LastFeedbackStatus = $"BossFeedback: {message}";
        }

        private AudioClip ResolveWarningClip(BossAttackType attackType)
        {
            switch (attackType)
            {
                case BossAttackType.High:
                    return highWarningClip ??= CreateToneClip("ArcaneHighWarning", 360f, 240f, 0.32f, 0.28f);
                case BossAttackType.Middle:
                    return middleWarningClip ??= CreateToneClip("ArcaneMiddleWarning", 480f, 320f, 0.32f, 0.25f);
                case BossAttackType.Low:
                    return lowWarningClip ??= CreateToneClip("ArcaneLowWarning", 220f, 180f, 0.36f, 0.3f);
                default:
                    return middleWarningClip ??= CreateToneClip("ArcaneMiddleWarning", 480f, 320f, 0.32f, 0.25f);
            }
        }

        private void PlayClip(AudioClip clip, float volume)
        {
            if (!enableAudio || clip == null)
                return;

            EnsureAudio();
            if (audioSource != null)
                audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void EnsureAudio()
        {
            if (audioSource != null)
                return;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.ignoreListenerPause = true;
            audioSource.dopplerLevel = 0f;
            audioSource.priority = 32;
            audioSource.volume = 1f;
        }

        private void ShowStatus(string message, Color color, float duration)
        {
            if (!enableText)
                return;

            EnsureStatusText();
            if (statusText == null)
                return;

            statusText.text = message;
            statusText.color = color;
            statusText.gameObject.SetActive(true);
            textHideTime = Time.time + Mathf.Max(0.1f, duration);
        }

        private void EnsureStatusText()
        {
            if (statusText != null)
                return;

            if (golemTarget == null)
                return;

            var textObject = new GameObject("Arcane Boss Combat Feedback Text");
            textObject.hideFlags = HideFlags.DontSave;
            textObject.transform.position = ResolveTextPosition();
            textObject.transform.rotation = Quaternion.identity;
            textObject.transform.localScale = Vector3.one * textScale;

            statusText = textObject.AddComponent<TextMesh>();
            statusText.anchor = TextAnchor.MiddleCenter;
            statusText.alignment = TextAlignment.Center;
            statusText.fontSize = 92;
            statusText.characterSize = 0.1f;
            statusText.gameObject.SetActive(false);
            UpdateStatusTextTransform();
        }

        private void UpdateStatusTextTransform()
        {
            if (statusText == null || !statusText.gameObject.activeSelf)
                return;

            statusText.transform.position = ResolveTextPosition();
            var cameraTransform = Camera.main != null ? Camera.main.transform : null;
            if (cameraTransform == null)
                return;

            var lookDirection = statusText.transform.position - cameraTransform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
                statusText.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        private Vector3 ResolveTextPosition()
        {
            if (golemTarget == null)
                return transform.position + Vector3.up * 2.5f;

            if (TryGetTargetBounds(out var bounds))
                return bounds.center + Vector3.up * (bounds.extents.y + textWorldOffset.y);

            return golemTarget.transform.position + Vector3.up * 3.2f + textWorldOffset;
        }

        private bool TryGetTargetBounds(out Bounds bounds)
        {
            bounds = new Bounds(golemTarget.transform.position, Vector3.zero);
            var initialized = false;
            foreach (var renderer in golemTarget.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return initialized;
        }

        private static AudioClip CreateToneClip(string clipName, float startFrequency, float endFrequency, float length, float gain)
        {
            const int sampleRate = 44100;
            var sampleCount = Mathf.CeilToInt(sampleRate * Mathf.Max(0.05f, length));
            var samples = new float[sampleCount];

            for (var i = 0; i < sampleCount; i++)
            {
                var t = (float)i / sampleRate;
                var n = sampleCount > 1 ? (float)i / (sampleCount - 1) : 0f;
                var frequency = Mathf.Lerp(startFrequency, endFrequency, n);
                var envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(n)) * Mathf.Exp(-n * 1.6f);
                samples[i] = Mathf.Sin(t * frequency * Mathf.PI * 2f) * envelope * gain;
            }

            var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.hideFlags = HideFlags.DontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreatePatternSuccessClip()
        {
            const int sampleRate = 44100;
            const float length = 0.68f;
            var sampleCount = Mathf.CeilToInt(sampleRate * length);
            var samples = new float[sampleCount];
            var notes = new[] { 523.25f, 659.25f, 783.99f, 1046.5f };
            const float noteLength = 0.13f;

            for (var i = 0; i < sampleCount; i++)
            {
                var time = (float)i / sampleRate;
                var n = sampleCount > 1 ? (float)i / (sampleCount - 1) : 0f;
                var noteIndex = Mathf.Clamp(Mathf.FloorToInt(time / noteLength), 0, notes.Length - 1);
                var localNoteTime = time - noteIndex * noteLength;
                var frequency = notes[noteIndex];

                var attack = Mathf.Clamp01(localNoteTime / 0.018f);
                var noteEnvelope = attack * Mathf.Exp(-localNoteTime * 5.8f);
                var globalEnvelope = Mathf.Clamp01(n / 0.04f) * (1f - n * 0.18f);
                var shimmerEnvelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(n));

                var fundamental = Mathf.Sin(time * frequency * Mathf.PI * 2f);
                var octave = Mathf.Sin(time * frequency * 2f * Mathf.PI * 2f) * 0.35f;
                var fifth = Mathf.Sin(time * frequency * 1.5f * Mathf.PI * 2f) * 0.18f;
                var shimmer = (Random.value * 2f - 1f) * 0.018f * shimmerEnvelope;

                var value = ((fundamental + octave + fifth) * noteEnvelope * 0.28f + shimmer) * globalEnvelope;
                samples[i] = Mathf.Clamp(value, -0.85f, 0.85f);
            }

            var clip = AudioClip.Create("ArcaneBossPatternSuccess", sampleCount, 1, sampleRate, false);
            clip.hideFlags = HideFlags.DontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateNoiseHitClip()
        {
            const int sampleRate = 44100;
            const float length = 0.22f;
            var sampleCount = Mathf.CeilToInt(sampleRate * length);
            var samples = new float[sampleCount];

            for (var i = 0; i < sampleCount; i++)
            {
                var n = sampleCount > 1 ? (float)i / (sampleCount - 1) : 0f;
                var tone = Mathf.Sin(i * 0.08f) * 0.18f;
                var noise = Random.value * 2f - 1f;
                var envelope = Mathf.Exp(-n * 7.5f);
                samples[i] = (tone + noise * 0.32f) * envelope;
            }

            var clip = AudioClip.Create("ArcaneBossHit", sampleCount, 1, sampleRate, false);
            clip.hideFlags = HideFlags.DontSave;
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
