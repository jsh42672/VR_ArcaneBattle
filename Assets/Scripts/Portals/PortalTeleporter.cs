using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class PortalTeleporter : MonoBehaviour
{
    public PortalData portalData;
    public bool isExitPortal = false;
    
    private Light portalLight;
    
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
        col.radius = 2.5f;
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Detect VR player
        if (IsPlayer(other))
        {
            if (isExitPortal)
            {
                ReturnToWorldMap();
            }
            else
            {
                EnterBattleArena();
            }
        }
    }
    
    bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") || 
               other.name.Contains("XR Origin") ||
               other.GetComponentInParent<CharacterController>() != null;
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
        
        // Load WorldMap
        SceneManager.LoadScene("World");
    }
}
