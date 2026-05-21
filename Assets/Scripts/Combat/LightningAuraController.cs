using ArcaneVR.Combat;
using UnityEngine;

public class LightningAuraController : MonoBehaviour
{
    [SerializeField] private GolemCombatTarget golemTarget;
    [SerializeField] private Light[] auraLights;
    [SerializeField] private float idleIntensity = 0f;
    [SerializeField] private float activeIntensity = 1.1f;
    [SerializeField] private Color activeColor = new Color(0.45f, 0.8f, 1f, 1f);

    private GolemCombatTarget subscribedGolemTarget;
    private BossElementStatusSnapshot lastSnapshot;

    public string LastAuraStatus { get; private set; } = "LightningAura: idle";

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
        ApplyAura();
    }

    private void ResolveReferences()
    {
        if (golemTarget == null)
            golemTarget = GetComponent<GolemCombatTarget>() ?? FindAnyObjectByType<GolemCombatTarget>();

        if (auraLights == null || auraLights.Length == 0)
            auraLights = GetComponentsInChildren<Light>(true);
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
        LastAuraStatus = $"LightningAura: {snapshot.combatCue}";
    }

    private void ApplyAura()
    {
        if (auraLights == null || auraLights.Length == 0)
            return;

        var shouldPulse = lastSnapshot.isChargeCounterWindowOpen || lastSnapshot.isBarrierActive || lastSnapshot.isBurning;
        var pulse = shouldPulse ? (0.6f + 0.4f * Mathf.Sin(Time.time * 6f)) : 0f;
        var intensity = Mathf.Lerp(idleIntensity, activeIntensity, shouldPulse ? pulse : 0f);

        foreach (var auraLight in auraLights)
        {
            if (auraLight == null)
                continue;

            auraLight.enabled = intensity > 0.01f;
            auraLight.intensity = intensity;
            auraLight.color = activeColor;
        }
    }
}
