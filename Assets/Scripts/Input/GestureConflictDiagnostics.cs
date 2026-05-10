using ArcaneVR.Combat;
using ArcaneVR.Spell;
using UnityEngine;

namespace ArcaneVR.Input
{
    public class GestureConflictDiagnostics : MonoBehaviour
    {
        [SerializeField] private GestureDetector gestureDetector;
        [SerializeField] private CombinationChecker combinationChecker;
        [SerializeField] private HandPullMovementController handPullMovement;
        [SerializeField] private int noisyPoseChangeThreshold = 8;

        private PoseId lastLeftPose = PoseId.None;
        private PoseId lastRightPose = PoseId.None;
        private float windowStartTime;
        private int poseChangeCount;
        private int comboSuccessCount;
        private int comboFailCount;
        private SpellId lastComboSpell = SpellId.None;

        public string SummaryStatus { get; private set; } = "waiting";
        public bool IsPoseNoisy { get; private set; }
        public bool IsPullDeclarationConflict { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            windowStartTime = Time.time;
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            ResolveReferences();
            IsPullDeclarationConflict = combinationChecker != null && combinationChecker.IsLeftDeclarationSuppressedByPull;

            if (Time.time - windowStartTime < 1f)
            {
                BuildSummary();
                return;
            }

            IsPoseNoisy = poseChangeCount >= noisyPoseChangeThreshold;
            poseChangeCount = 0;
            comboSuccessCount = 0;
            comboFailCount = 0;
            windowStartTime = Time.time;
            BuildSummary();
        }

        private void ResolveReferences()
        {
            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();
            if (combinationChecker == null)
                combinationChecker = FindAnyObjectByType<CombinationChecker>();
            if (handPullMovement == null)
                handPullMovement = FindAnyObjectByType<HandPullMovementController>();
        }

        private void Subscribe()
        {
            if (gestureDetector != null)
                gestureDetector.OnPoseDetected += HandlePoseDetected;

            if (combinationChecker != null)
            {
                combinationChecker.OnCombinationSuccess += HandleComboSuccess;
                combinationChecker.OnCombinationFail += HandleComboFail;
            }
        }

        private void Unsubscribe()
        {
            if (gestureDetector != null)
                gestureDetector.OnPoseDetected -= HandlePoseDetected;

            if (combinationChecker != null)
            {
                combinationChecker.OnCombinationSuccess -= HandleComboSuccess;
                combinationChecker.OnCombinationFail -= HandleComboFail;
            }
        }

        private void HandlePoseDetected(PoseId left, PoseId right)
        {
            if (left != lastLeftPose || right != lastRightPose)
                poseChangeCount++;

            lastLeftPose = left;
            lastRightPose = right;
        }

        private void HandleComboSuccess(SpellId spellId)
        {
            comboSuccessCount++;
            lastComboSpell = spellId;
        }

        private void HandleComboFail()
        {
            comboFailCount++;
        }

        private void BuildSummary()
        {
            var pullText = handPullMovement != null && handPullMovement.IsPulling ? $"Pull:{handPullMovement.ActiveHandName}" : "Pull:-";
            var noiseText = IsPoseNoisy ? "NoisyPose" : "PoseOK";
            var conflictText = IsPullDeclarationConflict ? "PullBlocksL" : "NoConflict";
            var comboText = lastComboSpell != SpellId.None ? lastComboSpell.ToString() : "-";
            SummaryStatus = $"{noiseText} {conflictText} {pullText} C+{comboSuccessCount}/-{comboFailCount} {comboText}";
        }
    }
}
