using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Adapter for STT providers such as Meta Voice SDK/Wit. Connect the provider's full-transcription
    /// UnityEvent to SubmitTranscript so the existing FIRE/ICE/THUNDER parser stays in one place.
    /// </summary>
    [DefaultExecutionOrder(82)]
    public class ExternalVoiceCommandBridge : MonoBehaviour
    {
        [SerializeField] private VoiceRecognizer voiceRecognizer;
        [SerializeField] private string providerName = "External STT";
        [SerializeField] private bool registerProviderOnEnable;
        [SerializeField] private bool createRecognizerIfMissing = true;
        [SerializeField] private bool showDebugLog;

        public string ProviderName => string.IsNullOrWhiteSpace(providerName) ? "External STT" : providerName;
        public string LastTranscript { get; private set; } = string.Empty;
        public string LastPartialTranscript { get; private set; } = string.Empty;
        public float LastTranscriptTime { get; private set; } = -999f;
        public bool IsProviderRegistered { get; private set; }
        public bool IsProviderListening { get; private set; }

        private void Awake()
        {
            ResolveVoiceRecognizer();
        }

        private void OnEnable()
        {
            ResolveVoiceRecognizer();

            if (registerProviderOnEnable)
                RegisterProvider();
        }

        private void OnDisable()
        {
            IsProviderListening = false;
        }

        [ContextMenu("External Voice/Register Provider")]
        public void RegisterProvider()
        {
            ResolveVoiceRecognizer();
            if (voiceRecognizer == null)
                return;

            voiceRecognizer.RegisterExternalSttProvider(ProviderName, IsProviderListening);
            IsProviderRegistered = true;
        }

        public void RegisterProvider(string newProviderName)
        {
            SetProviderName(newProviderName);
            RegisterProvider();
        }

        public void SetProviderName(string newProviderName)
        {
            if (!string.IsNullOrWhiteSpace(newProviderName))
                providerName = newProviderName.Trim();
        }

        [ContextMenu("External Voice/Unregister Provider")]
        public void UnregisterProvider()
        {
            if (voiceRecognizer != null)
                voiceRecognizer.UnregisterExternalSttProvider();

            IsProviderRegistered = false;
            IsProviderListening = false;
        }

        public void MarkListeningStarted()
        {
            ResolveVoiceRecognizer();
            IsProviderListening = true;
            IsProviderRegistered = true;
            voiceRecognizer?.RegisterExternalSttProvider(ProviderName, true);
        }

        public void MarkListeningStopped()
        {
            IsProviderListening = false;
            voiceRecognizer?.SetExternalSttListening(false);
        }

        public void SubmitPartialTranscript(string transcript)
        {
            LastPartialTranscript = transcript ?? string.Empty;
        }

        public void SubmitTranscript(string transcript)
        {
            ResolveVoiceRecognizer();
            if (voiceRecognizer == null)
                return;

            LastTranscript = transcript ?? string.Empty;
            LastTranscriptTime = Time.time;
            IsProviderRegistered = true;
            voiceRecognizer.SubmitExternalVoiceCommand(LastTranscript, ProviderName);

            if (showDebugLog)
                Debug.Log($"[ArcaneVR] External voice transcript from {ProviderName}: {LastTranscript}");
        }

        public void OnFullTranscription(string transcript)
        {
            SubmitTranscript(transcript);
        }

        [ContextMenu("External Voice/Mock FIRE")]
        public void SubmitFire()
        {
            SubmitTranscript("FIRE");
        }

        [ContextMenu("External Voice/Mock ICE")]
        public void SubmitIce()
        {
            SubmitTranscript("ICE");
        }

        [ContextMenu("External Voice/Mock THUNDER")]
        public void SubmitThunder()
        {
            SubmitTranscript("THUNDER");
        }

        private void ResolveVoiceRecognizer()
        {
            if (voiceRecognizer != null)
                return;

            voiceRecognizer = FindAnyObjectByType<VoiceRecognizer>();
            if (voiceRecognizer == null && createRecognizerIfMissing)
                voiceRecognizer = gameObject.AddComponent<VoiceRecognizer>();
        }
    }
}
