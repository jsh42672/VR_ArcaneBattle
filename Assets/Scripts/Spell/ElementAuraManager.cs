using UnityEngine;

namespace ArcaneVR.Spell
{
    /// <summary>
    /// Manages the single shared AuraParticles system.
    /// Activates it with the correct element color when a gesture is confirmed,
    /// and tracks the hand position each frame.
    /// </summary>
    public class ElementAuraManager : MonoBehaviour
    {
        [SerializeField] private ParticleSystem auraParticles;
        [SerializeField] private string timeFocusExemptLayerName = "TimeFocusExempt";

        [Header("── 속성별 오라 색상 ──")]
        [SerializeField] private Color fireColor    = new Color(1.00f, 0.05f, 0.00f, 1f); // intense red
        [SerializeField] private Color iceColor     = new Color(0.45f, 0.85f, 1.00f, 1f); // sky blue
        [SerializeField] private Color thunderColor = new Color(1.00f, 0.95f, 0.00f, 1f); // clear yellow

        [Header("── 보이스 강화 반영 ──")]
        [SerializeField] private float voiceBoostSizeMultiplier = 2.4f;

        private Transform _followTarget;
        private bool _isShowing;
        private bool _suppressed;
        private bool _voiceBoosted;
        private float _baseStartSizeMultiplier = 1f;
        private ElementType _currentElement = ElementType.None;
        private Material _materialInstance;
        private ParticleSystemRenderer _particleRenderer;

        public bool IsShowing => _isShowing && !_suppressed;

        private void Awake()
        {
            if (auraParticles == null) return;

            var main = auraParticles.main;
            _baseStartSizeMultiplier = main.startSizeMultiplier;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;
            main.useUnscaledTime = true;

            SetupParticleMaterial();
            ApplyTimeFocusExemptLayer();
            auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void LateUpdate()
        {
            if (!_isShowing || _suppressed || _followTarget == null || auraParticles == null) return;
            auraParticles.transform.position = _followTarget.position;
        }

        /// <summary>Activate the aura for the given element, following the wrist transform.</summary>
        public void Show(ElementType elementType, Transform followTarget)
        {
            if (auraParticles == null) return;

            _followTarget = followTarget;
            _isShowing = true;

            var changed = _currentElement != elementType;
            _currentElement = elementType;

            if (followTarget != null)
                auraParticles.transform.position = followTarget.position;

            ApplyElementColor(elementType);

            if (changed || !auraParticles.isPlaying)
            {
                auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                auraParticles.Play(true);
            }
        }

        /// <summary>
        /// Temporarily hide/show without clearing internal state.
        /// While suppressed the aura is invisible; un-suppress restores it if Show() was called.
        /// </summary>
        public void SetSuppressed(bool suppressed)
        {
            if (_suppressed == suppressed) return;
            _suppressed = suppressed;
            if (auraParticles == null) return;

            if (suppressed)
            {
                auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            else if (_isShowing)
            {
                auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                auraParticles.Play(true);
            }
        }

        /// <summary>
        /// Scale up the aura when a matching voice command is recognised.
        /// Pass false to restore normal size.
        /// </summary>
        public void SetVoiceBoosted(bool boosted)
        {
            if (_voiceBoosted == boosted) return;
            _voiceBoosted = boosted;
            ApplyBoostSize();
        }

        /// <summary>Stop and clear the aura particles.</summary>
        public void Hide()
        {
            _isShowing = false;
            _followTarget = null;
            _currentElement = ElementType.None;
            if (_voiceBoosted)
            {
                _voiceBoosted = false;
                ApplyBoostSize();
            }

            if (auraParticles == null) return;
            auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void SetupParticleMaterial()
        {
            _particleRenderer = auraParticles.GetComponent<ParticleSystemRenderer>();
            if (_particleRenderer == null) return;

            // Create an owned material instance so we never mutate the shared asset
            _materialInstance = new Material(_particleRenderer.sharedMaterial != null
                ? _particleRenderer.sharedMaterial
                : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")));

            // Additive blending → overlapping particles brighten, creating a glow
            _materialInstance.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _materialInstance.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            _materialInstance.SetFloat("_ZWrite", 0f);
            _materialInstance.renderQueue = 3000;

            // Assign a procedural soft-circle texture so particles render round, not square
            _materialInstance.SetTexture("_BaseMap", CreateSoftCircleTexture(64));

            _particleRenderer.sharedMaterial = _materialInstance;
        }

        private void ApplyBoostSize()
        {
            if (auraParticles == null) return;
            var main = auraParticles.main;
            main.startSizeMultiplier = _voiceBoosted
                ? _baseStartSizeMultiplier * voiceBoostSizeMultiplier
                : _baseStartSizeMultiplier;
        }

        private void ApplyElementColor(ElementType type)
        {
            var color = type switch
            {
                ElementType.Fire    => fireColor,
                ElementType.Ice     => iceColor,
                ElementType.Thunder => thunderColor,
                _                   => Color.white
            };

            var main = auraParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);

            if (_materialInstance == null)
                SetupParticleMaterial();

            ApplyMaterialColor(_materialInstance, color);

            if (_particleRenderer != null && _materialInstance != null)
                _particleRenderer.sharedMaterial = _materialInstance;
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            if (material == null) return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_TintColor"))
                material.SetColor("_TintColor", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.5f);
            }
        }

        private void ApplyTimeFocusExemptLayer()
        {
            var layer = LayerMask.NameToLayer(timeFocusExemptLayerName);
            if (layer < 0) return;
            foreach (var t in auraParticles.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        /// <summary>
        /// Generates a white soft-circle (gaussian vignette) texture at runtime.
        /// Alpha=1 at center fading to 0 at edges — gives glowing orb look.
        /// </summary>
        private static Texture2D CreateSoftCircleTexture(int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var half = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - half) / half;
                    var dy = (y - half) / half;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy); // 0..~1.41
                    // smoothstep fade: full white in centre, transparent at edge
                    var t = Mathf.Clamp01(1f - dist);
                    var alpha = t * t * (3f - 2f * t); // smoothstep
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
