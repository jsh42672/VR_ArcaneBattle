using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using ArcaneVR.Core;

public class PortalTeleporter : MonoBehaviour
{
    public PortalData portalData;
    public bool isExitPortal = false;
    [Header("Audio")]
    [SerializeField] private AudioClip ambientLoopClip;
    [SerializeField] [Range(0f, 1f)] private float ambientLoopVolume = 0.55f;
    [SerializeField] private float ambientMinDistance = 2f;
    [SerializeField] private float ambientMaxDistance = 14f;
    
    private Light portalLight;
    private AudioSource ambientAudioSource;
    
    void Start()
    {
        // Setup visuals
        portalLight = GetComponentInChildren<Light>();
        if (portalLight != null && portalData != null)
        {
            portalLight.color = portalData.glowColor;
        }

        EnsureAmbientAudio();
        
        // Ensure trigger
        SphereCollider col = GetComponent<SphereCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<SphereCollider>();
        }
        col.isTrigger = true;
        col.radius = 2.5f;
    }

    void OnDisable()
    {
        if (ambientAudioSource != null)
            ambientAudioSource.Stop();
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
        return ArcanePlayerRigResolver.IsPlayerCollider(other);
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

    void EnsureAmbientAudio()
    {
        if (ambientLoopClip == null)
            return;

        ambientAudioSource = GetComponent<AudioSource>();
        if (ambientAudioSource == null)
            ambientAudioSource = gameObject.AddComponent<AudioSource>();

        ambientAudioSource.playOnAwake = false;
        ambientAudioSource.loop = true;
        ambientAudioSource.clip = ambientLoopClip;
        ambientAudioSource.volume = Mathf.Clamp01(ambientLoopVolume);
        ambientAudioSource.spatialBlend = 1f;
        ambientAudioSource.rolloffMode = AudioRolloffMode.Linear;
        ambientAudioSource.minDistance = Mathf.Max(0.1f, ambientMinDistance);
        ambientAudioSource.maxDistance = Mathf.Max(ambientAudioSource.minDistance + 0.1f, ambientMaxDistance);
        ambientAudioSource.dopplerLevel = 0f;

        if (!ambientAudioSource.isPlaying)
            ambientAudioSource.Play();
    }
}
