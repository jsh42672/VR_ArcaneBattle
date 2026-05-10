using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Reflection based bridge for Meta Voice SDK. It avoids compile-time dependency on the package,
    /// but automatically wires AppVoiceExperience transcription events when the package is installed.
    /// </summary>
    [DefaultExecutionOrder(84)]
    public class MetaVoiceSdkAutoBridge : MonoBehaviour
    {
        private const string ProviderName = "Meta Voice SDK";
        private const string AppVoiceExperienceTypeName = "Oculus.Voice.AppVoiceExperience";
        private const string AppDictationExperienceTypeName = "Oculus.Voice.Dictation.AppDictationExperience";
        private const string AppVoiceExperienceQualifiedTypeName = "Oculus.Voice.AppVoiceExperience, VoiceSDK.Runtime";
        private const string AppDictationExperienceQualifiedTypeName = "Oculus.Voice.Dictation.AppDictationExperience, VoiceSDK.Dictation.Runtime";
        private const string AppBuiltInsTypeName = "Oculus.Voice.AppBuiltIns";
        private const string AppBuiltInsQualifiedTypeName = "Oculus.Voice.AppBuiltIns, VoiceSDK.Runtime";
        private const string WitRuntimeConfigurationTypeName = "Meta.WitAi.Configuration.WitRuntimeConfiguration";
        private const string WitRuntimeConfigurationQualifiedTypeName = "Meta.WitAi.Configuration.WitRuntimeConfiguration, Meta.WitAi";
        private const string WitConfigurationTypeName = "Meta.WitAi.Data.Configuration.WitConfiguration";
        private const string WitConfigurationQualifiedTypeName = "Meta.WitAi.Data.Configuration.WitConfiguration, Meta.WitAi";
        private const string WitAppInfoTypeName = "Meta.WitAi.Data.Info.WitAppInfo";
        private const string WitAppInfoQualifiedTypeName = "Meta.WitAi.Data.Info.WitAppInfo, Meta.WitAI.Lib";

        [SerializeField] private ExternalVoiceCommandBridge externalVoiceBridge;
        [SerializeField] private bool autoBind = true;
        [SerializeField] private bool createVoiceExperienceIfMissing = true;
        [SerializeField] private bool useBuiltInEnglishWitConfiguration = true;
        [SerializeField] private bool autoActivateWhenReady = true;
        [SerializeField] private float bindRetryInterval = 1f;
        [SerializeField] private float activationRetryInterval = 2.5f;

        private Component voiceComponent;
        private object voiceEvents;
        private float nextBindTime;
        private float nextActivateTime;
        private bool isBound;
        private bool hasConfiguration;

        public string StatusText { get; private set; } = "Meta Voice SDK: waiting";
        public bool IsSdkTypeAvailable { get; private set; }
        public bool IsVoiceExperienceFound => voiceComponent != null;
        public bool IsBound => isBound;
        public bool HasConfiguration => hasConfiguration;

        private void Awake()
        {
            ResolveExternalBridge();
        }

        private void OnEnable()
        {
            ResolveExternalBridge();
            if (autoBind)
                TryBind();
        }

        private void OnDisable()
        {
            externalVoiceBridge?.MarkListeningStopped();
        }

        private void Update()
        {
            if (autoBind && Time.unscaledTime >= nextBindTime)
            {
                nextBindTime = Time.unscaledTime + Mathf.Max(0.25f, bindRetryInterval);
                TryBind();
            }

            if (!autoActivateWhenReady ||
                !isBound ||
                !hasConfiguration ||
                Time.unscaledTime < nextActivateTime)
            {
                return;
            }

            nextActivateTime = Time.unscaledTime + Mathf.Max(0.5f, activationRetryInterval);
            TryActivateIfIdle();
        }

        [ContextMenu("Meta Voice SDK/Try Bind")]
        public void TryBind()
        {
            ResolveExternalBridge();
            externalVoiceBridge?.SetProviderName(ProviderName);

            var appVoiceType = FindType(AppVoiceExperienceTypeName, AppVoiceExperienceQualifiedTypeName);
            var dictationType = FindType(AppDictationExperienceTypeName, AppDictationExperienceQualifiedTypeName);
            var targetType = appVoiceType ?? dictationType;
            IsSdkTypeAvailable = targetType != null;

            if (targetType == null)
            {
                StatusText = "Meta Voice SDK: package missing";
                return;
            }

            if (voiceComponent == null || !targetType.IsInstanceOfType(voiceComponent))
                voiceComponent = FindVoiceExperienceComponent(appVoiceType, dictationType);

            if (voiceComponent == null && createVoiceExperienceIfMissing && appVoiceType != null)
                voiceComponent = CreateVoiceExperienceComponent(appVoiceType);

            if (voiceComponent == null)
            {
                StatusText = "Meta Voice SDK: AppVoiceExperience missing";
                return;
            }

            if (useBuiltInEnglishWitConfiguration && !HasUsableConfiguration(voiceComponent))
                TryAssignBuiltInWitConfiguration(voiceComponent, "English");

            hasConfiguration = HasUsableConfiguration(voiceComponent);
            voiceEvents = GetMemberValue(voiceComponent, "VoiceEvents");
            if (voiceEvents == null)
            {
                StatusText = "Meta Voice SDK: VoiceEvents missing";
                return;
            }

            BindVoiceEvents();
            if (hasConfiguration)
                externalVoiceBridge?.RegisterProvider(ProviderName);

            StatusText = hasConfiguration
                ? "Meta Voice SDK: bound (English built-in)"
                : "Meta Voice SDK: Wit config missing";
        }

        [ContextMenu("Meta Voice SDK/Activate Once")]
        public void ActivateOnce()
        {
            TryActivateIfIdle(true);
        }

        private void BindVoiceEvents()
        {
            if (isBound || voiceEvents == null || externalVoiceBridge == null)
                return;

            AddStringListener("OnFullTranscription", externalVoiceBridge.SubmitTranscript);
            AddStringListener("OnPartialTranscription", externalVoiceBridge.SubmitPartialTranscript);
            AddVoidListener("OnStartListening", externalVoiceBridge.MarkListeningStarted);
            AddVoidListener("OnStoppedListening", externalVoiceBridge.MarkListeningStopped);
            AddVoidListener("OnComplete", externalVoiceBridge.MarkListeningStopped);
            isBound = true;
        }

        private void TryActivateIfIdle(bool force = false)
        {
            if (voiceComponent == null)
                return;

            var active = ReadBoolProperty(voiceComponent, "Active");
            var micActive = ReadBoolProperty(voiceComponent, "MicActive");
            if (!force && (active || micActive))
                return;

            var activateMethod = voiceComponent.GetType().GetMethod(
                "Activate",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);

            if (activateMethod == null)
            {
                StatusText = "Meta Voice SDK: Activate() missing";
                return;
            }

            try
            {
                activateMethod.Invoke(voiceComponent, null);
                externalVoiceBridge?.MarkListeningStarted();
                StatusText = "Meta Voice SDK: Activate() called";
            }
            catch (Exception exception)
            {
                StatusText = $"Meta Voice SDK: Activate failed {exception.GetType().Name}";
            }
        }

        private void AddStringListener(string eventName, UnityAction<string> listener)
        {
            var unityEvent = GetMemberValue(voiceEvents, eventName) as UnityEvent<string>;
            unityEvent?.AddListener(listener);
        }

        private void AddVoidListener(string eventName, UnityAction listener)
        {
            var unityEvent = GetMemberValue(voiceEvents, eventName) as UnityEvent;
            unityEvent?.AddListener(listener);
        }

        private static Type FindType(string fullName, string assemblyQualifiedName)
        {
            var directType = Type.GetType(assemblyQualifiedName, false);
            if (directType != null)
                return directType;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static Component FindVoiceExperienceComponent(params Type[] candidateTypes)
        {
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (behaviour == null)
                    continue;

                var type = behaviour.GetType();
                foreach (var candidateType in candidateTypes)
                {
                    if (candidateType != null && candidateType.IsAssignableFrom(type))
                        return behaviour;
                }
            }

            return null;
        }

        private static Component CreateVoiceExperienceComponent(Type appVoiceType)
        {
            var host = GameObject.Find("App Voice Experience") ?? new GameObject("App Voice Experience");
            var existing = host.GetComponent(appVoiceType);
            if (existing != null)
                return existing;

            return host.AddComponent(appVoiceType) as Component;
        }

        private static bool HasUsableConfiguration(Component component)
        {
            var runtimeConfiguration = GetMemberValue(component, "RuntimeConfiguration");
            if (runtimeConfiguration != null)
            {
                var witConfiguration = GetMemberValue(runtimeConfiguration, "witConfiguration");
                if (HasClientAccessToken(witConfiguration))
                    return true;
            }

            return HasClientAccessToken(GetMemberValue(component, "Configuration"));
        }

        private static void TryAssignBuiltInWitConfiguration(Component component, string languageName)
        {
            var witConfig = CreateBuiltInWitConfiguration(languageName);
            if (witConfig == null)
                return;

            var runtimeConfig = CreateWitRuntimeConfiguration(witConfig);
            if (runtimeConfig == null)
                return;

            SetMemberValue(component, "RuntimeConfiguration", runtimeConfig);
        }

        private static UnityEngine.Object CreateBuiltInWitConfiguration(string languageName)
        {
            var witConfigurationType = FindType(WitConfigurationTypeName, WitConfigurationQualifiedTypeName);
            var appBuiltInsType = FindType(AppBuiltInsTypeName, AppBuiltInsQualifiedTypeName);
            if (witConfigurationType == null || appBuiltInsType == null)
                return null;

            var apps = GetStaticMemberValue(appBuiltInsType, "apps") as IDictionary;
            if (apps == null || !apps.Contains(languageName))
                return null;

            var appData = apps[languageName] as IDictionary;
            if (appData == null)
                return null;

            var token = appData["clientToken"] as string;
            if (string.IsNullOrEmpty(token))
                return null;

            var config = ScriptableObject.CreateInstance(witConfigurationType);
            if (config == null)
                return null;

            config.name = $"Arcane {languageName} Built-in WitConfiguration";
            InvokeMember(config, "SetClientAccessToken", token);
            TrySetApplicationInfo(config, appData);
            return config;
        }

        private static object CreateWitRuntimeConfiguration(UnityEngine.Object witConfiguration)
        {
            var runtimeConfigurationType = FindType(WitRuntimeConfigurationTypeName, WitRuntimeConfigurationQualifiedTypeName);
            if (runtimeConfigurationType == null)
                return null;

            var runtimeConfiguration = Activator.CreateInstance(runtimeConfigurationType);
            SetMemberValue(runtimeConfiguration, "witConfiguration", witConfiguration);
            return runtimeConfiguration;
        }

        private static void TrySetApplicationInfo(UnityEngine.Object config, IDictionary appData)
        {
            var appInfoType = FindType(WitAppInfoTypeName, WitAppInfoQualifiedTypeName);
            if (appInfoType == null)
                return;

            var appInfo = Activator.CreateInstance(appInfoType);
            SetMemberValue(appInfo, "name", appData["name"] as string);
            SetMemberValue(appInfo, "id", appData["id"] as string);
            SetMemberValue(appInfo, "lang", appData["lang"] as string);
            InvokeMember(config, "SetApplicationInfo", appInfo);
        }

        private static bool HasClientAccessToken(object witConfiguration)
        {
            if (witConfiguration == null)
                return false;

            var method = witConfiguration.GetType().GetMethod(
                "GetClientAccessToken",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (method == null)
                return true;

            var token = method.Invoke(witConfiguration, null) as string;
            return !string.IsNullOrEmpty(token);
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null)
                return null;

            var type = instance.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
                return property.GetValue(instance);

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? field.GetValue(instance) : null;
        }

        private static object GetStaticMemberValue(Type type, string memberName)
        {
            var property = type.GetProperty(memberName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
                return property.GetValue(null);

            var field = type.GetField(memberName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? field.GetValue(null) : null;
        }

        private static void SetMemberValue(object instance, string memberName, object value)
        {
            if (instance == null)
                return;

            var type = instance.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(instance, value);
                return;
            }

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            field?.SetValue(instance, value);
        }

        private static void InvokeMember(object instance, string memberName, params object[] parameters)
        {
            if (instance == null)
                return;

            var parameterTypes = new Type[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
                parameterTypes[i] = parameters[i]?.GetType() ?? typeof(object);

            var method = instance.GetType().GetMethod(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);

            method?.Invoke(instance, parameters);
        }

        private static bool ReadBoolProperty(object instance, string propertyName)
        {
            var value = GetMemberValue(instance, propertyName);
            return value is bool boolValue && boolValue;
        }

        private void ResolveExternalBridge()
        {
            if (externalVoiceBridge != null)
                return;

            externalVoiceBridge = FindAnyObjectByType<ExternalVoiceCommandBridge>();
            if (externalVoiceBridge == null)
                externalVoiceBridge = gameObject.AddComponent<ExternalVoiceCommandBridge>();
        }
    }
}
