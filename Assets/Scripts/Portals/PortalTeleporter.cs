using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using ArcaneVR.Core;

public class PortalTeleporter : MonoBehaviour
{
    public PortalData portalData;
    public bool isExitPortal = false;
    public string worldSceneName = "OPenWorld2";
    public float activationRadius = 2.5f;
    public bool usePlayerDistanceFallback = true;
    
    private Light portalLight;
    private bool teleportRequested;
    
    void Start()
    {
        // Setup visuals
        portalLight = GetComponentInChildren<Light>();
        if (portalLight != null && portalData != null)
        {
            portalLight.color = portalData.glowColor;
        }
        
        // Ensure trigger
        SphereCollider col = GetComponent<SphereCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<SphereCollider>();
        }
        col.isTrigger = true;
        col.radius = activationRadius;
    }

    void Update()
    {
        if (!usePlayerDistanceFallback || teleportRequested)
            return;

        Transform player = ArcanePlayerRigResolver.FindHeadTransform();
        if (player == null)
            player = ArcanePlayerRigResolver.FindPlayerRigTransform();

        if (player == null)
            return;

        if (Vector3.Distance(player.position, transform.position) <= activationRadius)
        {
            Teleport();
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Detect VR player
        if (IsPlayer(other))
        {
            Teleport();
        }
    }
    
    bool IsPlayer(Collider other)
    {
        return ArcanePlayerRigResolver.IsPlayerCollider(other);
    }

    void Teleport()
    {
        if (teleportRequested)
            return;

        teleportRequested = true;

        if (isExitPortal)
        {
            ReturnToWorldMap();
        }
        else
        {
            EnterBattleArena();
        }
    }
    
    void EnterBattleArena()
    {
        if (portalData == null) return;
        
        // Save which portal was used
        PortalManager.Instance.SavePortalEntry(portalData.portalID);
        
        // Load battle scene
        SceneManager.LoadScene(portalData.targetSceneName);
    }
    
    void ReturnToWorldMap()
    {
        // Get return portal ID
        string returnPortalID = PortalManager.Instance.GetLastPortalID();
        
        // Save return spawn data
        PlayerPrefs.SetString("ReturnPortalID", returnPortalID);
        
        // Load world map
        SceneManager.LoadScene(worldSceneName);
    }
}
