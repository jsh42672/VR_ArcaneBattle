using UnityEngine;
using ArcaneVR.Core;

public class BattleArenaSpawner : MonoBehaviour
{
    void Start()
    {
        SpawnPlayer();
    }
    
    void SpawnPlayer()
    {
        // Find spawn point
        GameObject spawnPoint = GameObject.Find("PlayerSpawnPoint");
        if (spawnPoint == null)
        {
            spawnPoint = GameObject.FindGameObjectWithTag("Respawn");
        }
        
        GameObject player = ArcanePlayerRigResolver.FindPlayerRigGameObject();
        
        if (player != null && spawnPoint != null)
        {
            // Teleport to spawn
            player.transform.position = spawnPoint.transform.position;
            player.transform.rotation = spawnPoint.transform.rotation;
            
            Debug.Log("Player spawned at arena position");
        }
    }
}
