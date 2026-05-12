using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public class WristAuraController : MonoBehaviour
{
    [Header("References")]
    public VisualEffect auraVFX;
    public Light auraLight;
    public MonoBehaviour skeleton; 

    [Header("Mana Settings")]
    [Range(0f, 1f)] public float ManaPct = 1f;
    public float maxLightIntensity = 2f;
    public float maxLightRange = 0.4f;

    [Header("Flicker Settings")]
    public float flickerThreshold = 0.2f;
    public float flickerSpeed = 8f;

    [Header("Aura Color")]
    public Color auraColor = new Color(0.655f, 0.545f, 0.988f); // #A78BFA

    private float _smoothedMana = 1f;
    private float _flickerTimer = 0f;
    private bool _wasZero = false;

    void Start()
    {
        if (auraVFX == null) auraVFX = GetComponent<VisualEffect>();
        _smoothedMana = ManaPct;
        ApplyMana(_smoothedMana);
    }

    void Update()
    {
        FollowWristBone();
        SmoothMana();
        ApplyMana(_smoothedMana);
    }

    void FollowWristBone()
    {
        if (skeleton == null) return;
        
        try 
        {
            var bonesProp = skeleton.GetType().GetProperty("Bones");
            if (bonesProp == null) return;
            var bones = bonesProp.GetValue(skeleton) as System.Collections.IEnumerable;
            if (bones == null) return;

            foreach (var bone in bones)
            {
                var idProp = bone.GetType().GetProperty("Id");
                if (idProp == null) continue;
                int idValue = (int)idProp.GetValue(bone);
                
                if (idValue == 0) // Hand_WristRoot
                {
                    var transProp = bone.GetType().GetProperty("Transform");
                    if (transProp == null) continue;
                    Transform trans = transProp.GetValue(bone) as Transform;
                    if (trans != null)
                    {
                        transform.position = trans.position;
                        transform.rotation = trans.rotation;
                    }
                    break;
                }
            }
        }
        catch {}
    }

    void SmoothMana()
    {
        _smoothedMana = Mathf.Lerp(_smoothedMana, ManaPct, Time.deltaTime * 5f);
        if (_smoothedMana < 0.005f) _smoothedMana = 0f;
    }

    void ApplyMana(float m)
    {
        if (auraVFX != null)
        {
            bool shouldPlay = m > 0f;
            if (shouldPlay && _wasZero) { auraVFX.Play(); _wasZero = false; }
            else if (!shouldPlay && !_wasZero) { auraVFX.Stop(); _wasZero = true; }

            auraVFX.SetFloat("ManaPct", m);
            auraVFX.SetFloat("SpawnRate", Mathf.Lerp(0f, 100f, m));
            auraVFX.SetFloat("ParticleSize", Mathf.Lerp(0f, 1f, m));
            auraVFX.SetFloat("OrbitRadius", Mathf.Lerp(0f, 0.08f, m));
            auraVFX.SetFloat("RingAlpha", ComputeAlpha(m));
            auraVFX.SetVector4("AuraColor", auraColor);
        }

        if (auraLight != null)
        {
            if (m <= 0f) { auraLight.enabled = false; return; }
            auraLight.enabled = true;
            auraLight.color = auraColor;
            auraLight.range = maxLightRange * m;

            if (m <= flickerThreshold)
            {
                _flickerTimer += Time.deltaTime * flickerSpeed;
                float flicker = Mathf.Abs(Mathf.Sin(_flickerTimer)) * (m / flickerThreshold);
                auraLight.intensity = maxLightIntensity * flicker * 0.5f;
            }
            else
            {
                _flickerTimer = 0f;
                auraLight.intensity = maxLightIntensity * m;
            }
        }
    }

    float ComputeAlpha(float m)
    {
        if (m <= 0f) return 0f;
        if (m <= flickerThreshold)
        {
            float flicker = Mathf.Abs(Mathf.Sin(_flickerTimer * flickerSpeed));
            return Mathf.Lerp(0f, 0.4f, m / flickerThreshold) * flicker;
        }
        return Mathf.Lerp(0.1f, 1f, m);
    }
}
