using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneVR.EditorTests
{
    public class CombinationArchitectureEditorTests
    {
        const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void CombineGestureDetector_ExistsAsXrHandsOnlyInputInterpreter()
        {
            var type = Type.GetType("ArcaneVR.Input.CombineGestureDetector, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("leftHandTrackingEvents", InstanceFields));
            Assert.IsNotNull(type.GetField("rightHandTrackingEvents", InstanceFields));
            Assert.IsNotNull(type.GetField("leftCombineShape", InstanceFields));
            Assert.IsNotNull(type.GetField("rightCombineShape", InstanceFields));
            Assert.IsNull(type.GetField("leftOvrHand", InstanceFields));
            Assert.IsNull(type.GetField("rightOvrHand", InstanceFields));
        }

        [Test]
        public void CombinationFocusModeController_UsesCombineDetectorAndSharedTimeFocus()
        {
            var type = Type.GetType("ArcaneVR.Input.CombinationFocusModeController, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("combineGestureDetector", InstanceFields));
            Assert.IsNotNull(type.GetField("timeFocusController", InstanceFields));
            Assert.IsNotNull(type.GetField("leftGrimoireGesture", InstanceFields));
            Assert.IsNotNull(type.GetField("suppressLegacyGesturesDuringFocus", InstanceFields));
            Assert.IsNotNull(type.GetField("legacyGesturesToSuppress", InstanceFields));
        }

        [Test]
        public void CombinationChecker_ExposesLockedFocusStateMachine()
        {
            var type = Type.GetType("ArcaneVR.Input.CombinationChecker, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(Type.GetType("ArcaneVR.Input.CombinationState, Assembly-CSharp"));
            Assert.IsNotNull(type.GetProperty("State"));
            Assert.IsNotNull(type.GetProperty("FailureFeedbackSeconds"));
            Assert.IsNotNull(type.GetMethod("TickForTest"));
            Assert.IsNotNull(type.GetMethod("ReleaseFocusLock"));
        }

        [Test]
        public void CombinationChecker_SameElementsFailAndLockDeclarations()
        {
            var checkerType = Type.GetType("ArcaneVR.Input.CombinationChecker, Assembly-CSharp");
            var elementType = Type.GetType("ArcaneVR.Spell.ElementType, Assembly-CSharp");
            var stateType = Type.GetType("ArcaneVR.Input.CombinationState, Assembly-CSharp");
            Assert.IsNotNull(checkerType);
            Assert.IsNotNull(elementType);
            Assert.IsNotNull(stateType);

            var owner = new GameObject("CombinationChecker Test");
            try
            {
                var checker = owner.AddComponent(checkerType);
                var submit = checkerType.GetMethod("SubmitElementDeclarationForTest");
                var fire = Enum.Parse(elementType, "Fire");
                var ice = Enum.Parse(elementType, "Ice");

                Assert.IsTrue((bool)submit.Invoke(checker, new[] { (object)true, fire }));
                Assert.IsFalse((bool)submit.Invoke(checker, new[] { (object)false, fire }));
                Assert.IsFalse((bool)submit.Invoke(checker, new[] { (object)false, ice }));

                var state = checkerType.GetProperty("State")?.GetValue(checker);
                Assert.AreEqual(Enum.Parse(stateType, "ComboFailed"), state);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void SpellCaster_CanSuppressVoiceDuringCombinationFocus()
        {
            var type = Type.GetType("ArcaneVR.Spell.SpellCaster, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("ignoreVoiceDuringCombinationFocus", InstanceFields));
            Assert.IsNotNull(type.GetMethod("IsVoiceInputSuppressedForCombinationFocus", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [Test]
        public void CombinationChecker_ExposesFocusExpirationFailure()
        {
            var type = Type.GetType("ArcaneVR.Input.CombinationChecker, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetMethod("ReportFocusExpired"));
        }

        [Test]
        public void SpellCaster_ExposesCombinationAuraFeedback()
        {
            var type = Type.GetType("ArcaneVR.Spell.SpellCaster, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("showCombinationAura", InstanceFields));
            Assert.IsNotNull(type.GetField("combinationAuraRoot", InstanceFields));
            Assert.IsNotNull(type.GetMethod("UpdateCombinationAuraFeedback", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(type.GetMethod("GetComboAuraColor", BindingFlags.Static | BindingFlags.NonPublic));
        }

        [Test]
        public void ElementAuraDummy_ProvidesSharedColoredTimeFocusExemptAura()
        {
            var auraType = Type.GetType("ArcaneVR.Spell.ElementAuraDummy, Assembly-CSharp");
            var casterType = Type.GetType("ArcaneVR.Spell.SpellCaster, Assembly-CSharp");

            Assert.IsNotNull(auraType);
            Assert.IsNotNull(auraType.GetMethod("Create"));
            Assert.IsNotNull(auraType.GetMethod("Configure"));
            Assert.IsNotNull(auraType.GetMethod("ApplyLayerRecursively"));
            Assert.IsNotNull(casterType);
            Assert.IsNotNull(casterType.GetField("useCommonDummyAuras", InstanceFields));
            Assert.IsNotNull(casterType.GetField("auraTimeFocusExemptLayerName", InstanceFields));
            Assert.IsNotNull(casterType.GetField("rightIceAuraInstance", InstanceFields));
            Assert.IsNotNull(casterType.GetMethod("ShowRightElementAura", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [Test]
        public void SpellCaster_UsesXrGestureDetectorAndOwnsDummyAttackPrefabs()
        {
            var type = Type.GetType("ArcaneVR.Spell.SpellCaster, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("gestureDetector", InstanceFields));
            Assert.IsNotNull(type.GetField("rightFireballPrefab", InstanceFields));
            Assert.IsNotNull(type.GetField("rightIceProjectilePrefab", InstanceFields));
            Assert.IsNotNull(type.GetField("rightThunderAuraPrefab", InstanceFields));
            Assert.IsNotNull(type.GetField("rightThunderRangeMeters", InstanceFields));
            Assert.IsNotNull(type.GetField("rightThunderChargeGraceSeconds", InstanceFields));
            Assert.IsNotNull(type.GetField("rightThunderShootPoseGraceSeconds", InstanceFields));
            Assert.IsNull(type.GetField("prototypeHand", InstanceFields));
        }

        [Test]
        public void GestureDetector_SeparatesThunderChargeAndShootGestures()
        {
            var type = Type.GetType("ArcaneVR.Input.GestureDetector, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("rightThunderGesture", InstanceFields));
            Assert.IsNotNull(type.GetField("rightThunderShootGesture", InstanceFields));
            Assert.IsNotNull(type.GetField("rightThunderShootArmWindowSeconds", InstanceFields));
            Assert.IsNotNull(type.GetField("rightThunderShootArmedUntilTime", InstanceFields));
        }

        [Test]
        public void TimeStopSystems_CentralizesGrimoireAndCombinationLocks()
        {
            var type = Type.GetType("ArcaneVR.Input.TimeStopSystems, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("timeFocusController", InstanceFields));
            Assert.IsNotNull(type.GetField("leftGrimoireGesture", InstanceFields));
            Assert.IsNotNull(type.GetField("rightPageTurnGesture", InstanceFields));
            Assert.IsNotNull(type.GetField("combinationFocusController", InstanceFields));
            Assert.IsNotNull(type.GetField("combinationChecker", InstanceFields));
            Assert.IsNotNull(type.GetField("handPullMovement", InstanceFields));
            Assert.IsNotNull(type.GetField("spellCaster", InstanceFields));
            Assert.IsNotNull(type.GetProperty("IsGrimoireTimeStopActive"));
            Assert.IsNotNull(type.GetProperty("IsCombinationTimeStopActive"));
        }

        [Test]
        public void GrimoireManager_CanBeSuppressedByCombinationFocus()
        {
            var type = Type.GetType("ArcaneVR.UI.GrimoireManager, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("leftHandBookAnchor", InstanceFields));
            Assert.IsNotNull(type.GetProperty("IsExternallySuppressed"));
            Assert.IsNotNull(type.GetMethod("SetExternalSuppressed"));
            Assert.IsNotNull(type.GetMethod("IsSuppressed", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [Test]
        public void LeftGrimoireGesture_CanBeSuppressedByCombinationFocus()
        {
            var type = Type.GetType("ArcaneVR.Input.LeftGrimoireGesture, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetProperty("IsExternallySuppressed"));
            Assert.IsNotNull(type.GetMethod("SetExternalSuppressed"));
        }
    }
}
