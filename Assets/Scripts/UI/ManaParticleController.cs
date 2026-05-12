using UnityEngine;
using ArcaneVR.Combat;

namespace ArcaneVR.Feedback
{
    /// <summary>
    /// 파티클 개수는 유지하면서, 머티리얼 Emission 밝기와 Light intensity만
    /// 마나 비율에 따라 조절합니다.
    /// MaterialPropertyBlock을 사용하므로 공유 머티리얼에 영향을 주지 않습니다.
    /// </summary>
    public class ManaParticleController : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("비워두면 씬에서 자동 탐색")]
        [SerializeField] private CombatManager combatManager;

        [Tooltip("밝기를 조절할 파티클 시스템 (여러 개 가능)")]
        [SerializeField] private ParticleSystem[] targetParticles;

        [Tooltip("함께 조절할 Light (없으면 비워두기)")]
        [SerializeField] private Light[] targetLights;

        [Header("Emission 밝기 범위 (HDR)")]
        [Tooltip("마나 0%일 때 머티리얼 Emission 밝기 배율")]
        [SerializeField] private float emissionIntensityMin = 0f;

        [Tooltip("마나 100%일 때 머티리얼 Emission 밝기 배율")]
        [SerializeField] private float emissionIntensityMax = 3f;

        [Header("Light Intensity 범위")]
        [SerializeField] private float lightIntensityMin = 0f;
        [SerializeField] private float lightIntensityMax = 3f;

        [Header("보간 속도")]
        [Tooltip("0이면 즉시 반영, 높을수록 빠르게 따라감")]
        [SerializeField] private float smoothSpeed = 4f;

        // 리플렉션 캐싱
        private System.Reflection.FieldInfo _currentManaField;
        private System.Reflection.FieldInfo _maxManaField;

        // 파티클 렌더러 & PropertyBlock 캐싱
        private ParticleSystemRenderer[] _renderers;
        private MaterialPropertyBlock _propBlock;

        private float _smoothedRatio = 1f;

        // ─────────────────────────────────────────────
        private void Awake()
        {
            if (combatManager == null)
                combatManager = FindObjectOfType<CombatManager>();

            if (combatManager == null)
            {
                Debug.LogWarning("[ManaParticleController] CombatManager를 찾을 수 없습니다.");
                enabled = false;
                return;
            }

            // private 필드 리플렉션 캐싱
            var type = typeof(CombatManager);
            var flags = System.Reflection.BindingFlags.NonPublic
                      | System.Reflection.BindingFlags.Instance;
            _currentManaField = type.GetField("currentMana", flags);
            _maxManaField = type.GetField("maxMana", flags);

            if (_currentManaField == null || _maxManaField == null)
            {
                Debug.LogError("[ManaParticleController] currentMana / maxMana 필드를 찾지 못했습니다.");
                enabled = false;
                return;
            }

            // ParticleSystemRenderer 캐싱
            _propBlock = new MaterialPropertyBlock();

            if (targetParticles != null)
            {
                _renderers = new ParticleSystemRenderer[targetParticles.Length];
                for (int i = 0; i < targetParticles.Length; i++)
                {
                    if (targetParticles[i] != null)
                        _renderers[i] = targetParticles[i].GetComponent<ParticleSystemRenderer>();
                }
            }
        }

        private void Update()
        {
            float ratio = GetManaRatio();

            _smoothedRatio = smoothSpeed > 0f
                ? Mathf.Lerp(_smoothedRatio, ratio, Time.deltaTime * smoothSpeed)
                : ratio;

            ApplyEmissionBrightness(_smoothedRatio);
            ApplyLightIntensity(_smoothedRatio);
        }

        // ─────────────────────────────────────────────
        private float GetManaRatio()
        {
            float current = (float)_currentManaField.GetValue(combatManager);
            float max = (float)_maxManaField.GetValue(combatManager);
            return max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }

        /// <summary>
        /// MaterialPropertyBlock으로 _EmissionColor의 HDR 밝기만 변경.
        /// 파티클 개수·색상은 전혀 건드리지 않습니다.
        /// </summary>
        private void ApplyEmissionBrightness(float ratio)
        {
            if (_renderers == null) return;

            float intensity = Mathf.Lerp(emissionIntensityMin, emissionIntensityMax, ratio);

            foreach (var r in _renderers)
            {
                if (r == null) continue;

                r.GetPropertyBlock(_propBlock);

                // 기존 색상을 유지한 채 HDR 밝기만 올리거나 낮춤
                Color baseColor = _propBlock.GetColor("_BaseColor");
                if (baseColor == Color.clear) baseColor = Color.white;

                _propBlock.SetColor("_EmissionColor", baseColor * intensity);

                r.SetPropertyBlock(_propBlock);
            }
        }

        private void ApplyLightIntensity(float ratio)
        {
            if (targetLights == null) return;

            float intensity = Mathf.Lerp(lightIntensityMin, lightIntensityMax, ratio);
            foreach (var lt in targetLights)
            {
                if (lt != null) lt.intensity = intensity;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (combatManager == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, combatManager.transform.position);
        }
#endif
    }
}
