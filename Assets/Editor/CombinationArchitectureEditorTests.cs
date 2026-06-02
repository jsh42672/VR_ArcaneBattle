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
        public void GrimoireManager_CanBeSuppressedByCombinationFocus()
        {
            var type = Type.GetType("ArcaneVR.UI.GrimoireManager, Assembly-CSharp");

            Assert.IsNotNull(type);
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
