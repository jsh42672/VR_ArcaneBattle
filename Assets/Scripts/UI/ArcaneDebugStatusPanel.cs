using System.Text;
using ArcaneVR.Combat;
using ArcaneVR.Input;
using ArcaneVR.Spell;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.UI
{
    [DefaultExecutionOrder(120)]
    public class ArcaneDebugStatusPanel : MonoBehaviour
    {
        [SerializeField] private GestureDetector gestureDetector;
        [SerializeField] private CombinationChecker combinationChecker;
        [SerializeField] private BarrierController barrierController;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private VoiceRecognizer voiceRecognizer;
        [SerializeField] private SpellCaster spellCaster;
        [SerializeField] private GolemCombatTarget golemTarget;
        [SerializeField] private ArcaneActionModeController actionModeController;
        [SerializeField] private HandPullMovementController handPullMovement;
        [SerializeField] private GestureConflictDiagnostics gestureDiagnostics;
        [SerializeField] private MagicSystemTestDriver testDriver;
        [SerializeField] private MetaVoiceSdkAutoBridge metaVoiceSdkBridge;
        [SerializeField] private bool attachToHead = true;
        [SerializeField] private Vector3 headLocalPosition = new Vector3(-0.62f, -0.08f, 1.35f);
        [SerializeField] private Vector3 fallbackWorldPosition = new Vector3(-0.8f, 1.45f, 2.8f);
        [SerializeField] private int fontSize = 48;
        [SerializeField] private float characterSize = 0.009f;
        [SerializeField] private float statusRefreshInterval = 0.1f;
        [SerializeField] private bool usePresentationLayout = true;
        [SerializeField] private bool showGestureModeLine = true;
        [SerializeField] private bool showLowLevelDetails;

        private readonly StringBuilder builder = new StringBuilder();
        private TextMesh textMesh;
        private float nextStatusRefreshTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForArcaneScenes()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (!HandGestureDebugOverlay.IsGestureOverlayScene(sceneName))
                return;

            if (FindAnyObjectByType<ArcaneDebugStatusPanel>() != null)
                return;

            var host = new GameObject("Arcane Debug Status Panel");
            host.AddComponent<ArcaneDebugStatusPanel>();
        }

        private void Awake()
        {
            EnsureTextMesh();
            ResolveReferences();
        }

        private void LateUpdate()
        {
            AttachToView();
            if (Time.unscaledTime < nextStatusRefreshTime)
                return;

            nextStatusRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, statusRefreshInterval);
            ResolveReferences();
            RefreshText();
        }

        private void EnsureTextMesh()
        {
            if (textMesh != null)
                return;

            textMesh = GetComponent<TextMesh>();
            if (textMesh == null)
                textMesh = gameObject.AddComponent<TextMesh>();

            textMesh.anchor = TextAnchor.UpperLeft;
            textMesh.alignment = TextAlignment.Left;
            textMesh.fontSize = fontSize;
            textMesh.characterSize = characterSize;
            textMesh.color = Color.white;
        }

        private void ResolveReferences()
        {
            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();
            if (combinationChecker == null)
                combinationChecker = FindAnyObjectByType<CombinationChecker>();
            if (barrierController == null)
                barrierController = FindAnyObjectByType<BarrierController>();
            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();
            if (voiceRecognizer == null)
                voiceRecognizer = FindAnyObjectByType<VoiceRecognizer>();
            if (spellCaster == null)
                spellCaster = FindAnyObjectByType<SpellCaster>();
            if (golemTarget == null)
                golemTarget = FindAnyObjectByType<GolemCombatTarget>();
            if (actionModeController == null)
                actionModeController = FindAnyObjectByType<ArcaneActionModeController>();
            if (handPullMovement == null)
                handPullMovement = FindAnyObjectByType<HandPullMovementController>();
            if (gestureDiagnostics == null)
                gestureDiagnostics = FindAnyObjectByType<GestureConflictDiagnostics>();
            if (testDriver == null)
                testDriver = FindAnyObjectByType<MagicSystemTestDriver>();
            if (metaVoiceSdkBridge == null)
                metaVoiceSdkBridge = FindAnyObjectByType<MetaVoiceSdkAutoBridge>();
        }

        private void AttachToView()
        {
            var camera = Camera.main;
            if (attachToHead && camera != null)
            {
                if (transform.parent != camera.transform)
                    transform.SetParent(camera.transform, false);

                transform.localPosition = headLocalPosition;
                transform.localRotation = Quaternion.identity;
                return;
            }

            if (transform.parent != null)
                transform.SetParent(null, true);

            transform.position = fallbackWorldPosition;
            if (camera != null)
                transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, Vector3.up);
        }

        private void RefreshText()
        {
            builder.Clear();
            builder.AppendLine(usePresentationLayout ? "ARCANE DEMO" : "ARCANE TEST");
            AppendVoiceStatus();
            AppendCastStatus();
            AppendComboStatus();
            if (showGestureModeLine)
                AppendModeStatus();
            AppendGuardStatus();
            AppendGolemStatus();
            AppendTestDriverStatus();

            textMesh.text = builder.ToString();
        }

        private void AppendVoiceStatus()
        {
            builder.Append("VOICE ");
            if (voiceRecognizer == null)
            {
                builder.AppendLine("missing");
                return;
            }

            builder.Append(string.IsNullOrEmpty(voiceRecognizer.LastRecognizedPhrase)
                ? "-"
                : Compact(voiceRecognizer.LastRecognizedPhrase, 36));
            builder.Append(" | ");
            builder.AppendLine(voiceRecognizer.LastRecognizedElement.ToString());
        }

        private void AppendComboStatus()
        {
            builder.Append("COMBO ");
            if (combinationChecker == null)
            {
                builder.AppendLine("missing");
                return;
            }

            builder.Append("Ready ");
            builder.Append(Mark(combinationChecker.IsComboReady));
            builder.Append(" | L ");
            builder.Append(combinationChecker.LeftDeclaredElement);
            builder.Append(" / R ");
            builder.Append(combinationChecker.RightDeclaredElement);
            builder.Append(" | ");
            builder.Append(ComboName(combinationChecker.CurrentComboCandidate));
            if (combinationChecker.IsLeftDeclarationSuppressedByPull ||
                combinationChecker.IsLeftDeclarationSuppressedByMode)
            {
                builder.Append(" Supp ");
                builder.Append(combinationChecker.IsLeftDeclarationSuppressedByPull ? "Pull" : "Mode");
            }
            if (showLowLevelDetails && gestureDetector != null)
            {
                builder.Append(" | Combine ");
                builder.Append(Mark(gestureDetector.IsCombineCandidate));
                builder.Append(" Push ");
                builder.Append(gestureDetector.CurrentCombineForwardSpeed.ToString("0.00"));
            }
            builder.Append(" | ");
            builder.Append(Compact(combinationChecker.LastComboStatus, 28));
            builder.AppendLine();
        }

        private void AppendModeStatus()
        {
            builder.Append("MODE ");
            if (actionModeController == null)
            {
                builder.AppendLine("missing");
                return;
            }

            builder.Append(actionModeController.ShortStatusText);
            builder.Append(" | Triangle ");
            builder.Append(Mark(actionModeController.IsTriangleGestureActive));
            builder.Append(" ");
            builder.Append(actionModeController.TriangleHoldProgress.ToString("0.0"));
            builder.Append(" | PullLock ");
            builder.Append(Mark(handPullMovement != null && handPullMovement.IsMovementSuppressed));
            if (showLowLevelDetails)
            {
                builder.Append(" | ");
                builder.Append(Compact(actionModeController.TriangleDebugText, 38));
            }
            if (actionModeController.CastModeRemaining >= 0f)
            {
                builder.Append(" ");
                builder.Append(actionModeController.CastModeRemaining.ToString("0.0"));
                builder.Append("s");
            }
            builder.AppendLine();
        }

        private void AppendGuardStatus()
        {
            builder.Append("GUARD ");
            if (barrierController == null)
            {
                builder.AppendLine("missing");
                return;
            }

            if (barrierController.IsResponseWindowOpen)
            {
                builder.Append("NOW ");
                builder.Append(barrierController.ResponseWindowRemaining.ToString("0.0"));
                builder.Append("s");
            }
            else if (testDriver != null && testDriver.NextBarrierResponseIn >= 0f)
            {
                builder.Append("Next ");
                builder.Append(testDriver.NextBarrierResponseIn.ToString("0.0"));
                builder.Append("s");
            }
            else
            {
                builder.Append("Idle");
            }

            builder.Append(" | Pose ");
            builder.Append(Mark(barrierController.IsGuardPoseActive));
            builder.Append(" Active ");
            builder.Append(Mark(barrierController.IsBarrierActive));
            builder.Append(" | ");
            if (barrierController.IsResponseWindowOpen || showLowLevelDetails)
            {
                builder.Append("Hold ");
                builder.Append(barrierController.GuardHoldTime.ToString("0.00"));
                builder.Append("/");
                builder.Append(barrierController.RequiredHoldTime.ToString("0.00"));
                builder.Append(" | ");
            }
            builder.AppendLine(barrierController.LastResultText);
        }

        private void AppendGolemStatus()
        {
            builder.Append("GOLEM ");
            if (golemTarget == null)
            {
                builder.AppendLine("missing");
                return;
            }

            builder.Append(golemTarget.CurrentCombatCue);
            builder.Append(" | B ");
            builder.Append(Mark(golemTarget.IsBarrierActive));
            builder.Append(" W ");
            builder.Append(Mark(golemTarget.IsWeakExposed));
            builder.Append(" C ");
            builder.Append(Mark(golemTarget.IsChargeCounterWindowOpen));
            builder.Append(" HP ");
            builder.Append(golemTarget.CurrentHealth.ToString("0"));
            builder.Append("/");
            builder.AppendLine(golemTarget.MaxHealth.ToString("0"));
        }

        private void AppendCastStatus()
        {
            builder.Append("CAST ");
            builder.Append(spellCaster != null ? spellCaster.PrototypeArmStatus : "missing");
            builder.Append(" | Mana ");
            builder.Append(combatManager != null ? $"{combatManager.CurrentMana:0.#}/{combatManager.MaxMana:0.#}" : "-/-");
            builder.Append(" | ");
            builder.Append(spellCaster != null ? spellCaster.LastManaCostStatus : "Cost: missing");
            if (showLowLevelDetails)
            {
                builder.Append(" | R ");
                builder.Append(ResolveRightPose());
                builder.Append(" Pull ");
                builder.Append(Mark(handPullMovement != null && handPullMovement.IsPulling));
            }
            builder.AppendLine();
        }

        private void AppendTestDriverStatus()
        {
            if (testDriver == null)
                return;

            builder.Append("TEST ");
            builder.Append(Compact(testDriver.DriverStatus, 58));
            if (testDriver.NextChargeWindowIn >= 0f)
            {
                builder.Append(" | Charge ");
                builder.Append(testDriver.NextChargeWindowIn.ToString("0.0"));
                builder.Append("s");
            }
            builder.AppendLine();
        }

        private string ResolveLeftPose()
        {
            if (gestureDetector == null)
                return "-";

            return gestureDetector.CurrentLeftPrototypePose != PoseType.None
                ? gestureDetector.CurrentLeftPrototypePose.ToString()
                : gestureDetector.CurrentLeftPose.ToString();
        }

        private string ResolveRightPose()
        {
            if (gestureDetector == null)
                return "-";

            return gestureDetector.CurrentRightPrototypePose != PoseType.None
                ? gestureDetector.CurrentRightPrototypePose.ToString()
                : gestureDetector.CurrentRightPose.ToString();
        }

        private static string Mark(bool value)
        {
            return value ? "O" : "X";
        }

        private static string ComboName(SpellId spellId)
        {
            return spellId == SpellId.None ? "None" : SpellHitData.GetDisplayName(spellId);
        }

        private static string Compact(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return "-";

            text = text.Replace('\n', ' ').Replace('\r', ' ');
            if (text.Length <= maxLength)
                return text;

            return text.Substring(0, Mathf.Max(0, maxLength - 3)) + "...";
        }
    }
}
