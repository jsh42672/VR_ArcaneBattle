using UnityEngine;

public class LightPulse : MonoBehaviour
{
    public float minIntensity = 1f;
    public float maxIntensity = 5f;
    public float speed = 1f;

    private Light _light;

    void Start()
    {
        _light = GetComponent<Light>();
    }

    void Update()
    {
        if (_light != null)
        {
            float t = (Mathf.Sin(Time.time * speed * 2.5f) + 1f) * 0.5f;
            _light.intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
        }
    }
}
