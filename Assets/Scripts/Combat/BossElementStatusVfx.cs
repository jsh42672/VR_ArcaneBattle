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

        private GolemCombatTarget subscribedGolemTarget;
        private BossElementStatusSnapshot lastSnapshot;

        public string LastStatusVfxText { get; private set; } = "BossStatusFx: idle";

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
            ApplyStatusColor();
        }

        private void ResolveReferences()
        {
            if (golemTarget == null)
                golemTarget = GetComponent<GolemCombatTarget>() ?? FindAnyObjectByType<GolemCombatTarget>();

            if ((renderers == null || renderers.Length == 0) && golemTarget != null)
                renderers = golemTarget.GetComponentsInChildren<Renderer>(true);
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
            lastSnapshot = snapshot;
            LastStatusVfxText = $"BossStatusFx: {snapshot.combatCue}";
        }

        private void ApplyStatusColor()
        {
            if (renderers == null || renderers.Length == 0)
                return;

            Color? color = null;
            if (lastSnapshot.isBarrierActive)
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
    }
}
