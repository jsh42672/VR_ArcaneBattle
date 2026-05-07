using System;
using ArcaneVR.Core;
using ArcaneVR.Spell;
using ArcaneVR.UI;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Receives two Pose IDs from GestureDetector and validates two-hand combination within a 0.5s window. Fires OnCombinationSuccess or OnCombinationFail events.
    /// </summary>
    public class CombinationChecker : MonoBehaviour
    {
        [SerializeField] private GestureDetector gestureDetector;
        [SerializeField] private GrimoireManager grimoireManager;
        [SerializeField] private float combinationWindow = 0.5f;
        [SerializeField] private bool emitFailEvents = true;
        [SerializeField] private bool allowCombosWithoutGameManager = true;
        [SerializeField] private bool allowLockedCombosInEditor = true;

        public event Action<SpellId> OnCombinationSuccess;
        public event Action OnCombinationFail;

        public ElementType CurrentElement { get; private set; } = ElementType.None;
        public PoseId CurrentAttackPose { get; private set; } = PoseId.None;

        private PoseId lastLeftPose = PoseId.None;
        private PoseId lastRightPose = PoseId.None;
        private float lastLeftPoseTime = -999f;
        private float lastRightPoseTime = -999f;
        private SpellId lastSpellId = SpellId.None;
        private float lastSuccessTime = -999f;
        private bool isGrimoireOpen;

        private void Awake()
        {
            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();

            if (grimoireManager == null)
                grimoireManager = FindAnyObjectByType<GrimoireManager>();
        }

        private void OnEnable()
        {
            if (gestureDetector != null)
                gestureDetector.OnPoseDetected += HandlePoseDetected;

            if (grimoireManager != null)
            {
                grimoireManager.OnGrimoireOpen += HandleGrimoireOpen;
                grimoireManager.OnGrimoireClose += HandleGrimoireClose;
                isGrimoireOpen = grimoireManager.IsOpen;
            }
        }

        private void OnDisable()
        {
            if (gestureDetector != null)
                gestureDetector.OnPoseDetected -= HandlePoseDetected;

            if (grimoireManager != null)
            {
                grimoireManager.OnGrimoireOpen -= HandleGrimoireOpen;
                grimoireManager.OnGrimoireClose -= HandleGrimoireClose;
            }
        }

        private void HandlePoseDetected(PoseId left, PoseId right)
        {
            var now = Time.time;
            var leftElement = PoseToElement(left);
            if (leftElement != ElementType.None)
                CurrentElement = leftElement;

            if (left != PoseId.None)
            {
                lastLeftPose = left;
                lastLeftPoseTime = now;
            }

            if (right != PoseId.None)
            {
                lastRightPose = right;
                lastRightPoseTime = now;
            }

            if (isGrimoireOpen && now - lastRightPoseTime <= combinationWindow)
            {
                var rightOnlySpellId = ResolveRightHandSingleSpell(lastRightPose);
                if (rightOnlySpellId != SpellId.None && CurrentElement != ElementType.None)
                {
                    TryEmitSuccess(rightOnlySpellId, now);
                    return;
                }
            }

            if (now - lastLeftPoseTime > combinationWindow || now - lastRightPoseTime > combinationWindow)
                return;

            var spellId = ResolveSpell(lastLeftPose, lastRightPose);
            if (spellId == SpellId.None)
            {
                if (emitFailEvents)
                    OnCombinationFail?.Invoke();
                return;
            }

            if (isGrimoireOpen && IsComboSpell(spellId))
            {
                EmitFail();
                return;
            }

            if (IsComboSpell(spellId) && !IsComboUnlocked(spellId))
            {
                EmitFail();
                return;
            }

            TryEmitSuccess(spellId, now);
        }

        private static SpellId ResolveSpell(PoseId left, PoseId right)
        {
            if (left == PoseId.Fist && right == PoseId.Ok)
                return SpellId.Combo_FireIce;

            if (left == PoseId.Ok && right == PoseId.Horn)
                return SpellId.Combo_IceThunder;

            if (left == PoseId.Horn && right == PoseId.Fist)
                return SpellId.Combo_ThunderFire;

            if (right == PoseId.IndexPoint && IsElementPose(left))
                return SpellId.Single_Pointer;

            if (right == PoseId.OpenPalm && IsElementPose(left))
                return SpellId.Single_Wave;

            if (right == PoseId.FistPush && IsElementPose(left))
                return SpellId.Single_Strike;

            return SpellId.None;
        }

        private static SpellId ResolveRightHandSingleSpell(PoseId right)
        {
            return right switch
            {
                PoseId.IndexPoint => SpellId.Single_Pointer,
                PoseId.OpenPalm => SpellId.Single_Wave,
                PoseId.FistPush => SpellId.Single_Strike,
                _ => SpellId.None
            };
        }

        public static ElementType PoseToElement(PoseId pose)
        {
            return pose switch
            {
                PoseId.Fist => ElementType.Fire,
                PoseId.Ok => ElementType.Ice,
                PoseId.Horn => ElementType.Thunder,
                _ => ElementType.None
            };
        }

        private static bool IsElementPose(PoseId pose)
        {
            return pose == PoseId.Fist || pose == PoseId.Ok || pose == PoseId.Horn;
        }

        private static bool IsComboSpell(SpellId spellId)
        {
            return spellId == SpellId.Combo_FireIce ||
                   spellId == SpellId.Combo_IceThunder ||
                   spellId == SpellId.Combo_ThunderFire;
        }

        private bool IsComboUnlocked(SpellId spellId)
        {
            if (allowLockedCombosInEditor && Application.isEditor)
                return true;

            var gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.UnlockData == null)
                return allowCombosWithoutGameManager;

            return spellId switch
            {
                SpellId.Combo_FireIce => gameManager.fireUnlocked && gameManager.iceUnlocked,
                SpellId.Combo_IceThunder => gameManager.iceUnlocked && gameManager.thunderUnlocked,
                SpellId.Combo_ThunderFire => gameManager.thunderUnlocked && gameManager.fireUnlocked,
                _ => true
            };
        }

        private void TryEmitSuccess(SpellId spellId, float now)
        {
            if (spellId == lastSpellId && now - lastSuccessTime < combinationWindow)
                return;

            lastSpellId = spellId;
            lastSuccessTime = now;
            CurrentAttackPose = lastRightPose;
            OnCombinationSuccess?.Invoke(spellId);
        }

        private void EmitFail()
        {
            if (emitFailEvents)
                OnCombinationFail?.Invoke();
        }

        private void HandleGrimoireOpen()
        {
            isGrimoireOpen = true;
        }

        private void HandleGrimoireClose()
        {
            isGrimoireOpen = false;
        }
    }
}
