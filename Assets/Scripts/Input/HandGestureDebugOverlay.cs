using ArcaneVR.Combat;
using ArcaneVR.Spell;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.Input
{
    [DefaultExecutionOrder(50)]
    public class HandGestureDebugOverlay : MonoBehaviour
    {
        private const int LeftHand = 0;
        private const int RightHand = 1;
        private const int OpenGesture = 0;
        private const int TwoGesture = 1;
        private const int ThumbGesture = 2;

        [SerializeField] private GestureDetector gestureDetector;
        [SerializeField] private SpellCaster spellCaster;
        [SerializeField] private HandPullMovementController handPullMovement;
        [SerializeField] private CombinationChecker combinationChecker;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private VoiceRecognizer voiceRecognizer;
        [SerializeField] private bool attachToHead = true;
        [SerializeField] private Vector3 localRootPosition = new Vector3(0f, 0f, 2.65f);
        [SerializeField] private Vector3 fallbackWorldPosition = new Vector3(0f, 1.45f, 3.1f);
        [SerializeField] private float textCharacterSize = 0.008f;
        [SerializeField] private int fontSize = 50;
        [SerializeField] private float statusRefreshInterval = 0.1f;
        [SerializeField] private bool showDetailedDebug;
        [SerializeField] private bool showCastDebug;

        private readonly TextMesh[,] statusTexts = new TextMesh[2, 3];
        private readonly TextMesh[] poseTexts = new TextMesh[2];
        private readonly TextMesh[] fingerTexts = new TextMesh[2];
        private readonly TextMesh[] handStatusTexts = new TextMesh[2];
        private readonly TextMesh[] prototypeDebugTexts = new TextMesh[2];
        private TextMesh spellCasterText;
        private TextMesh movementText;
        private TextMesh arcaneStateText;
        private Transform overlayRoot;
        private float nextStatusRefreshTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForHandTestScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (!ShouldAutoCreateOverlay(sceneName))
                return;

            if (FindAnyObjectByType<HandGestureDebugOverlay>() != null)
                return;

            var host = new GameObject("Arcane Gesture Text Overlay");
            if (FindAnyObjectByType<GestureDetector>() == null)
                host.AddComponent<GestureDetector>();

            host.AddComponent<HandGestureDebugOverlay>();
        }

        public static bool IsPrototypeScene(string sceneName)
        {
            return sceneName.StartsWith("HandTest") ||
                   sceneName == "Main" ||
                   sceneName == "DogeTest" ||
                   sceneName == "DodgeTest" ||
                   sceneName == "TestScene_GestureProto" ||
                   sceneName == "TestScene_Input";
        }

        public static bool ShouldAutoCreateOverlay(string sceneName)
        {
            return sceneName.StartsWith("HandTest") ||
                   sceneName == "TestScene_GestureProto" ||
                   sceneName == "TestScene_Input";
        }

        public static bool IsGestureOverlayScene(string sceneName)
        {
            return IsPrototypeScene(sceneName) ||
                   sceneName == "World" ||
                   sceneName == "World_main" ||
                   sceneName == "Tutorial" ||
                   sceneName == "FireColoseum" ||
                   sceneName == "IceColoseum" ||
                   sceneName == "ElectricColoseum";
        }

        public void Configure(GestureDetector detector)
        {
            gestureDetector = detector;
            if (spellCaster == null)
                spellCaster = FindAnyObjectByType<SpellCaster>();

            if (handPullMovement == null)
                handPullMovement = FindAnyObjectByType<HandPullMovementController>();

            if (combinationChecker == null)
                combinationChecker = FindAnyObjectByType<CombinationChecker>();

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();

            if (voiceRecognizer == null)
                voiceRecognizer = FindAnyObjectByType<VoiceRecognizer>();
        }

        private void Awake()
        {
            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();

            if (spellCaster == null)
                spellCaster = FindAnyObjectByType<SpellCaster>();

            if (handPullMovement == null)
                handPullMovement = FindAnyObjectByType<HandPullMovementController>();

            if (combinationChecker == null)
                combinationChecker = FindAnyObjectByType<CombinationChecker>();

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();

            if (voiceRecognizer == null)
                voiceRecognizer = FindAnyObjectByType<VoiceRecognizer>();

            BuildOverlay();
        }

        private void LateUpdate()
        {
            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();

            if (spellCaster == null)
                spellCaster = FindAnyObjectByType<SpellCaster>();

            if (handPullMovement == null)
                handPullMovement = FindAnyObjectByType<HandPullMovementController>();

            if (combinationChecker == null)
                combinationChecker = FindAnyObjectByType<CombinationChecker>();

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();

            if (voiceRecognizer == null)
                voiceRecognizer = FindAnyObjectByType<VoiceRecognizer>();

            AttachOverlay();
            if (Time.unscaledTime < nextStatusRefreshTime)
                return;

            nextStatusRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, statusRefreshInterval);
            RefreshStatuses();
        }

        private void BuildOverlay()
        {
            overlayRoot = new GameObject("Gesture Status Text").transform;
            overlayRoot.SetParent(transform, false);
            overlayRoot.localPosition = fallbackWorldPosition;
            overlayRoot.localRotation = Quaternion.identity;

            BuildHandTextBlock(LeftHand, "LEFT", -0.78f, TextAnchor.MiddleLeft);
            BuildHandTextBlock(RightHand, "RIGHT", 0.18f, TextAnchor.MiddleLeft);
            if (showCastDebug)
                spellCasterText = CreateText("CAST: waiting", new Vector3(-0.78f, -0.52f, 0f), TextAnchor.MiddleLeft, Color.gray);

            movementText = CreateText("MOVE: waiting", new Vector3(-0.78f, showCastDebug ? -0.62f : -0.52f, 0f), TextAnchor.MiddleLeft, Color.gray);
            arcaneStateText = CreateText("ARCANE: waiting", new Vector3(-0.78f, showCastDebug ? -0.72f : -0.62f, 0f), TextAnchor.MiddleLeft, Color.gray);
        }

        private void BuildHandTextBlock(int handIndex, string title, float x, TextAnchor anchor)
        {
            CreateText(title, new Vector3(x, 0.33f, 0f), anchor, Color.white);

            BuildGestureRow(handIndex, OpenGesture, "OPEN", x, 0.23f, anchor);
            BuildGestureRow(handIndex, TwoGesture, "TWO", x, 0.13f, anchor);
            BuildGestureRow(handIndex, ThumbGesture, "THUMB", x, 0.03f, anchor);
            poseTexts[handIndex] = CreateText("POSE: None", new Vector3(x, -0.09f, 0f), anchor, Color.gray);
            handStatusTexts[handIndex] = CreateText("TRACK: WAIT", new Vector3(x, -0.19f, 0f), anchor, Color.gray);

            if (!showDetailedDebug)
                return;

            fingerTexts[handIndex] = CreateText("T:- I:- M:- R:- P:-", new Vector3(x, -0.29f, 0f), anchor, Color.gray);
            prototypeDebugTexts[handIndex] = CreateText("debug: waiting", new Vector3(x, -0.39f, 0f), anchor, Color.gray);
        }

        private void BuildGestureRow(int handIndex, int gestureIndex, string label, float x, float y, TextAnchor anchor)
        {
            CreateText(label, new Vector3(x, y, 0f), anchor, Color.white);
            statusTexts[handIndex, gestureIndex] = CreateText("X", new Vector3(x + 0.32f, y, 0f), anchor, Color.red);
        }

        private TextMesh CreateText(string text, Vector3 localPosition, TextAnchor anchor, Color color)
        {
            var textObject = new GameObject(text + " Text");
            textObject.transform.SetParent(overlayRoot, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.identity;

            var textMesh = textObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = anchor;
            textMesh.alignment = TextAlignment.Left;
            textMesh.fontSize = fontSize;
            textMesh.characterSize = textCharacterSize;
            textMesh.color = color;
            return textMesh;
        }

        private void AttachOverlay()
        {
            if (overlayRoot == null)
                return;

            var camera = Camera.main;
            if (attachToHead && camera != null)
            {
                if (overlayRoot.parent != camera.transform)
                    overlayRoot.SetParent(camera.transform, false);

                overlayRoot.localPosition = localRootPosition;
                overlayRoot.localRotation = Quaternion.identity;
                return;
            }

            if (overlayRoot.parent != transform)
                overlayRoot.SetParent(transform, false);

            overlayRoot.localPosition = fallbackWorldPosition;
            overlayRoot.localRotation = Quaternion.identity;
        }

        private void RefreshStatuses()
        {
            var leftPose = gestureDetector != null ? gestureDetector.CurrentLeftPose : PoseId.None;
            var rightPose = gestureDetector != null ? gestureDetector.CurrentRightPose : PoseId.None;
            var leftFingerDebug = gestureDetector != null ? gestureDetector.CurrentLeftFingerDebug : default;
            var rightFingerDebug = gestureDetector != null ? gestureDetector.CurrentRightFingerDebug : default;

            SetHandStatuses(
                LeftHand,
                leftPose,
                gestureDetector != null ? gestureDetector.CurrentLeftPrototypePose : PoseType.None,
                leftFingerDebug,
                gestureDetector != null ? gestureDetector.CurrentLeftHandStatus : "L: detector missing",
                gestureDetector != null ? gestureDetector.CurrentLeftPrototypeDebug : "L: detector missing");
            SetHandStatuses(
                RightHand,
                rightPose,
                gestureDetector != null ? gestureDetector.CurrentRightPrototypePose : PoseType.None,
                rightFingerDebug,
                gestureDetector != null ? gestureDetector.CurrentRightHandStatus : "R: detector missing",
                gestureDetector != null ? gestureDetector.CurrentRightPrototypeDebug : "R: detector missing");

            if (spellCasterText != null)
            {
                spellCasterText.text = spellCaster != null ? spellCaster.PrototypeDebugStatus : "CAST: caster missing";
                spellCasterText.color = spellCaster != null && spellCaster.PrototypeDebugStatus.Contains("fired")
                    ? Color.green
                    : Color.white;
            }

            if (movementText != null)
            {
                if (handPullMovement == null)
                {
                    movementText.text = "MOVE: missing";
                    movementText.color = Color.red;
                }
                else
                {
                    var delta = handPullMovement.LastMoveDelta;
                    movementText.text = handPullMovement.IsPulling
                        ? $"MOVE: {handPullMovement.ActiveHandName} ({delta.x:F2},{delta.z:F2})"
                        : $"MOVE: {handPullMovement.LastDebugMessage}";
                    movementText.color = handPullMovement.IsPulling ? Color.green : Color.gray;
                }
            }

            if (arcaneStateText != null)
            {
                var comboText = combinationChecker != null
                    ? $"Combo:{ToMark(combinationChecker.IsComboReady)} {combinationChecker.LeftDeclaredElement}/{combinationChecker.RightDeclaredElement} {combinationChecker.CurrentComboCandidate}"
                    : "Combo:missing";
                var manaText = combatManager != null
                    ? $"Mana:{combatManager.CurrentMana:0.#}/{combatManager.MaxMana:0.#}{(combatManager.IsManaDisrupted ? " DISRUPT" : string.Empty)}"
                    : "Mana:missing";
                var voiceText = voiceRecognizer != null ? $"Voice:{voiceRecognizer.ShortStatusText}" : "Voice:missing";
                arcaneStateText.text = $"ARCANE {manaText} | {voiceText} | {comboText}";
                arcaneStateText.color = combinationChecker != null && combinationChecker.IsComboReady ? Color.cyan : Color.white;
            }
        }

        private void SetHandStatuses(
            int handIndex,
            PoseId pose,
            PoseType prototypePose,
            FingerPoseDebug fingerDebug,
            string handStatus,
            string prototypeDebug)
        {
            var displayPose = ResolveDisplayPose(pose, prototypePose);
            SetStatus(handIndex, OpenGesture, displayPose == PoseType.OpenPalm);
            SetStatus(handIndex, TwoGesture, displayPose == PoseType.TwoFinger);
            SetStatus(handIndex, ThumbGesture, displayPose == PoseType.ThumbsUp);

            var poseText = poseTexts[handIndex];
            if (poseText == null)
                return;

            poseText.text = $"POSE: {displayPose}";
            poseText.color = displayPose == PoseType.None ? Color.gray : Color.cyan;

            var fingerText = fingerTexts[handIndex];
            if (fingerText != null)
            {
                fingerText.text = fingerDebug.ToCompactString();
                fingerText.color = fingerDebug.hasData ? Color.white : Color.gray;
            }

            var handStatusText = handStatusTexts[handIndex];
            if (handStatusText != null)
            {
                var tracked = handStatus.Contains("tracked:True") ||
                              handStatus.Contains("tracked:true") ||
                              handStatus.Contains("XRRouter") ||
                              fingerDebug.hasData;
                handStatusText.text = showDetailedDebug ? handStatus : (tracked ? "TRACK: OK" : "TRACK: WAIT");
                handStatusText.color = tracked ? Color.green : Color.yellow;
            }

            var prototypeDebugText = prototypeDebugTexts[handIndex];
            if (prototypeDebugText != null)
            {
                prototypeDebugText.text = prototypeDebug;
                prototypeDebugText.color = prototypePose == PoseType.None ? Color.gray : Color.cyan;
            }
        }

        private static PoseType ResolveDisplayPose(PoseId pose, PoseType prototypePose)
        {
            if (prototypePose != PoseType.None)
                return prototypePose;

            return pose switch
            {
                PoseId.OpenPalm => PoseType.OpenPalm,
                PoseId.Ok => PoseType.TwoFinger,
                _ => PoseType.None
            };
        }

        private void SetStatus(int handIndex, int gestureIndex, bool active)
        {
            var statusText = statusTexts[handIndex, gestureIndex];
            if (statusText == null)
                return;

            statusText.text = ToMark(active);
            statusText.color = active ? Color.green : Color.red;
        }

        private static string ToMark(bool active)
        {
            return active ? "O" : "X";
        }
    }
}
