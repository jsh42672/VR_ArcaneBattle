using UnityEngine;
using ArcaneVR.Core;

namespace ArcaneVR.Combat
{
    [DefaultExecutionOrder(131)]
    public class BarrierVisualController : MonoBehaviour
    {
        [SerializeField] private BarrierController barrierController;

        [Header("── 쉴드 프리팹 ──")]
        [SerializeField] private GameObject barrierShieldPrefab;
        [Tooltip("비워두면 HMD 헤드 트랜스폼으로 자동 설정됨.")]
        [SerializeField] private Transform barrierShieldAnchor;
        [Tooltip("앵커(헤드) 로컬 좌표계 기준 위치 오프셋. Y 음수로 가슴 높이 조정.")]
        [SerializeField] private Vector3 barrierShieldOffset = new Vector3(0f, -0.5f, 0f);
        [SerializeField] private float barrierShieldScale = 1f;

        [Header("── 렌더러 틴트 효과 ──")]
        [Tooltip("활성화 시 씬의 렌더러들에 색상을 입힘. 화면이 밝아지는 효과를 끄려면 체크 해제.")]
        [SerializeField] private bool enableRendererTint = false;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private Color activeColor = new Color(0.25f, 0.65f, 1f, 1f);
        [SerializeField] private Color guardColor = new Color(0.2f, 1f, 0.7f, 1f);

        private BarrierController subscribedBarrierController;
        private bool barrierActive;
        private bool guardActive;
        private GameObject activeShieldInstance;

        public string LastBarrierVisualStatus { get; private set; } = "BarrierFx: idle";

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            DestroyShield();
        }

        private void Update()
        {
            ResolveReferences();
            Subscribe();
            ApplyTint();
            UpdateShieldTransform();
        }

        private void ResolveReferences()
        {
            if (barrierController == null)
                barrierController = FindAnyObjectByType<BarrierController>();

            if (targetRenderers == null || targetRenderers.Length == 0)
                targetRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);

            if (barrierShieldAnchor == null)
                barrierShieldAnchor = ArcanePlayerRigResolver.FindHeadTransform();
        }

        private void Subscribe()
        {
            if (subscribedBarrierController == barrierController)
                return;

            Unsubscribe();
            subscribedBarrierController = barrierController;
            if (subscribedBarrierController == null)
                return;

            subscribedBarrierController.OnBarrierActiveChanged += HandleBarrierActiveChanged;
            subscribedBarrierController.OnGuardPoseChanged += HandleGuardPoseChanged;
        }

        private void Unsubscribe()
        {
            if (subscribedBarrierController != null)
            {
                subscribedBarrierController.OnBarrierActiveChanged -= HandleBarrierActiveChanged;
                subscribedBarrierController.OnGuardPoseChanged -= HandleGuardPoseChanged;
            }

            subscribedBarrierController = null;
        }

        private void HandleBarrierActiveChanged(bool active)
        {
            barrierActive = active;
            LastBarrierVisualStatus = active ? "BarrierFx: active" : "BarrierFx: idle";
            if (active)
                EnsureShield();
            else
                DestroyShield();
        }

        private void HandleGuardPoseChanged(bool active)
        {
            guardActive = active;
            if (!barrierActive)
                LastBarrierVisualStatus = active ? "BarrierFx: guard" : "BarrierFx: idle";
        }

        private void ApplyTint()
        {
            if (!enableRendererTint || (!barrierActive && !guardActive) || targetRenderers == null)
                return;

            var color = barrierActive ? activeColor : guardColor;
            foreach (var targetRenderer in targetRenderers)
            {
                if (targetRenderer == null || targetRenderer.material == null || !targetRenderer.material.HasProperty("_Color"))
                    continue;

                targetRenderer.material.color = Color.Lerp(targetRenderer.material.color, color, Time.deltaTime * 8f);
            }
        }

        private void EnsureShield()
        {
            if (barrierShieldPrefab == null || activeShieldInstance != null)
                return;

            activeShieldInstance = Instantiate(barrierShieldPrefab);
            activeShieldInstance.transform.localScale = Vector3.one * Mathf.Max(0.01f, barrierShieldScale);
            ApplyHierarchyParticleScaling(activeShieldInstance);
            UpdateShieldTransform();
        }

        private void UpdateShieldTransform()
        {
            if (activeShieldInstance == null || barrierShieldAnchor == null)
                return;

            activeShieldInstance.transform.SetPositionAndRotation(
                barrierShieldAnchor.position + barrierShieldAnchor.rotation * barrierShieldOffset,
                barrierShieldAnchor.rotation);
        }

        private void DestroyShield()
        {
            if (activeShieldInstance != null)
                Destroy(activeShieldInstance);

            activeShieldInstance = null;
        }

        private static void ApplyHierarchyParticleScaling(GameObject root)
        {
            if (root == null)
                return;

            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }
    }
}
