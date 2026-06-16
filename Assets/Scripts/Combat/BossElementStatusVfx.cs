using UnityEngine;

namespace ArcaneVR.Combat
{
    [DefaultExecutionOrder(132)]
    public class BossElementStatusVfx : MonoBehaviour
    {
        [SerializeField] private GolemCombatTarget golemTarget;
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Color barrierColor = new Color(0.2f, 0.55f, 1f, 1f);
        [SerializeField] private Color weakColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color chargeColor = new Color(0.8f, 0.35f, 1f, 1f);
        [SerializeField] private Color burnColor = new Color(1f, 0.35f, 0.1f, 1f);
        [SerializeField] private Color slowColor = new Color(0.25f, 0.85f, 1f, 1f);
        [SerializeField] private Color staggerColor = new Color(1f, 0.95f, 0.3f, 1f);
        [SerializeField] private GameObject barrierShieldPrefab;
        [SerializeField] private Transform barrierShieldAnchor;
        [SerializeField] private Vector3 barrierShieldOffset;
        [SerializeField] private float barrierShieldScale = 1f;

        private GolemCombatTarget subscribedGolemTarget;
        private BossElementStatusSnapshot lastSnapshot;
        private GameObject activeBarrierShield;

        public string LastStatusVfxText { get; private set; } = "BossStatusFx: idle";

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            DestroyBarrierShield();
        }

        private void Update()
        {
            ResolveReferences();
            Subscribe();
            ApplyStatusColor();
            UpdateBarrierShieldTransform();
        }

        private void ResolveReferences()
        {
            if (golemTarget == null)
                golemTarget = GetComponent<GolemCombatTarget>() ?? FindAnyObjectByType<GolemCombatTarget>();

            if ((renderers == null || renderers.Length == 0) && golemTarget != null)
                renderers = golemTarget.GetComponentsInChildren<Renderer>(true);

            if (barrierShieldAnchor == null && golemTarget != null)
                barrierShieldAnchor = golemTarget.transform;
        }

        private void Subscribe()
        {
            if (subscribedGolemTarget == golemTarget)
                return;

            Unsubscribe();
            subscribedGolemTarget = golemTarget;
            if (subscribedGolemTarget == null)
                return;

            subscribedGolemTarget.OnElementStatusChanged += HandleStatusChanged;
            HandleStatusChanged(subscribedGolemTarget.GetStatusSnapshot());
        }

        private void Unsubscribe()
        {
            if (subscribedGolemTarget != null)
                subscribedGolemTarget.OnElementStatusChanged -= HandleStatusChanged;

            subscribedGolemTarget = null;
        }

        private void HandleStatusChanged(BossElementStatusSnapshot snapshot)
        {
            var wasBarrierActive = lastSnapshot.isBarrierActive;
            lastSnapshot = snapshot;
            LastStatusVfxText = $"BossStatusFx: {snapshot.combatCue}";

            if (!wasBarrierActive && snapshot.isBarrierActive)
                EnsureBarrierShield();
            else if (wasBarrierActive && !snapshot.isBarrierActive)
                DestroyBarrierShield();
        }

        private void ApplyStatusColor()
        {
            if (renderers == null || renderers.Length == 0)
                return;

            Color? color = null;
            if (lastSnapshot.isStaggered)
                color = staggerColor;
            else if (lastSnapshot.isBurning)
                color = burnColor;
            else if (lastSnapshot.isSlowed)
                color = slowColor;
            else if (lastSnapshot.isBarrierActive)
                color = barrierColor;
            else if (lastSnapshot.isWeakExposed)
                color = weakColor;
            else if (lastSnapshot.isChargeCounterWindowOpen)
                color = chargeColor;

            if (!color.HasValue)
                return;

            foreach (var targetRenderer in renderers)
            {
                if (targetRenderer == null || targetRenderer.material == null || !targetRenderer.material.HasProperty("_Color"))
                    continue;

                targetRenderer.material.color = Color.Lerp(targetRenderer.material.color, color.Value, Time.deltaTime * 5f);
            }
        }

        private void EnsureBarrierShield()
        {
            if (barrierShieldPrefab == null || activeBarrierShield != null)
                return;

            activeBarrierShield = Instantiate(barrierShieldPrefab);
            activeBarrierShield.transform.localScale = Vector3.one * Mathf.Max(0.01f, barrierShieldScale);
            ApplyHierarchyParticleScaling(activeBarrierShield);
            UpdateBarrierShieldTransform();
        }

        private void UpdateBarrierShieldTransform()
        {
            if (activeBarrierShield == null || barrierShieldAnchor == null)
                return;

            activeBarrierShield.transform.SetPositionAndRotation(
                barrierShieldAnchor.position + barrierShieldAnchor.rotation * barrierShieldOffset,
                barrierShieldAnchor.rotation);
        }

        private void DestroyBarrierShield()
        {
            if (activeBarrierShield != null)
                Destroy(activeBarrierShield);

            activeBarrierShield = null;
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
