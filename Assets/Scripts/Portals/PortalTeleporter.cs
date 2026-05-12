using UnityEngine;
using UnityEngine.SceneManagement;
using ArcaneVR.Core;

public class PortalTeleporter : MonoBehaviour
{
    public PortalData portalData;
    public bool isExitPortal = false;
    [SerializeField] private float triggerRadius = 0.9f;
    [SerializeField] private bool enableProximityActivation = true;
    [SerializeField] private float proximityActivationRadius = 0.9f;
    [SerializeField] private float proximityVerticalTolerance = 1.8f;
    [SerializeField] private float proximityCheckInterval = 0.05f;
    [SerializeField] private float activationStartDelay = 1.25f;
    [SerializeField] private float maxActivationWorldRadius = 0.9f;

    private Light portalLight;
    private SphereCollider triggerCollider;
    private bool transitionRequested;
    private float nextProximityCheckTime;
    private float activationReadyTime;

    public string LastPortalStatus { get; private set; } = "Portal: idle";
    
    void Start()
    {
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
        triggerCollider.radius = ResolveLocalTriggerRadius(ResolveClampedWorldRadius(triggerRadius));

        var body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();

        body.isKinematic = true;
        body.useGravity = false;
        activationReadyTime = Time.time + Mathf.Max(0f, activationStartDelay);
        LastPortalStatus = "Portal: ready";
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

        if (!IsHeadWithinActivationRange())
            return;

        ActivatePortal("trigger");
    }

    void OnTriggerStay(Collider other)
    {
        if (transitionRequested || !IsActivationReady() || !IsPlayer(other))
            return;

        if (!IsHeadWithinActivationRange())
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
        if (!IsHeadWithinActivationRange())
            return;

        ActivatePortal("proximity");
    }

    private bool IsHeadWithinActivationRange()
    {
        var head = ArcanePlayerRigResolver.FindHeadTransform();
        if (head == null)
        {
            LastPortalStatus = "Portal: waiting head";
            return false;
        }

        var headPosition = head.position;
        var activationCenter = ResolveActivationCenter();
        var horizontalDelta = Vector3.ProjectOnPlane(headPosition - activationCenter, Vector3.up);
        var verticalDelta = Mathf.Abs(headPosition.y - activationCenter.y);
        var horizontalRadius = ResolveEffectiveHorizontalActivationRadius();
        var verticalTolerance = ResolveEffectiveVerticalTolerance();

        if (horizontalDelta.magnitude > horizontalRadius ||
            verticalDelta > verticalTolerance)
        {
            LastPortalStatus = $"Portal: near {horizontalDelta.magnitude:0.0}/{horizontalRadius:0.0}m";
            return false;
        }

        return true;
    }

    private Vector3 ResolveActivationCenter()
    {
        return triggerCollider != null ? triggerCollider.bounds.center : transform.position;
    }

    private float ResolveEffectiveHorizontalActivationRadius()
    {
        return ResolveClampedWorldRadius(proximityActivationRadius);
    }

    private float ResolveEffectiveVerticalTolerance()
    {
        return Mathf.Max(0.25f, proximityVerticalTolerance);
    }

    private float ResolveLocalTriggerRadius(float desiredWorldRadius)
    {
        var scale = transform.lossyScale;
        var maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return desiredWorldRadius / Mathf.Max(0.001f, maxScale);
    }

    private float ResolveClampedWorldRadius(float radius)
    {
        var maxRadius = maxActivationWorldRadius > 0f ? maxActivationWorldRadius : 0.9f;
        return Mathf.Clamp(radius, 0.25f, Mathf.Max(0.25f, maxRadius));
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
            returnWorldScene = "World";

        transitionRequested = true;
        LastPortalStatus = $"Portal: return {returnWorldScene}";
        SceneManager.LoadScene(returnWorldScene);
    }
}
