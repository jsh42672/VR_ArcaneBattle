using UnityEngine;

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
        
        // Find XR Origin
        GameObject player = GameObject.Find("XR Origin");
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }
        
        if (player != null && spawnPoint != null)
        {
            // Teleport to spawn
            player.transform.position = spawnPoint.transform.position;
            player.transform.rotation = spawnPoint.transform.rotation;
            
            Debug.Log("Player spawned at arena position");
        }
    }
}
