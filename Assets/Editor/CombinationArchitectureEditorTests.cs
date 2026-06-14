using System;
using System.IO;
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
        public void CombinationChecker_SameElementsRemainDeclaredAndAllowRedeclaration()
        {
            var checkerType = Type.GetType("ArcaneVR.Input.CombinationChecker, Assembly-CSharp");
            var elementType = Type.GetType("ArcaneVR.Spell.ElementType, Assembly-CSharp");
            var stateType = Type.GetType("ArcaneVR.Input.CombinationState, Assembly-CSharp");
            var spellIdType = Type.GetType("ArcaneVR.Spell.SpellId, Assembly-CSharp");
            Assert.IsNotNull(checkerType);
            Assert.IsNotNull(elementType);
            Assert.IsNotNull(stateType);
            Assert.IsNotNull(spellIdType);

            var owner = new GameObject("CombinationChecker Test");
            try
            {
                var checker = owner.AddComponent(checkerType);
                var submit = checkerType.GetMethod("SubmitElementDeclarationForTest");
                var fire = Enum.Parse(elementType, "Fire");
                var ice = Enum.Parse(elementType, "Ice");

                Assert.IsTrue((bool)submit.Invoke(checker, new[] { (object)true, fire }));
                Assert.IsTrue((bool)submit.Invoke(checker, new[] { (object)false, fire }));

                var state = checkerType.GetProperty("State")?.GetValue(checker);
                Assert.AreEqual(Enum.Parse(stateType, "ElementDeclared"), state);

                Assert.IsTrue((bool)submit.Invoke(checker, new[] { (object)false, ice }));

                var candidate = checkerType.GetProperty("CurrentComboCandidate")?.GetValue(checker);
                Assert.AreEqual(Enum.Parse(spellIdType, "Combo_FireIce"), candidate);
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
            Assert.IsNotNull(type.GetField("combinationFeedbackSfxVolume", InstanceFields));
            Assert.IsNotNull(type.GetField("_comboFeedbackAudio", InstanceFields));
            Assert.IsNotNull(type.GetField("_combinationAuraRoot", InstanceFields));
            Assert.IsNotNull(type.GetMethod("UpdateCombinationAuraFeedback", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(type.GetMethod("PlayCombinationFeedbackSfx", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(type.GetMethod("LogComboSfx", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(type.GetMethod("GetComboAuraColor", BindingFlags.Static | BindingFlags.NonPublic));
        }

        [Test]
        public void SpellCaster_OnValidate_ReplacesInactiveHeadReferenceWithMainCamera()
        {
            var type = Type.GetType("ArcaneVR.Spell.SpellCaster, Assembly-CSharp");
            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic));

            var inactiveHead = new GameObject("Inactive Head");
            var mainCameraGo = new GameObject("Active Main Camera");
            try
            {
                inactiveHead.SetActive(false);
                mainCameraGo.tag = "MainCamera";
                var mainCamera = mainCameraGo.AddComponent<Camera>();

                var host = new GameObject("SpellCaster Host");
                var spellCaster = host.AddComponent(type);
                type.GetField("headTransform", InstanceFields)?.SetValue(spellCaster, inactiveHead.transform);

                type.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(spellCaster, null);

                var resolved = type.GetField("headTransform", InstanceFields)?.GetValue(spellCaster) as Transform;
                Assert.AreEqual(mainCamera.transform, resolved);

                UnityEngine.Object.DestroyImmediate(host);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inactiveHead);
                UnityEngine.Object.DestroyImmediate(mainCameraGo);
            }
        }

        [Test]
        public void SpellCaster_OnValidate_ReplacesMixedRigHandReferencesWithOvrAnchors()
        {
            var type = Type.GetType("ArcaneVR.Spell.SpellCaster, Assembly-CSharp");
            Assert.IsNotNull(type);

            var ovrRig = new GameObject("OVRCameraRig").AddComponent<OVRCameraRig>();
            var trackingSpace = new GameObject("TrackingSpace").transform;
            trackingSpace.SetParent(ovrRig.transform, false);

            var centerEye = new GameObject("CenterEyeAnchor").transform;
            centerEye.SetParent(trackingSpace, false);
            centerEye.gameObject.tag = "MainCamera";
            centerEye.gameObject.AddComponent<Camera>();

            var leftHandAnchor = new GameObject("LeftHandAnchor").transform;
            leftHandAnchor.SetParent(trackingSpace, false);
            var rightHandAnchor = new GameObject("RightHandAnchor").transform;
            rightHandAnchor.SetParent(trackingSpace, false);

            var xrOrigin = new GameObject("XR Origin");
            var xrLeftWrist = new GameObject("L_Wrist").transform;
            xrLeftWrist.SetParent(xrOrigin.transform, false);
            var xrRightWrist = new GameObject("R_Wrist").transform;
            xrRightWrist.SetParent(xrOrigin.transform, false);

            try
            {
                ovrRig.EnsureGameObjectIntegrity();
                var expectedLeftAnchor = ovrRig.leftHandAnchor;
                var expectedRightAnchor = ovrRig.rightHandAnchor;
                Assert.IsNotNull(expectedLeftAnchor);
                Assert.IsNotNull(expectedRightAnchor);

                var host = new GameObject("SpellCaster Host");
                var spellCaster = host.AddComponent(type);
                type.GetField("headTransform", InstanceFields)?.SetValue(spellCaster, centerEye);
                type.GetField("leftHandSpawnPoint", InstanceFields)?.SetValue(spellCaster, xrLeftWrist);
                type.GetField("rightHandSpawnPoint", InstanceFields)?.SetValue(spellCaster, xrRightWrist);

                type.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(spellCaster, null);

                var resolvedLeft = type.GetField("leftHandSpawnPoint", InstanceFields)?.GetValue(spellCaster) as Transform;
                var resolvedRight = type.GetField("rightHandSpawnPoint", InstanceFields)?.GetValue(spellCaster) as Transform;

                Assert.AreEqual(expectedLeftAnchor, resolvedLeft);
                Assert.AreEqual(expectedRightAnchor, resolvedRight);

                UnityEngine.Object.DestroyImmediate(host);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(xrOrigin);
                UnityEngine.Object.DestroyImmediate(ovrRig.gameObject);
            }
        }

        [Test]
        public void SpellCaster_ExposesComboProjectileOverrideSlots()
        {
            var type = Type.GetType("ArcaneVR.Spell.SpellCaster, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("comboFireIceProjectilePrefab", InstanceFields));
            Assert.IsNotNull(type.GetField("comboIceThunderProjectilePrefab", InstanceFields));
            Assert.IsNotNull(type.GetField("comboThunderFireProjectilePrefab", InstanceFields));
            Assert.IsNotNull(type.GetField("comboProjectileLifetime", InstanceFields));
            Assert.IsNotNull(type.GetField("comboProjectileLightIntensity", InstanceFields));
            Assert.IsNotNull(type.GetField("comboProjectileScale", InstanceFields));
            Assert.IsNotNull(type.GetMethod("ResolveComboProjectilePrefab", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(type.GetMethod("CreateComboDebugProjectile", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(type.GetMethod("ApplyComboProjectileVisuals", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(type.GetMethod("ResolveProjectileLifetime", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [Test]
        public void SpellProjectile_ExposesRuntimeLifetimeOverride()
        {
            var type = Type.GetType("ArcaneVR.Spell.SpellProjectile, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetMethod("SetLifetime", BindingFlags.Instance | BindingFlags.Public));
        }

        [Test]
        public void ElementAuraDummy_ProvidesCombinationTimeFocusExemptAura()
        {
            var auraType = Type.GetType("ArcaneVR.Spell.ElementAuraDummy, Assembly-CSharp");
            var casterType = Type.GetType("ArcaneVR.Spell.SpellCaster, Assembly-CSharp");

            Assert.IsNotNull(auraType);
            Assert.IsNotNull(auraType.GetMethod("Create"));
            Assert.IsNotNull(auraType.GetMethod("Configure"));
            Assert.IsNotNull(auraType.GetMethod("ApplyLayerRecursively"));
            Assert.IsNotNull(casterType);
            Assert.IsNotNull(casterType.GetField("auraTimeFocusExemptLayerName", InstanceFields));
            Assert.IsNotNull(casterType.GetField("_combinationAuraRoot", InstanceFields));
            Assert.IsNotNull(casterType.GetMethod("EnsureCombinationAura", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [Test]
        public void SpellCaster_UsesXrGestureDetectorAndElementSpellModules()
        {
            var type = Type.GetType("ArcaneVR.Spell.SpellCaster, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("gestureDetector", InstanceFields));
            Assert.IsNotNull(type.GetField("fireModule", InstanceFields));
            Assert.IsNotNull(type.GetField("iceModule", InstanceFields));
            Assert.IsNotNull(type.GetField("thunderModule", InstanceFields));
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
        public void GestureDetector_ExposesLeftCandidateDiagnosticsForCombineAndBarrier()
        {
            var type = Type.GetType("ArcaneVR.Input.GestureDetector, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetMethod("BuildLeftCandidateDiagnostics", BindingFlags.Static | BindingFlags.NonPublic));
        }

        [Test]
        public void GestureDetector_ExposesLeftThunderSpecificDiagnostics()
        {
            var type = Type.GetType("ArcaneVR.Input.GestureDetector, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetMethod("BuildLeftThunderDiagnostics", BindingFlags.Static | BindingFlags.NonPublic));
        }

        [Test]
        public void GestureDetector_ExposesLeftThunderThresholdAndResolutionPath()
        {
            var type = Type.GetType("ArcaneVR.Input.GestureDetector, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("leftThunderCompletenessThreshold", InstanceFields));
            Assert.IsNotNull(type.GetMethod("TryResolveLeftThunderGesture", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [Test]
        public void GestureDetector_ExposesSharedIcePalmUpGuardsForBothHands()
        {
            var type = Type.GetType("ArcaneVR.Input.GestureDetector, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("rightIcePalmUpDotThreshold", InstanceFields));
            Assert.IsNotNull(type.GetField("leftIcePalmUpDotThreshold", InstanceFields));
            Assert.IsNotNull(type.GetField("invertRightIcePalmDirection", InstanceFields));
            Assert.IsNotNull(type.GetField("invertLeftIcePalmDirection", InstanceFields));
            Assert.IsNotNull(type.GetMethod("IsRightPalmFacingUp", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(type.GetMethod("IsLeftPalmFacingUp", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(type.GetMethod("IsPalmFacingUp", BindingFlags.Static | BindingFlags.NonPublic));
        }

        [Test]
        public void CombinationChecker_ExposesLeftThunderDeclarationDiagnostics()
        {
            var type = Type.GetType("ArcaneVR.Input.CombinationChecker, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetMethod("BuildLeftThunderDeclarationDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [Test]
        public void CombinationChecker_DefaultsLeftElementDebugBypassOffAndLogsElementState()
        {
            var type = Type.GetType("ArcaneVR.Input.CombinationChecker, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("enableComboElementStateLogs", InstanceFields));
            Assert.IsNotNull(type.GetField("lastComboElementStateLogKey", InstanceFields));
            Assert.IsNotNull(type.GetMethod("LogComboElementStateIfChanged", BindingFlags.Instance | BindingFlags.NonPublic));

            var owner = new GameObject("CombinationChecker Defaults Test");
            try
            {
                var checker = owner.AddComponent(type);
                var debugBypass = type.GetField("allowLeftElementDeclarationOutsideFocusForDebug", InstanceFields);
                Assert.IsNotNull(debugBypass);
                Assert.IsFalse((bool)debugBypass.GetValue(checker));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void CombinationChecker_PullSuppressionYieldsToCombinationFocus()
        {
            var type = Type.GetType("ArcaneVR.Input.CombinationChecker, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetMethod("IsLeftPullActive", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(type.GetMethod("IsCombinationFocusActive", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(type.GetMethod("RefreshComboCandidate", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [Test]
        public void IceSpellModule_TracksProjectileLifetimeForAuraPersistence()
        {
            var type = Type.GetType("ArcaneVR.Spell.IceSpellModule, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("_activeProjectileCount", InstanceFields));
            Assert.IsNotNull(type.GetProperty("HasActiveProjectile"));
            Assert.IsNotNull(type.GetMethod("RefreshAuraVisibility", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(type.GetMethod("TrackProjectileLifetime", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNotNull(type.GetMethod("NotifyProjectileDestroyed", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [Test]
        public void ElementSpellModules_ExposeSwapFriendlySfxFields()
        {
            var fireType = Type.GetType("ArcaneVR.Spell.FireSpellModule, Assembly-CSharp");
            var iceType = Type.GetType("ArcaneVR.Spell.IceSpellModule, Assembly-CSharp");
            var thunderType = Type.GetType("ArcaneVR.Spell.ThunderSpellModule, Assembly-CSharp");

            Assert.IsNotNull(fireType);
            Assert.IsNotNull(fireType.GetField("armSfxClip", InstanceFields));
            Assert.IsNotNull(fireType.GetField("castSfxClip", InstanceFields));
            Assert.IsNotNull(fireType.GetMethod("PlayElementSfx", BindingFlags.Instance | BindingFlags.NonPublic));

            Assert.IsNotNull(iceType);
            Assert.IsNotNull(iceType.GetField("armSfxClip", InstanceFields));
            Assert.IsNotNull(iceType.GetField("castSfxClip", InstanceFields));
            Assert.IsNotNull(iceType.GetMethod("PlayElementSfx", BindingFlags.Instance | BindingFlags.NonPublic));

            Assert.IsNotNull(thunderType);
            Assert.IsNotNull(thunderType.GetField("armSfxClip", InstanceFields));
            Assert.IsNotNull(thunderType.GetField("castSfxClip", InstanceFields));
            Assert.IsNotNull(thunderType.GetMethod("PlayElementSfx", BindingFlags.Instance | BindingFlags.NonPublic));
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

        [Test]
        public void FeedbackManager_UsesInspectorReferencesInsteadOfRuntimeDiscovery()
        {
            var type = Type.GetType("ArcaneVR.UI.FeedbackManager, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic));
            AssertSourceDoesNotContain(
                "Assets/Scripts/UI/FeedbackManager.cs",
                "FindAnyObjectByType<CombatManager>",
                "FindAnyObjectByType<SpellCaster>",
                "FindAnyObjectByType<VoiceRecognizer>",
                "FindAnyObjectByType<GolemCombatTarget>",
                "FindAnyObjectByType<BossAI>");
        }

        [Test]
        public void FeedbackManager_OnValidate_ReplacesInactivePlayerCamera()
        {
            var type = Type.GetType("ArcaneVR.UI.FeedbackManager, Assembly-CSharp");
            Assert.IsNotNull(type);

            var inactiveCameraGo = new GameObject("Inactive Feedback Camera");
            var mainCameraGo = new GameObject("Active Main Camera");
            try
            {
                var inactiveCamera = inactiveCameraGo.AddComponent<Camera>();
                inactiveCameraGo.SetActive(false);
                mainCameraGo.tag = "MainCamera";
                var mainCamera = mainCameraGo.AddComponent<Camera>();

                var host = new GameObject("FeedbackManager Host");
                var feedback = host.AddComponent(type);
                type.GetField("playerCamera", InstanceFields)?.SetValue(feedback, inactiveCamera);

                type.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(feedback, null);

                var resolved = type.GetField("playerCamera", InstanceFields)?.GetValue(feedback) as Camera;
                Assert.AreEqual(mainCamera, resolved);

                UnityEngine.Object.DestroyImmediate(host);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inactiveCameraGo);
                UnityEngine.Object.DestroyImmediate(mainCameraGo);
            }
        }

        [Test]
        public void CombinationChecker_UsesInspectorReferencesInsteadOfRuntimeDiscovery()
        {
            var type = Type.GetType("ArcaneVR.Input.CombinationChecker, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic));
            AssertSourceDoesNotContain(
                "Assets/Scripts/Input/CombinationChecker.cs",
                "FindAnyObjectByType<GestureDetector>",
                "FindAnyObjectByType<GrimoireManager>",
                "FindAnyObjectByType<HandPullMovementController>",
                "FindAnyObjectByType<ArcaneActionModeController>",
                "FindAnyObjectByType<CombinationFocusModeController>");
        }

        [Test]
        public void MovementController_UsesInspectorReferencesInsteadOfRuntimeDiscovery()
        {
            var type = Type.GetType("ArcaneVR.Input.MovementController, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic));
            AssertSourceDoesNotContain(
                "Assets/Scripts/Input/MovementController.cs",
                "FindAnyObjectByType<GestureDetector>",
                "FindAnyObjectByType<GestureEventRouter>",
                "FindAnyObjectByType<ConstraintController>");
        }

        [Test]
        public void GrimoireManager_UsesInspectorReferencesInsteadOfRuntimeDiscovery()
        {
            var type = Type.GetType("ArcaneVR.UI.GrimoireManager, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic));
            AssertSourceDoesNotContain(
                "Assets/Scripts/UI/GrimoireManager.cs",
                "FindAnyObjectByType<GestureDetector>",
                "GameObject.Find(\"L_Wrist\")",
                "FindObjectsByType<SpellCaster>",
                "FindObjectsByType<Canvas>",
                "FindBestOvrHand(",
                "GameObject.Find(\"PlayerSpawnPoint\")");
        }

        [Test]
        public void GrimoireManager_OnValidate_ReplacesInactivePlayerCamera()
        {
            var type = Type.GetType("ArcaneVR.UI.GrimoireManager, Assembly-CSharp");
            Assert.IsNotNull(type);

            var inactiveHead = new GameObject("Inactive Grimoire Head");
            var mainCameraGo = new GameObject("Active Main Camera");
            try
            {
                inactiveHead.SetActive(false);
                mainCameraGo.tag = "MainCamera";
                var mainCamera = mainCameraGo.AddComponent<Camera>();

                var host = new GameObject("GrimoireManager Host");
                var manager = host.AddComponent(type);
                type.GetField("playerCamera", InstanceFields)?.SetValue(manager, inactiveHead.transform);

                type.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(manager, null);

                var resolved = type.GetField("playerCamera", InstanceFields)?.GetValue(manager) as Transform;
                Assert.AreEqual(mainCamera.transform, resolved);

                UnityEngine.Object.DestroyImmediate(host);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inactiveHead);
                UnityEngine.Object.DestroyImmediate(mainCameraGo);
            }
        }

        [Test]
        public void GrimoireManager_OnValidate_ReplacesMixedRigBookAnchorWithOvrAnchor()
        {
            var type = Type.GetType("ArcaneVR.UI.GrimoireManager, Assembly-CSharp");
            Assert.IsNotNull(type);

            var ovrRig = new GameObject("OVRCameraRig").AddComponent<OVRCameraRig>();
            var trackingSpace = new GameObject("TrackingSpace").transform;
            trackingSpace.SetParent(ovrRig.transform, false);

            var centerEye = new GameObject("CenterEyeAnchor").transform;
            centerEye.SetParent(trackingSpace, false);
            centerEye.gameObject.tag = "MainCamera";
            centerEye.gameObject.AddComponent<Camera>();

            var leftHandAnchor = new GameObject("LeftHandAnchor").transform;
            leftHandAnchor.SetParent(trackingSpace, false);

            var xrOrigin = new GameObject("XR Origin");
            var xrLeftWrist = new GameObject("L_Wrist").transform;
            xrLeftWrist.SetParent(xrOrigin.transform, false);

            try
            {
                ovrRig.EnsureGameObjectIntegrity();
                var expectedLeftAnchor = ovrRig.leftHandAnchor;
                Assert.IsNotNull(expectedLeftAnchor);

                var host = new GameObject("GrimoireManager Host");
                var manager = host.AddComponent(type);
                type.GetField("playerCamera", InstanceFields)?.SetValue(manager, centerEye);
                type.GetField("leftHandBookAnchor", InstanceFields)?.SetValue(manager, xrLeftWrist);

                type.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(manager, null);

                var resolved = type.GetField("leftHandBookAnchor", InstanceFields)?.GetValue(manager) as Transform;
                Assert.AreEqual(expectedLeftAnchor, resolved);

                UnityEngine.Object.DestroyImmediate(host);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(xrOrigin);
                UnityEngine.Object.DestroyImmediate(ovrRig.gameObject);
            }
        }

        static void AssertSourceDoesNotContain(string assetRelativePath, params string[] forbiddenSnippets)
        {
            var projectRoot = Directory.GetParent(Application.dataPath);
            Assert.IsNotNull(projectRoot);

            var fullPath = Path.Combine(projectRoot.FullName, assetRelativePath);
            var source = File.ReadAllText(fullPath);
            foreach (var snippet in forbiddenSnippets)
                StringAssert.DoesNotContain(snippet, source, $"{assetRelativePath} should not contain runtime lookup snippet: {snippet}");
        }
    }
}
