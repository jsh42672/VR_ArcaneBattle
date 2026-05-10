using UnityEngine;
using ArcaneVR.Core;

public class PortalSpawnHandler : MonoBehaviour
{
    public PortalData[] worldPortals;
    
    void Start()
    {
        string returnPortalID = PlayerPrefs.GetString("ReturnPortalID", "");
        
        if (!string.IsNullOrEmpty(returnPortalID))
        {
            SpawnAtReturnPortal(returnPortalID);
            PlayerPrefs.DeleteKey("ReturnPortalID");
        }
    }
    
    void SpawnAtReturnPortal(string portalID)
    {
        // Find matching portal data
        PortalData targetPortal = null;
        foreach (var portal in worldPortals)
        {
            if (portal != null && portal.portalID == portalID)
            {
                targetPortal = portal;
                break;
            }
        }
        
        if (targetPortal == null) return;
        
        GameObject player = ArcanePlayerRigResolver.FindPlayerRigGameObject();
        
        if (player != null)
        {
            // Teleport to return position
            player.transform.position = targetPortal.returnPosition + new Vector3(0, 0, 3);
            player.transform.rotation = Quaternion.Euler(0, targetPortal.returnRotationY, 0);
        }
    }
}
