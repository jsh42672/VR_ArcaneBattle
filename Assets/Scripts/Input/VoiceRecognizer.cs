using System;
using System.Collections;
using ArcaneVR.Spell;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Listens for English voice commands via STT and fires gameplay events for elements and mode toggles.
    /// </summary>
    public class VoiceRecognizer : MonoBehaviour
    {
        [SerializeField] private bool autoStartListening = true;
        [SerializeField] private bool restartAfterResult = true;
        [SerializeField] private float restartDelay = 0.25f;
        [SerializeField] private float microphonePermissionTimeout = 8f;
        [SerializeField] private bool allowExternalSttProvider = true;

        public event Action<ElementType> OnVoiceCommand;
        public event Action OnModeToggleCommand;
        public event Action<string> OnVoiceStatusChanged;

        public bool IsListening { get; private set; }
        public bool IsAvailable { get; private set; }
        public string StatusText { get; private set; } = "Voice: initializing";
        public string ShortStatusText { get; private set; } = "Voice Init";
        public string DiagnosticText { get; private set; } = "Voice diag: initializing";
        public bool HasExternalSttProvider { get; private set; }
        public bool IsExternalSttListening { get; private set; }
        public string ExternalProviderName { get; private set; } = string.Empty;
        public string ActiveProviderName =>
            HasExternalSttProvider ? ExternalProviderName :
            IsAvailable ? "Android SpeechRecognizer" :
            "None";
        public ElementType LastRecognizedElement { get; private set; } = ElementType.None;
        public string LastRecognizedPhrase { get; private set; } = string.Empty;
        public float LastRecognizedTime { get; private set; } = -999f;

        private Coroutine restartCoroutine;
        private Coroutine permissionCoroutine;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject speechRecognizer;
        private AndroidJavaObject recognizerIntent;
        private RecognitionListenerProxy recognitionListener;
#endif

        private void Awake()
        {
#if !UNITY_ANDROID || UNITY_EDITOR
            _ = microphonePermissionTimeout;
#endif
            RefreshAvailability();
        }

        private void OnEnable()
        {
            if (autoStartListening)
                StartListening();
        }

        private void OnDisable()
        {
            StopListening();
        }

        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (speechRecognizer != null)
            {
                speechRecognizer.Call("destroy");
                speechRecognizer.Dispose();
                speechRecognizer = null;
            }

            recognizerIntent?.Dispose();
            recognizerIntent = null;
#endif
        }

        public void StartListening()
        {
            RefreshAvailability();

            if (HasExternalSttProvider)
            {
                SetStatus(
                    IsExternalSttListening
                        ? $"Voice: external STT listening via {ExternalProviderName}"
                        : $"Voice: external STT ready via {ExternalProviderName}",
                    IsExternalSttListening ? "Ext Listen" : "Ext Ready",
                    "External STT controls microphone capture; transcripts call SubmitExternalVoiceCommand");
                return;
            }

            if (!IsAvailable)
            {
                SetStatus(StatusText, "NoSTT", DiagnosticText);
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                SetStatus("Voice: microphone permission requested", "Mic?", "Waiting for RECORD_AUDIO permission");
                SchedulePermissionRetry();
                return;
            }

            EnsureAndroidRecognizer();
            if (speechRecognizer == null || recognizerIntent == null)
            {
                SetStatus("Voice: SpeechRecognizer setup failed", "NoSTT", "createSpeechRecognizer or intent returned null");
                IsAvailable = false;
                return;
            }

            speechRecognizer.Call("startListening", recognizerIntent);
            IsListening = true;
            SetStatus("Voice: listening - say FIRE / ICE / THUNDER / FOCUS", "Listening", "Android STT listening");
#else
            SetStatus("Voice: NoSTT - available only on Android/Quest builds", "NoSTT", "Editor/PC build: Android STT disabled");
#endif
        }

        public void StopListening()
        {
            if (restartCoroutine != null)
            {
                StopCoroutine(restartCoroutine);
                restartCoroutine = null;
            }

            if (permissionCoroutine != null)
            {
                StopCoroutine(permissionCoroutine);
                permissionCoroutine = null;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (speechRecognizer != null && IsListening)
                speechRecognizer.Call("stopListening");
#endif

            IsListening = false;
            if (IsAvailable)
                SetStatus("Voice: stopped", "Stopped");
        }

        public void SubmitVoiceCommand(string phrase)
        {
            SubmitVoiceCommand(phrase, "Voice command parsed");
        }

        public void SubmitExternalVoiceCommand(string phrase)
        {
            SubmitExternalVoiceCommand(phrase, ExternalProviderName);
        }

        public void SubmitExternalVoiceCommand(string phrase, string providerName)
        {
            if (!string.IsNullOrWhiteSpace(providerName))
                RegisterExternalSttProvider(providerName, true);

            SubmitVoiceCommand(
                phrase,
                string.IsNullOrWhiteSpace(providerName)
                    ? "External STT transcript parsed"
                    : $"External STT transcript parsed from {providerName}");
        }

        public void RegisterExternalSttProvider(string providerName)
        {
            RegisterExternalSttProvider(providerName, false);
        }

        public void RegisterExternalSttProvider(string providerName, bool isListening)
        {
            if (!allowExternalSttProvider)
                return;

            HasExternalSttProvider = true;
            ExternalProviderName = string.IsNullOrWhiteSpace(providerName) ? "External STT" : providerName.Trim();
            IsExternalSttListening = isListening;
            IsAvailable = true;
            SetStatus(
                isListening
                    ? $"Voice: external STT listening via {ExternalProviderName}"
                    : $"Voice: external STT ready via {ExternalProviderName}",
                isListening ? "Ext Listen" : "Ext Ready",
                "External STT bridge active");
        }

        public void SetExternalSttListening(bool isListening)
        {
            if (!HasExternalSttProvider)
                RegisterExternalSttProvider("External STT", isListening);

            IsExternalSttListening = isListening;
            SetStatus(
                isListening
                    ? $"Voice: external STT listening via {ExternalProviderName}"
                    : $"Voice: external STT stopped via {ExternalProviderName}",
                isListening ? "Ext Listen" : "Ext Stop",
                "External STT bridge active");
        }

        public void UnregisterExternalSttProvider()
        {
            HasExternalSttProvider = false;
            IsExternalSttListening = false;
            ExternalProviderName = string.Empty;
            RefreshAvailability();
        }

        private void SubmitVoiceCommand(string phrase, string successDiagnosticText)
        {
            LastRecognizedPhrase = phrase ?? string.Empty;
            LastRecognizedTime = Time.time;

            if (IsModeTogglePhrase(LastRecognizedPhrase))
            {
                LastRecognizedElement = ElementType.None;
                SetStatus($"Voice: Arcane Focus from '{LastRecognizedPhrase}'", "Focus", successDiagnosticText);
                OnModeToggleCommand?.Invoke();
                return;
            }

            LastRecognizedElement = ParseElement(LastRecognizedPhrase);

            if (LastRecognizedElement == ElementType.None)
            {
                SetStatus($"Voice: ignored '{LastRecognizedPhrase}'", "Ignored", "Phrase did not contain FIRE / ICE / THUNDER / FOCUS");
                return;
            }

            SetStatus($"Voice: {LastRecognizedElement} from '{LastRecognizedPhrase}'", LastRecognizedElement.ToString(), successDiagnosticText);
            OnVoiceCommand?.Invoke(LastRecognizedElement);
        }

        public ElementType ParseElement(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
                return ElementType.None;

            var normalized = phrase.Trim().ToUpperInvariant();
            var compact = normalized
                .Replace("'", string.Empty)
                .Replace(" ", string.Empty)
                .Replace(".", string.Empty)
                .Replace(",", string.Empty)
                .Replace("!", string.Empty)
                .Replace("?", string.Empty);

            if (normalized.Contains("THUNDER") ||
                normalized.Contains("LIGHTNING") ||
                normalized.Contains("FULGUR") ||
                normalized.Contains("FULGOR") ||
                normalized.Contains("FULGER") ||
                normalized.Contains("FULGURE"))
            {
                return ElementType.Thunder;
            }

            if (normalized.Contains("FIRE") ||
                normalized.Contains("IGNIS") ||
                normalized.Contains("IGNEOUS") ||
                normalized.Contains("IGNITE"))
            {
                return ElementType.Fire;
            }

            if (normalized.Contains("FREEZE") ||
                normalized.Contains("FROST") ||
                normalized.Contains("GLACIES") ||
                normalized.Contains("GLACIER") ||
                normalized.Contains("GLACIAL") ||
                normalized.Contains("ICE") ||
                compact == "ITS" ||
                compact == "EYES" ||
                compact == "HI")
            {
                return ElementType.Ice;
            }

            return ElementType.None;
        }

        public bool IsModeTogglePhrase(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
                return false;

            var compact = CompactSpeechPhrase(phrase);
            if (string.IsNullOrWhiteSpace(compact))
                return false;

            if (compact.Contains("FOCUS") ||
                compact.Contains("FOCUSED") ||
                compact.Contains("FOCUSS") ||
                compact.Contains("FOKUS") ||
                compact.Contains("ARCANE") ||
                compact.Contains("ARKANE") ||
                compact.Contains("ARCAIN") ||
                compact.Contains("ARCAN") ||
                compact.Contains("MAGICFOCUS") ||
                compact.Contains("CASTMODE") ||
                compact.Contains("SIEGEMODE") ||
                compact.Contains("COMBOMODE") ||
                compact.Contains("COMBINE") ||
                compact.Contains("COMBINATION"))
            {
                return true;
            }

            var tokens = compact.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (IsSimilarSpeechToken(token, "FOCUS", 2) ||
                    IsSimilarSpeechToken(token, "ARCANE", 2) ||
                    IsSimilarSpeechToken(token, "COMBO", 1) ||
                    IsSimilarSpeechToken(token, "CAST", 1) ||
                    IsSimilarSpeechToken(token, "SIEGE", 1))
                {
                    return true;
                }
            }

            return false;
        }

        private static string CompactSpeechPhrase(string phrase)
        {
            var normalized = phrase.Trim().ToUpperInvariant();
            var chars = new char[normalized.Length];
            var writeIndex = 0;

            foreach (var character in normalized)
            {
                if (char.IsLetterOrDigit(character))
                {
                    chars[writeIndex++] = character;
                    continue;
                }

                if (char.IsWhiteSpace(character) && (writeIndex == 0 || chars[writeIndex - 1] != ' '))
                    chars[writeIndex++] = ' ';
            }

            return new string(chars, 0, writeIndex).Trim();
        }

        private static bool IsSimilarSpeechToken(string token, string expected, int maxDistance)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            if (token.Contains(expected) || expected.Contains(token))
                return token.Length >= 3;

            return ComputeEditDistance(token, expected) <= maxDistance;
        }

        private static int ComputeEditDistance(string left, string right)
        {
            if (string.IsNullOrEmpty(left))
                return right?.Length ?? 0;
            if (string.IsNullOrEmpty(right))
                return left.Length;

            var previous = new int[right.Length + 1];
            var current = new int[right.Length + 1];
            for (var j = 0; j <= right.Length; j++)
                previous[j] = j;

            for (var i = 1; i <= left.Length; i++)
            {
                current[0] = i;
                for (var j = 1; j <= right.Length; j++)
                {
                    var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                    current[j] = Mathf.Min(
                        Mathf.Min(current[j - 1] + 1, previous[j] + 1),
                        previous[j - 1] + cost);
                }

                var temp = previous;
                previous = current;
                current = temp;
            }

            return previous[right.Length];
        }

        [ContextMenu("Voice Mock/FIRE")]
        private void MockFireCommand()
        {
            SubmitVoiceCommand("FIRE");
        }

        [ContextMenu("Voice Mock/ICE")]
        private void MockIceCommand()
        {
            SubmitVoiceCommand("ICE");
        }

        [ContextMenu("Voice Mock/THUNDER")]
        private void MockThunderCommand()
        {
            SubmitVoiceCommand("THUNDER");
        }

        [ContextMenu("Voice Mock/ARCANE FOCUS")]
        private void MockArcaneFocusCommand()
        {
            SubmitVoiceCommand("ARCANE FOCUS");
        }

        private void RefreshAvailability()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var speechRecognizerClass = new AndroidJavaClass("android.speech.SpeechRecognizer");
                var serviceCount = CountRecognitionServices(activity);
                IsAvailable = activity != null && speechRecognizerClass.CallStatic<bool>("isRecognitionAvailable", activity);
                if (!IsAvailable && HasExternalSttProvider)
                {
                    IsAvailable = true;
                    SetStatus(
                        $"Voice: external STT ready via {ExternalProviderName}",
                        IsExternalSttListening ? "Ext Listen" : "Ext Ready",
                        $"External STT active; Android RecognitionService services {FormatServiceCount(serviceCount)}");
                    return;
                }

                SetStatus(
                    IsAvailable ? "Voice: Android SpeechRecognizer available" : "Voice: NoSTT - recognizer unavailable",
                    IsAvailable ? "Ready" : "NoSTT",
                    IsAvailable
                        ? $"Android SpeechRecognizer ready | services {FormatServiceCount(serviceCount)}"
                        : $"No Android RecognitionService | services {FormatServiceCount(serviceCount)}");
            }
            catch (Exception exception)
            {
                if (HasExternalSttProvider)
                {
                    IsAvailable = true;
                    SetStatus(
                        $"Voice: external STT ready via {ExternalProviderName}",
                        IsExternalSttListening ? "Ext Listen" : "Ext Ready",
                        $"External STT active; Android STT check failed: {exception.GetType().Name}");
                    return;
                }

                IsAvailable = false;
                SetStatus($"Voice: NoSTT - {exception.GetType().Name}", "NoSTT", exception.Message);
            }
#else
            if (HasExternalSttProvider)
            {
                IsAvailable = true;
                SetStatus(
                    $"Voice: external STT ready via {ExternalProviderName}",
                    IsExternalSttListening ? "Ext Listen" : "Ext Ready",
                    "External STT active in Editor/PC build");
                return;
            }

            IsAvailable = false;
            SetStatus("Voice: NoSTT - available only on Android/Quest builds", "NoSTT", "Editor/PC build: Android STT disabled");
#endif
        }

        private void SetStatus(string statusText, string shortStatusText)
        {
            SetStatus(statusText, shortStatusText, statusText);
        }

        private void SetStatus(string statusText, string shortStatusText, string diagnosticText)
        {
            StatusText = statusText;
            ShortStatusText = shortStatusText;
            DiagnosticText = diagnosticText;
            OnVoiceStatusChanged?.Invoke(StatusText);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static int CountRecognitionServices(AndroidJavaObject activity)
        {
            if (activity == null)
                return -1;

            using var packageManager = activity.Call<AndroidJavaObject>("getPackageManager");
            using var serviceIntent = new AndroidJavaObject("android.content.Intent", "android.speech.RecognitionService");
            using var services = packageManager.Call<AndroidJavaObject>("queryIntentServices", serviceIntent, 0);
            return services != null ? services.Call<int>("size") : 0;
        }

        private static string FormatServiceCount(int serviceCount)
        {
            return serviceCount >= 0 ? serviceCount.ToString() : "activity?";
        }

        private void EnsureAndroidRecognizer()
        {
            if (speechRecognizer != null && recognizerIntent != null)
                return;

            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var speechRecognizerClass = new AndroidJavaClass("android.speech.SpeechRecognizer");
            speechRecognizer = speechRecognizerClass.CallStatic<AndroidJavaObject>("createSpeechRecognizer", activity);
            recognitionListener = new RecognitionListenerProxy(this);
            speechRecognizer.Call("setRecognitionListener", recognitionListener);

            using var recognizerIntentClass = new AndroidJavaClass("android.speech.RecognizerIntent");
            var actionRecognizeSpeech = recognizerIntentClass.GetStatic<string>("ACTION_RECOGNIZE_SPEECH");
            recognizerIntent = new AndroidJavaObject("android.content.Intent", actionRecognizeSpeech);
            recognizerIntent.Call<AndroidJavaObject>(
                "putExtra",
                recognizerIntentClass.GetStatic<string>("EXTRA_LANGUAGE_MODEL"),
                recognizerIntentClass.GetStatic<string>("LANGUAGE_MODEL_FREE_FORM"));
            recognizerIntent.Call<AndroidJavaObject>("putExtra", recognizerIntentClass.GetStatic<string>("EXTRA_MAX_RESULTS"), 3);
            recognizerIntent.Call<AndroidJavaObject>("putExtra", recognizerIntentClass.GetStatic<string>("EXTRA_PARTIAL_RESULTS"), false);
            recognizerIntent.Call<AndroidJavaObject>("putExtra", recognizerIntentClass.GetStatic<string>("EXTRA_LANGUAGE"), "en-US");
        }

        private void SchedulePermissionRetry()
        {
            if (permissionCoroutine != null)
                StopCoroutine(permissionCoroutine);

            permissionCoroutine = StartCoroutine(WaitForMicrophonePermission());
        }

        private IEnumerator WaitForMicrophonePermission()
        {
            var endTime = Time.realtimeSinceStartup + Mathf.Max(1f, microphonePermissionTimeout);
            while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone) &&
                   Time.realtimeSinceStartup < endTime)
            {
                yield return new WaitForSecondsRealtime(0.25f);
            }

            permissionCoroutine = null;
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                StartListening();
                yield break;
            }

            SetStatus("Voice: microphone permission denied or timed out", "Mic denied", "RECORD_AUDIO permission was not granted");
        }

        private void HandleAndroidReady()
        {
            IsListening = true;
            SetStatus("Voice: listening - say FIRE / ICE / THUNDER / FOCUS", "Listening");
        }

        private void HandleAndroidResults(AndroidJavaObject results)
        {
            IsListening = false;
            var phrase = ExtractBestAndroidPhrase(results);
            SubmitVoiceCommand(phrase);
            ScheduleRestartIfNeeded();
        }

        private void HandleAndroidError(int errorCode)
        {
            IsListening = false;
            SetStatus($"Voice: recognizer error {errorCode}", $"Err {errorCode}", DescribeRecognizerError(errorCode));
            ScheduleRestartIfNeeded();
        }

        private static string DescribeRecognizerError(int errorCode)
        {
            return errorCode switch
            {
                1 => "Network operation timed out",
                2 => "Network error",
                3 => "Audio recording error",
                4 => "Server error",
                5 => "Client error",
                6 => "No speech input",
                7 => "No recognition match",
                8 => "Recognizer busy",
                9 => "Insufficient permissions",
                10 => "Too many requests",
                11 => "Language not supported",
                12 => "Language unavailable",
                _ => "Unknown recognizer error"
            };
        }

        private string ExtractBestAndroidPhrase(AndroidJavaObject results)
        {
            if (results == null)
                return string.Empty;

            using var speechRecognizerClass = new AndroidJavaClass("android.speech.SpeechRecognizer");
            var resultKey = speechRecognizerClass.GetStatic<string>("RESULTS_RECOGNITION");
            using var matches = results.Call<AndroidJavaObject>("getStringArrayList", resultKey);
            if (matches == null)
                return string.Empty;

            var count = matches.Call<int>("size");
            return count > 0 ? matches.Call<string>("get", 0) : string.Empty;
        }

        private sealed class RecognitionListenerProxy : AndroidJavaProxy
        {
            private readonly VoiceRecognizer owner;

            public RecognitionListenerProxy(VoiceRecognizer owner)
                : base("android.speech.RecognitionListener")
            {
                this.owner = owner;
            }

            public void onReadyForSpeech(AndroidJavaObject bundle)
            {
                owner.HandleAndroidReady();
            }

            public void onBeginningOfSpeech()
            {
            }

            public void onRmsChanged(float rmsdB)
            {
            }

            public void onBufferReceived(byte[] buffer)
            {
            }

            public void onEndOfSpeech()
            {
            }

            public void onError(int error)
            {
                owner.HandleAndroidError(error);
            }

            public void onResults(AndroidJavaObject results)
            {
                owner.HandleAndroidResults(results);
            }

            public void onPartialResults(AndroidJavaObject partialResults)
            {
            }

            public void onEvent(int eventType, AndroidJavaObject bundle)
            {
            }
        }
#endif

        private void ScheduleRestartIfNeeded()
        {
            if (!restartAfterResult || !isActiveAndEnabled)
                return;

            if (restartCoroutine != null)
                StopCoroutine(restartCoroutine);

            restartCoroutine = StartCoroutine(RestartListeningAfterDelay());
        }

        private IEnumerator RestartListeningAfterDelay()
        {
            yield return new WaitForSeconds(restartDelay);
            restartCoroutine = null;
            StartListening();
        }
    }
}
