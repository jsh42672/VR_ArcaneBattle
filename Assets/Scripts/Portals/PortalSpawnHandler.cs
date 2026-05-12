using System.Collections;
using UnityEngine;
using ArcaneVR.Core;

public class PortalSpawnHandler : MonoBehaviour
{
    public PortalData[] worldPortals;
    [SerializeField] private float returnForwardOffset = 3f;
    [SerializeField] private int spawnRetryFrames = 10;
    
    void Start()
    {
        string returnPortalID = PlayerPrefs.GetString("ReturnPortalID", "");
        
        if (!string.IsNullOrEmpty(returnPortalID))
        {
            StartCoroutine(SpawnAtReturnPortalWhenReady(returnPortalID));
            PlayerPrefs.DeleteKey("ReturnPortalID");
        }
    }
    
    IEnumerator SpawnAtReturnPortalWhenReady(string portalID)
    {
        for (int attempt = 0; attempt <= spawnRetryFrames; attempt++)
        {
            if (TrySpawnAtReturnPortal(portalID))
                yield break;

            yield return null;
        }

        Debug.LogWarning($"[PortalSpawnHandler] Could not place player at return portal: {portalID}");
    }

    bool TrySpawnAtReturnPortal(string portalID)
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
        
        if (targetPortal == null) return false;
        
        GameObject player = ArcanePlayerRigResolver.FindPlayerRigGameObject();
        
        if (player != null)
        {
            // Teleport to return position
            ResolveReturnPose(targetPortal, out var basePosition, out var returnRotation);
            var returnPosition = basePosition + returnRotation * Vector3.forward * returnForwardOffset;
            player.transform.SetPositionAndRotation(returnPosition, returnRotation);
            return true;
        }

        return false;
    }

    private static void ResolveReturnPose(PortalData targetPortal, out Vector3 position, out Quaternion rotation)
    {
        position = targetPortal.returnPosition;
        rotation = Quaternion.Euler(0f, targetPortal.returnRotationY, 0f);

        var portalTransform = FindScenePortalTransform(targetPortal);
        if (portalTransform == null)
            return;

        position = portalTransform.position;

        var flatForward = Vector3.ProjectOnPlane(portalTransform.forward, Vector3.up);
        if (flatForward.sqrMagnitude > 0.01f)
            rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
    }

    private static Transform FindScenePortalTransform(PortalData targetPortal)
    {
        foreach (var teleporter in FindObjectsByType<PortalTeleporter>(FindObjectsInactive.Include))
        {
            if (teleporter == null || teleporter.isExitPortal || teleporter.portalData == null)
                continue;

            if (teleporter.portalData == targetPortal ||
                teleporter.portalData.portalID == targetPortal.portalID)
            {
                return teleporter.transform;
            }
        }

        return null;
    }
}
