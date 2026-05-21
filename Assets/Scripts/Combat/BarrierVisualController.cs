using UnityEngine;

namespace ArcaneVR.Combat
{
    [DefaultExecutionOrder(131)]
    public class BarrierVisualController : MonoBehaviour
    {
        [SerializeField] private BarrierController barrierController;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private Color activeColor = new Color(0.25f, 0.65f, 1f, 1f);
        [SerializeField] private Color guardColor = new Color(0.2f, 1f, 0.7f, 1f);

        private BarrierController subscribedBarrierController;
        private bool barrierActive;
        private bool guardActive;

        public string LastBarrierVisualStatus { get; private set; } = "BarrierFx: idle";

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
            ApplyTint();
        }

        private void ResolveReferences()
        {
            if (barrierController == null)
                barrierController = FindAnyObjectByType<BarrierController>();

            if (targetRenderers == null || targetRenderers.Length == 0)
                targetRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
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
        }

        private void HandleGuardPoseChanged(bool active)
        {
            guardActive = active;
            if (!barrierActive)
                LastBarrierVisualStatus = active ? "BarrierFx: guard" : "BarrierFx: idle";
        }

        private void ApplyTint()
        {
            if ((!barrierActive && !guardActive) || targetRenderers == null)
                return;

            var color = barrierActive ? activeColor : guardColor;
            foreach (var targetRenderer in targetRenderers)
            {
                if (targetRenderer == null || targetRenderer.material == null || !targetRenderer.material.HasProperty("_Color"))
                    continue;

                targetRenderer.material.color = Color.Lerp(targetRenderer.material.color, color, Time.deltaTime * 8f);
            }
        }
    }
}
