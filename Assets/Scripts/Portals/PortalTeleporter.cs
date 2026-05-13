using UnityEngine;
using UnityEngine.SceneManagement;
using ArcaneVR.Core;

public class PortalTeleporter : MonoBehaviour
{
    public PortalData portalData;
    public bool isExitPortal = false;
    [SerializeField] private float triggerRadius = 1.15f;
    [SerializeField] private bool enableProximityActivation = true;
    [SerializeField] private float proximityActivationRadius = 1.15f;
    [SerializeField] private float proximityVerticalTolerance = 2.8f;
    [SerializeField] private float proximityCheckInterval = 0.05f;
    [SerializeField] private float activationStartDelay = 0.55f;
    [SerializeField] private float activationDwellDuration = 0.28f;
    [SerializeField] private float maxActivationWorldRadius = 1.35f;
    [SerializeField] private float minimumActivationWorldRadius = 0.35f;
    [SerializeField] private float maximumVerticalTolerance = 3.0f;
    [SerializeField] private float minimumVerticalTolerance = 0.55f;
    [SerializeField] private float visualActivationRadiusMultiplier = 0.12f;
    [SerializeField] private float visualActivationRadiusMax = 0.95f;
    [SerializeField] private bool useTransformPositionForActivation = true;

    private Light portalLight;
    private SphereCollider triggerCollider;
    private bool transitionRequested;
    private float nextProximityCheckTime;
    private float activationReadyTime;
    private float activationDwellStartTime = -1f;

    public string LastPortalStatus { get; private set; } = "Portal: idle";
    
    void Start()
    {
        NormalizeRuntimeActivationSettings();

        // Setup visuals
        portalLight = GetComponentInChildren<Light>();
        if (portalLight != null && portalData != null)
        {
            portalLight.color = portalData.glowColor;
        }
        
        // Ensure trigger
        triggerCollider = GetComponent<SphereCollider>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<SphereCollider>();
        }
        triggerCollider.isTrigger = true;
        if (useTransformPositionForActivation)
            triggerCollider.center = Vector3.zero;
        else if (TryGetVisualBounds(out var visualBounds))
            triggerCollider.center = transform.InverseTransformPoint(visualBounds.center);

        triggerCollider.radius = ResolveLocalTriggerRadius(Mathf.Max(
            ResolveClampedWorldRadius(triggerRadius),
            ResolveEffectiveHorizontalActivationRadius()));

        var body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();

        body.isKinematic = true;
        body.useGravity = false;
        activationReadyTime = Time.time + Mathf.Max(0f, activationStartDelay);
        LastPortalStatus = "Portal: ready";
    }

    private void NormalizeRuntimeActivationSettings()
    {
        triggerRadius = Mathf.Clamp(Mathf.Max(triggerRadius, 1.65f), 0.35f, 2.2f);
        proximityActivationRadius = Mathf.Clamp(Mathf.Max(proximityActivationRadius, 1.65f), 0.35f, 2.2f);
        proximityVerticalTolerance = Mathf.Clamp(Mathf.Max(proximityVerticalTolerance, 3.6f), 0.55f, 4.5f);
        activationStartDelay = Mathf.Min(Mathf.Max(0f, activationStartDelay), 0.35f);
        activationDwellDuration = Mathf.Clamp(activationDwellDuration, 0.12f, 0.35f);
        maxActivationWorldRadius = Mathf.Max(maxActivationWorldRadius, 2.2f);
        maximumVerticalTolerance = Mathf.Max(maximumVerticalTolerance, 4.5f);
        visualActivationRadiusMultiplier = 0f;
        visualActivationRadiusMax = 0f;
        useTransformPositionForActivation = true;
    }

    void Update()
    {
        if (!enableProximityActivation ||
            transitionRequested ||
            !IsActivationReady() ||
            Time.time < nextProximityCheckTime)
        {
            return;
        }

        nextProximityCheckTime = Time.time + Mathf.Max(0.02f, proximityCheckInterval);
        TryActivateByProximity();
    }

    void OnTriggerEnter(Collider other)
    {
        if (transitionRequested || !IsActivationReady() || !IsPlayer(other))
            return;

        if (!IsPlayerReadyForActivation())
            return;

        ActivatePortal("trigger");
    }

    void OnTriggerStay(Collider other)
    {
        if (transitionRequested || !IsActivationReady() || !IsPlayer(other))
            return;

        if (!IsPlayerReadyForActivation())
            return;

        ActivatePortal("trigger-stay");
    }
    
    bool IsPlayer(Collider other)
    {
        return ArcanePlayerRigResolver.IsPlayerCollider(other);
    }

    private bool IsActivationReady()
    {
        if (Time.time >= activationReadyTime)
            return true;

        LastPortalStatus = $"Portal: warmup {activationReadyTime - Time.time:0.0}s";
        return false;
    }

    private void TryActivateByProximity()
    {
        if (!IsPlayerReadyForActivation())
            return;

        ActivatePortal("proximity");
    }

    private bool IsPlayerReadyForActivation()
    {
        if (!IsHeadWithinActivationRange())
        {
            activationDwellStartTime = -1f;
            return false;
        }

        if (activationDwellStartTime < 0f)
            activationDwellStartTime = Time.time;

        var remaining = activationDwellDuration - (Time.time - activationDwellStartTime);
        if (remaining > 0f)
        {
            LastPortalStatus = $"Portal: enter {remaining:0.0}s";
            return false;
        }

        return true;
    }

    private bool IsHeadWithinActivationRange()
    {
        var head = ArcanePlayerRigResolver.FindHeadTransform();
        var playerRig = ArcanePlayerRigResolver.FindPlayerRigTransform();
        if (head == null && playerRig == null)
        {
            LastPortalStatus = "Portal: waiting player";
            return false;
        }

        var activationCenter = ResolveActivationCenter();
        var horizontalRadius = ResolveEffectiveHorizontalActivationRadius();
        var verticalTolerance = ResolveEffectiveVerticalTolerance();
        var closestHorizontal = float.PositiveInfinity;
        var closestVertical = float.PositiveInfinity;

        if (IsPositionWithinActivationRange(
                head != null ? head.position : playerRig.position,
                activationCenter,
                horizontalRadius,
                verticalTolerance,
                out closestHorizontal,
                out closestVertical))
        {
            return true;
        }

        if (playerRig != null &&
            head != null &&
            IsPositionWithinActivationRange(
                playerRig.position,
                activationCenter,
                horizontalRadius,
                verticalTolerance,
                out var rigHorizontal,
                out var rigVertical))
        {
            return true;
        }

        if (playerRig != null && head != null)
        {
            var rigDelta = playerRig.position - activationCenter;
            closestHorizontal = Mathf.Min(closestHorizontal, Vector3.ProjectOnPlane(rigDelta, Vector3.up).magnitude);
            closestVertical = Mathf.Min(closestVertical, Mathf.Abs(rigDelta.y));
        }

        LastPortalStatus = $"Portal: near {closestHorizontal:0.0}/{horizontalRadius:0.0}m y{closestVertical:0.0}/{verticalTolerance:0.0}";
        return false;
    }

    private static bool IsPositionWithinActivationRange(
        Vector3 position,
        Vector3 activationCenter,
        float horizontalRadius,
        float verticalTolerance,
        out float horizontalDistance,
        out float verticalDistance)
    {
        var delta = position - activationCenter;
        horizontalDistance = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;
        verticalDistance = Mathf.Abs(delta.y);
        return horizontalDistance <= horizontalRadius && verticalDistance <= verticalTolerance;
    }

    private Vector3 ResolveActivationCenter()
    {
        if (useTransformPositionForActivation)
            return transform.position;

        if (TryGetVisualBounds(out var visualBounds))
            return visualBounds.center;

        return triggerCollider != null ? triggerCollider.bounds.center : transform.position;
    }

    private float ResolveEffectiveHorizontalActivationRadius()
    {
        var configuredRadius = ResolveClampedWorldRadius(proximityActivationRadius);
        var visualRadius = ResolveVisualActivationRadius();
        return Mathf.Max(configuredRadius, visualRadius);
    }

    private float ResolveEffectiveVerticalTolerance()
    {
        var minTolerance = Mathf.Clamp(minimumVerticalTolerance, 0.25f, 1.2f);
        var configuredMax = maximumVerticalTolerance <= 0f ? 1.65f : maximumVerticalTolerance;
        var maxTolerance = Mathf.Clamp(configuredMax, minTolerance, 4.5f);
        return Mathf.Clamp(proximityVerticalTolerance, minTolerance, maxTolerance);
    }

    private float ResolveLocalTriggerRadius(float desiredWorldRadius)
    {
        var scale = transform.lossyScale;
        var maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return desiredWorldRadius / Mathf.Max(0.001f, maxScale);
    }

    private float ResolveClampedWorldRadius(float radius)
    {
        var maxRadius = Mathf.Max(0.35f, maxActivationWorldRadius);
        var minRadius = Mathf.Clamp(minimumActivationWorldRadius, 0.15f, maxRadius);
        return Mathf.Clamp(radius, minRadius, maxRadius);
    }

    private float ResolveVisualActivationRadius()
    {
        if (!TryGetVisualBounds(out var bounds))
            return 0f;

        var horizontalExtent = Mathf.Max(bounds.extents.x, bounds.extents.z);
        return Mathf.Clamp(
            horizontalExtent * Mathf.Max(0f, visualActivationRadiusMultiplier),
            0f,
            Mathf.Max(0f, visualActivationRadiusMax));
    }

    private bool TryGetVisualBounds(out Bounds bounds)
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        var boundsInitialized = false;
        bounds = new Bounds(transform.position, Vector3.zero);
        if (renderers == null || renderers.Length == 0)
            return false;

        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!boundsInitialized)
            {
                bounds = renderer.bounds;
                boundsInitialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return boundsInitialized;
    }

    private void ActivatePortal(string source)
    {
        if (transitionRequested)
            return;

        LastPortalStatus = $"Portal: activate {source}";

        if (isExitPortal)
            ReturnToWorldMap();
        else
            EnterBattleArena();
    }
    
    void EnterBattleArena()
    {
        if (portalData == null)
        {
            LastPortalStatus = "Portal: missing data";
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(portalData.targetSceneName))
        {
            Debug.LogWarning($"[PortalTeleporter] Target scene is not in Build Settings: {portalData.targetSceneName}");
            LastPortalStatus = $"Portal: missing scene {portalData.targetSceneName}";
            return;
        }

        transitionRequested = true;
        LastPortalStatus = $"Portal: load {portalData.targetSceneName}";
        
        // Save which portal was used
        PortalManager.Instance.SavePortalEntry(portalData.portalID, SceneManager.GetActiveScene().name);
        
        // Load battle scene
        SceneManager.LoadScene(portalData.targetSceneName);
    }
    
    void ReturnToWorldMap()
    {
        // Get return portal ID
        string returnPortalID = PortalManager.Instance.GetLastPortalID();
        
        // Save return spawn data
        PlayerPrefs.SetString("ReturnPortalID", returnPortalID);
        
        // Load the world scene the player originally entered from.
        var returnWorldScene = PortalManager.Instance.GetReturnWorldScene();
        if (!Application.CanStreamedLevelBeLoaded(returnWorldScene))
            returnWorldScene = "World_main";

        transitionRequested = true;
        LastPortalStatus = $"Portal: return {returnWorldScene}";
        SceneManager.LoadScene(returnWorldScene);
    }
}
