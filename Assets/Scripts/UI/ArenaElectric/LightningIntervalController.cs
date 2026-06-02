using UnityEngine;
using DigitalRuby.LightningBolt;

[RequireComponent(typeof(LightningBoltScript))]
public class LightningIntervalController : MonoBehaviour
{
    [Header("Timing Settings")]
    [Tooltip("Minimum time between strikes (seconds)")]
    public float minInterval = 1.5f;
    
    [Tooltip("Maximum time between strikes (seconds)")]
    public float maxInterval = 5f;

    [Tooltip("How long the bolt stays visible (seconds)")]
    public float strikeDuration = 0.2f;

    private LightningBoltScript _bolt;
    private float _nextTriggerTime;

    void Awake()
    {
        _bolt = GetComponent<LightningBoltScript>();
        
        // Force manual mode so we control the timing
        _bolt.ManualMode = true;
        _bolt.Duration = strikeDuration;
        
        ScheduleNext();
    }

    void Update()
    {
        if (Time.time >= _nextTriggerTime)
        {
            // Randomize duration slightly for each strike if desired
            _bolt.Duration = strikeDuration * Random.Range(0.8f, 1.2f);
            
            _bolt.Trigger();
            ScheduleNext();
        }
    }

    void ScheduleNext()
    {
        // Wait random interval before next strike
        _nextTriggerTime = Time.time + Random.Range(minInterval, maxInterval);
    }
}
