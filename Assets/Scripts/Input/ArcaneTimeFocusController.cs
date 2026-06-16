using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Shared slow-time focus effect for grimoire reading and later combination casting.
    /// </summary>
    public class ArcaneTimeFocusController : MonoBehaviour
    {
        public const float defaultSlowTimeScale = 0.05f;
        public const float defaultGrayscaleSaturation = -100f;

        [Header("── 시간 배속 ──")]
        [SerializeField, Range(0.01f, 1f)] private float slowTimeScale = defaultSlowTimeScale;

        [Header("── 화면 연출 ──")]
        [SerializeField] private bool enableGrayscale = true;
        [SerializeField, Range(-100f, 0f)] private float grayscaleSaturation = defaultGrayscaleSaturation;
        [SerializeField] private float grayscaleVolumePriority = 250f;

        [Header("── 포스트프로세스 제외 렌더링 ──")]
        [SerializeField] private bool enableExemptOverlayCamera = true;
        [SerializeField] private string exemptLayerName = "TimeFocusExempt";
        [SerializeField] private Camera baseCameraOverride;

        [Header("── 사운드 ──")]
        [SerializeField] private bool enableTickSound = true;
        [SerializeField] private AudioClip tickLoopClip;
        [SerializeField, Range(0f, 1f)] private float tickVolume = 0.85f;
        [SerializeField] private float tickIntervalSeconds = 0.5f;

        private readonly HashSet<string> activeReasons = new HashSet<string>();
        private readonly Dictionary<UniversalAdditionalCameraData, bool> cameraPostProcessingStates = new Dictionary<UniversalAdditionalCameraData, bool>();
        private readonly Dictionary<Camera, int> cameraCullingMaskStates = new Dictionary<Camera, int>();
        private float previousTimeScale = 1f;
        private float previousFixedDeltaTime = 0.02f;
        private float nextTickUnscaledTime = -999f;
        private Volume grayscaleVolume;
        private ColorAdjustments colorAdjustments;
        private AudioSource tickAudioSource;
        private Camera exemptOverlayCamera;
        private UniversalAdditionalCameraData exemptOverlayCameraData;

        public bool IsActive => activeReasons.Count > 0;
        public float SlowTimeScale => slowTimeScale;

        private void Update()
        {
            if (!IsActive || !enableTickSound)
                return;

            EnsureTickAudioSource();
            if (tickAudioSource == null || tickAudioSource.clip == null || Time.unscaledTime < nextTickUnscaledTime)
                return;

            tickAudioSource.PlayOneShot(tickAudioSource.clip, tickVolume);
            nextTickUnscaledTime = Time.unscaledTime + Mathf.Max(0.1f, tickIntervalSeconds);
        }

        private void OnDisable()
        {
            ClearAllFocus();
        }

        public void RequestFocus(string reason)
        {
            var key = NormalizeReason(reason);
            if (!activeReasons.Add(key))
                return;

            if (activeReasons.Count == 1)
                ApplyFocus();
        }

        public void ReleaseFocus(string reason)
        {
            var key = NormalizeReason(reason);
            if (!activeReasons.Remove(key))
                return;

            if (activeReasons.Count == 0)
                RestoreFocus();
        }

        public void ClearAllFocus()
        {
            if (activeReasons.Count == 0)
                return;

            activeReasons.Clear();
            RestoreFocus();
        }

        private void ApplyFocus()
        {
            previousTimeScale = Time.timeScale;
            previousFixedDeltaTime = Time.fixedDeltaTime;

            Time.timeScale = Mathf.Clamp(slowTimeScale, 0.01f, 1f);
            Time.fixedDeltaTime = previousFixedDeltaTime * Time.timeScale;

            SetCameraPostProcessingActive(true);
            SetExemptOverlayActive(true);
            SetGrayscaleActive(true);
            SetTickActive(true);
        }

        private void RestoreFocus()
        {
            Time.timeScale = previousTimeScale;
            Time.fixedDeltaTime = previousFixedDeltaTime;

            SetGrayscaleActive(false);
            SetExemptOverlayActive(false);
            SetCameraPostProcessingActive(false);
            SetTickActive(false);
        }

        private void SetGrayscaleActive(bool active)
        {
            if (!enableGrayscale)
                active = false;

            EnsureGrayscaleVolume();
            if (grayscaleVolume != null)
                grayscaleVolume.enabled = active;

            if (colorAdjustments != null)
                colorAdjustments.saturation.value = grayscaleSaturation;
        }

        private void EnsureGrayscaleVolume()
        {
            if (grayscaleVolume != null)
                return;

            var volumeObject = new GameObject("ArcaneTimeFocus_GrayscaleVolume")
            {
                hideFlags = HideFlags.DontSave
            };
            volumeObject.transform.SetParent(transform, false);

            grayscaleVolume = volumeObject.AddComponent<Volume>();
            grayscaleVolume.isGlobal = true;
            grayscaleVolume.priority = grayscaleVolumePriority;
            grayscaleVolume.weight = 1f;
            grayscaleVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            grayscaleVolume.profile.hideFlags = HideFlags.DontSave;
            colorAdjustments = grayscaleVolume.profile.Add<ColorAdjustments>(true);
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.saturation.value = grayscaleSaturation;
            grayscaleVolume.enabled = false;
        }

        private void SetCameraPostProcessingActive(bool active)
        {
            if (active)
            {
                cameraPostProcessingStates.Clear();
                foreach (var cameraData in FindObjectsByType<UniversalAdditionalCameraData>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (cameraData == null)
                        continue;

                    cameraPostProcessingStates[cameraData] = cameraData.renderPostProcessing;
                    cameraData.renderPostProcessing = true;
                }

                return;
            }

            foreach (var entry in cameraPostProcessingStates)
            {
                if (entry.Key != null)
                    entry.Key.renderPostProcessing = entry.Value;
            }

            cameraPostProcessingStates.Clear();
        }

        private void SetExemptOverlayActive(bool active)
        {
            if (!enableExemptOverlayCamera)
                active = false;

            var layer = LayerMask.NameToLayer(exemptLayerName);
            if (layer < 0)
            {
                if (active)
                    Debug.LogWarning($"[ArcaneTimeFocus] Exempt layer '{exemptLayerName}' does not exist.", this);
                return;
            }

            var baseCamera = ResolveBaseCamera();
            if (baseCamera == null)
            {
                if (active)
                    Debug.LogWarning("[ArcaneTimeFocus] Could not find a base camera for exempt overlay rendering.", this);
                return;
            }

            var baseCameraData = baseCamera.GetComponent<UniversalAdditionalCameraData>();
            if (baseCameraData == null)
                baseCameraData = baseCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

            EnsureExemptOverlayCamera(baseCamera, layer);

            if (active)
            {
                if (!cameraCullingMaskStates.ContainsKey(baseCamera))
                    cameraCullingMaskStates.Add(baseCamera, baseCamera.cullingMask);

                var layerMask = 1 << layer;
                baseCamera.cullingMask &= ~layerMask;

                if (!baseCameraData.cameraStack.Contains(exemptOverlayCamera))
                    baseCameraData.cameraStack.Add(exemptOverlayCamera);

                exemptOverlayCamera.enabled = true;
                return;
            }

            if (baseCameraData.cameraStack.Contains(exemptOverlayCamera))
                baseCameraData.cameraStack.Remove(exemptOverlayCamera);

            if (cameraCullingMaskStates.TryGetValue(baseCamera, out var previousMask))
                baseCamera.cullingMask = previousMask;

            cameraCullingMaskStates.Clear();

            if (exemptOverlayCamera != null)
                exemptOverlayCamera.enabled = false;
        }

        private Camera ResolveBaseCamera()
        {
            if (baseCameraOverride != null)
                return baseCameraOverride;

            if (Camera.main != null)
                return Camera.main;

            foreach (var candidate in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (candidate != null && candidate.enabled)
                    return candidate;
            }

            return null;
        }

        private void EnsureExemptOverlayCamera(Camera baseCamera, int layer)
        {
            if (exemptOverlayCamera == null)
            {
                var overlayObject = new GameObject("ArcaneTimeFocus_ExemptOverlayCamera")
                {
                    hideFlags = HideFlags.DontSave
                };
                overlayObject.transform.SetParent(baseCamera.transform, false);
                exemptOverlayCamera = overlayObject.AddComponent<Camera>();
                exemptOverlayCameraData = overlayObject.AddComponent<UniversalAdditionalCameraData>();
            }

            exemptOverlayCamera.CopyFrom(baseCamera);
            exemptOverlayCamera.transform.SetParent(baseCamera.transform, false);
            exemptOverlayCamera.transform.localPosition = Vector3.zero;
            exemptOverlayCamera.transform.localRotation = Quaternion.identity;
            exemptOverlayCamera.transform.localScale = Vector3.one;
            exemptOverlayCamera.cullingMask = 1 << layer;
            exemptOverlayCamera.clearFlags = CameraClearFlags.Depth;
            exemptOverlayCamera.depth = baseCamera.depth + 1f;
            exemptOverlayCamera.enabled = false;

            if (exemptOverlayCameraData == null)
                exemptOverlayCameraData = exemptOverlayCamera.GetComponent<UniversalAdditionalCameraData>();
            if (exemptOverlayCameraData == null)
                exemptOverlayCameraData = exemptOverlayCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

            exemptOverlayCameraData.renderType = CameraRenderType.Overlay;
            exemptOverlayCameraData.renderPostProcessing = false;
        }

        private void SetTickActive(bool active)
        {
            if (!enableTickSound)
                active = false;

            EnsureTickAudioSource();
            if (tickAudioSource == null)
                return;

            if (active)
            {
                nextTickUnscaledTime = -999f;
            }
            else
            {
                tickAudioSource.Stop();
                nextTickUnscaledTime = -999f;
            }
        }

        private void EnsureTickAudioSource()
        {
            if (tickAudioSource != null)
                return;

            tickAudioSource = GetComponent<AudioSource>();
            if (tickAudioSource == null)
                tickAudioSource = gameObject.AddComponent<AudioSource>();

            tickAudioSource.playOnAwake = false;
            tickAudioSource.loop = false;
            tickAudioSource.spatialBlend = 0f;
            tickAudioSource.dopplerLevel = 0f;
            tickAudioSource.ignoreListenerPause = true;
            tickAudioSource.volume = tickVolume;
            tickAudioSource.clip = tickLoopClip != null ? tickLoopClip : CreateTickLoopClip();
        }

        private static AudioClip CreateTickLoopClip()
        {
            const int sampleRate = 44100;
            const float lengthSeconds = 0.12f;
            var sampleCount = Mathf.CeilToInt(sampleRate * lengthSeconds);
            var samples = new float[sampleCount];

            AddTick(samples, sampleRate, 0.005f, 0.95f);

            var clip = AudioClip.Create("ArcaneTimeFocus_TickLoop", sampleCount, 1, sampleRate, false);
            clip.hideFlags = HideFlags.DontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        private static void AddTick(float[] samples, int sampleRate, float startTime, float gain)
        {
            var start = Mathf.Clamp(Mathf.RoundToInt(startTime * sampleRate), 0, samples.Length - 1);
            var length = Mathf.Min(Mathf.RoundToInt(0.055f * sampleRate), samples.Length - start);
            for (var i = 0; i < length; i++)
            {
                var n = (float)i / Mathf.Max(1, length - 1);
                var envelope = Mathf.Exp(-n * 9f);
                var tone = Mathf.Sin((start + i) * 920f * Mathf.PI * 2f / sampleRate);
                samples[start + i] += tone * envelope * gain;
            }
        }

        private static string NormalizeReason(string reason)
        {
            return string.IsNullOrWhiteSpace(reason) ? "Focus" : reason.Trim();
        }
    }
}
