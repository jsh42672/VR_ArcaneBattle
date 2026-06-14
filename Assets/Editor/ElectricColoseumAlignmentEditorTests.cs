using ArcaneVR.Boss;
using ArcaneVR.Combat;
using ArcaneVR.Input;
using ArcaneVR.Spell;
using ArcaneVR.UI;
using NUnit.Framework;
using System.Reflection;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ElectricColoseumAlignmentEditorTests
{
    private const string ScenePath = "Assets/Scenes/ElectricColoseum.unity";

    [Test]
    public void ElectricColoseum_ContainsModernRuntimeAuthority()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Assert.That(scene.IsValid() && scene.isLoaded, Is.True);

        var rigRoot = GameObject.Find("ArcanePlayerRig");
        Assert.That(rigRoot, Is.Not.Null, "Aligned scene must contain ArcanePlayerRig authority.");
        Assert.That(rigRoot.GetComponentInChildren<SpellCaster>(true), Is.Not.Null);
        Assert.That(rigRoot.GetComponentInChildren<CombinationChecker>(true), Is.Not.Null);
        Assert.That(rigRoot.GetComponentInChildren<CombatManager>(true), Is.Not.Null);
        Assert.That(rigRoot.GetComponentInChildren<GestureDetector>(true), Is.Not.Null);
        var hasMovementController =
            rigRoot.GetComponentInChildren<MovementController>(true) != null ||
            rigRoot.GetComponentInChildren<HandPullMovementController>(true) != null;
        Assert.That(hasMovementController, Is.True, "Aligned scene must expose the latest movement-control authority.");

        Assert.That(rigRoot.GetComponentInChildren<ArcaneVR.Combat.DodgeDetector>(true), Is.Not.Null);
        Assert.That(rigRoot.GetComponentInChildren<VoiceRecognizer>(true), Is.Not.Null);
        Assert.That(rigRoot.GetComponentInChildren<GrimoireManager>(true), Is.Not.Null);
        Assert.That(rigRoot.GetComponentInChildren<FeedbackManager>(true), Is.Not.Null);
    }

    [Test]
    public void ElectricColoseum_KeepsBossArenaAndRuntimeBinder()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Assert.That(GameObject.Find("BattleManager"), Is.Not.Null);
        Assert.That(GameObject.Find("Portal_Exit"), Is.Not.Null);
        Assert.That(GameObject.Find("ThunderGolemn"), Is.Not.Null);
        Assert.That(Object.FindAnyObjectByType<GolemCombatTarget>(), Is.Not.Null);
        Assert.That(Object.FindAnyObjectByType<BossBattleRuntimeBinder>(), Is.Not.Null);
    }

    [Test]
    public void ElectricColoseum_DoesNotLeaveLegacyXrOriginAsPrimaryAuthority()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var rigRoot = GameObject.Find("ArcanePlayerRig");
        GameObject legacyXrOrigin = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root != null && root.name == "XR Origin")
            {
                legacyXrOrigin = root;
                break;
            }
        }

        Assert.That(rigRoot, Is.Not.Null);
        Assert.That(legacyXrOrigin == null || !legacyXrOrigin.activeInHierarchy, Is.True,
            "Legacy XR Origin must not remain the active runtime authority after alignment.");
    }

    [Test]
    public void ElectricColoseumAligner_CreatesArcanePlayerRigAndDisablesLegacyAuthority()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        ArcaneVR.Editor.ElectricColoseumSceneAligner.AlignScene(scene);
        EditorSceneManager.SaveScene(scene);

        var rigRoot = GameObject.Find("ArcanePlayerRig");
        GameObject legacyXrOrigin = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root != null && root.name == "XR Origin")
            {
                legacyXrOrigin = root;
                break;
            }
        }

        Assert.That(rigRoot, Is.Not.Null);
        Assert.That(legacyXrOrigin == null || !legacyXrOrigin.activeSelf, Is.True);
    }

    [Test]
    public void ElectricColoseum_GrimoireReferencesUseAlignedRig()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var grimoire = Object.FindAnyObjectByType<GrimoireManager>();
        Assert.That(grimoire, Is.Not.Null);

        var playerCameraField = typeof(GrimoireManager).GetField("playerCamera", BindingFlags.NonPublic | BindingFlags.Instance);
        var leftAnchorField = typeof(GrimoireManager).GetField("leftHandBookAnchor", BindingFlags.NonPublic | BindingFlags.Instance);
        var playerCamera = playerCameraField?.GetValue(grimoire) as Transform;
        var leftAnchor = leftAnchorField?.GetValue(grimoire) as Transform;

        Assert.That(playerCamera, Is.Not.Null);
        Assert.That(leftAnchor, Is.Not.Null);
        Assert.That(playerCamera.name, Is.EqualTo("CenterEyeAnchor"));
        Assert.That(leftAnchor.name, Is.EqualTo("LeftHandAnchor"));
    }

    [Test]
    public void ElectricColoseum_WiresGestureConsumersToModernRig()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var gestureDetector = Object.FindAnyObjectByType<GestureDetector>();
        var grimoire = Object.FindAnyObjectByType<GrimoireManager>();
        var combinationChecker = Object.FindAnyObjectByType<CombinationChecker>();
        var spellCaster = Object.FindAnyObjectByType<SpellCaster>();
        var gestureRouter = Object.FindAnyObjectByType<GestureEventRouter>();

        Assert.That(gestureDetector, Is.Not.Null);
        Assert.That(grimoire, Is.Not.Null);
        Assert.That(combinationChecker, Is.Not.Null);
        Assert.That(spellCaster, Is.Not.Null);
        Assert.That(gestureRouter, Is.Not.Null);

        var detectorField = typeof(GrimoireManager).GetField("gestureDetector", BindingFlags.NonPublic | BindingFlags.Instance);
        var routerField = typeof(GrimoireManager).GetField("gestureRouter", BindingFlags.NonPublic | BindingFlags.Instance);
        var grimoireDetector = detectorField?.GetValue(grimoire) as GestureDetector;
        var grimoireRouter = routerField?.GetValue(grimoire) as GestureEventRouter;

        var comboDetectorField = typeof(CombinationChecker).GetField("gestureDetector", BindingFlags.NonPublic | BindingFlags.Instance);
        var comboGrimoireField = typeof(CombinationChecker).GetField("grimoireManager", BindingFlags.NonPublic | BindingFlags.Instance);
        var comboDetector = comboDetectorField?.GetValue(combinationChecker) as GestureDetector;
        var comboGrimoire = comboGrimoireField?.GetValue(combinationChecker) as GrimoireManager;

        var casterDetectorField = typeof(SpellCaster).GetField("gestureDetector", BindingFlags.NonPublic | BindingFlags.Instance);
        var casterGrimoireField = typeof(SpellCaster).GetField("grimoireManager", BindingFlags.NonPublic | BindingFlags.Instance);
        var casterDetector = casterDetectorField?.GetValue(spellCaster) as GestureDetector;
        var casterGrimoire = casterGrimoireField?.GetValue(spellCaster) as GrimoireManager;

        var detectorLeftEventsField = typeof(GestureDetector).GetField("leftHandTrackingEvents", BindingFlags.NonPublic | BindingFlags.Instance);
        var detectorRightEventsField = typeof(GestureDetector).GetField("rightHandTrackingEvents", BindingFlags.NonPublic | BindingFlags.Instance);
        var detectorLeftEvents = detectorLeftEventsField?.GetValue(gestureDetector) as UnityEngine.XR.Hands.XRHandTrackingEvents;
        var detectorRightEvents = detectorRightEventsField?.GetValue(gestureDetector) as UnityEngine.XR.Hands.XRHandTrackingEvents;
        var detectorOverlayField = typeof(GestureDetector).GetField("showPlayModeDebugOverlay", BindingFlags.NonPublic | BindingFlags.Instance);
        var detectorLogField = typeof(GestureDetector).GetField("showDebugLog", BindingFlags.NonPublic | BindingFlags.Instance);
        var detectorOverlay = detectorOverlayField != null && (bool)detectorOverlayField.GetValue(gestureDetector);
        var detectorLog = detectorLogField != null && (bool)detectorLogField.GetValue(gestureDetector);

        Assert.That(grimoireDetector, Is.EqualTo(gestureDetector));
        Assert.That(grimoireRouter, Is.EqualTo(gestureRouter));
        Assert.That(comboDetector, Is.EqualTo(gestureDetector));
        Assert.That(comboGrimoire, Is.EqualTo(grimoire));
        Assert.That(casterDetector, Is.EqualTo(gestureDetector));
        Assert.That(casterGrimoire, Is.EqualTo(grimoire));
        Assert.That(detectorLeftEvents, Is.Not.Null);
        Assert.That(detectorRightEvents, Is.Not.Null);
        Assert.That(detectorLeftEvents.name, Is.EqualTo("Left Hand Tracking"));
        Assert.That(detectorRightEvents.name, Is.EqualTo("Right Hand Tracking"));
        Assert.That(detectorOverlay, Is.True);
        Assert.That(detectorLog, Is.True);
    }

    [Test]
    public void ElectricColoseum_DisablesStandaloneLegacyGrimoireSystem()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject standaloneGrimoireSystem = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root != null && root.name == "GrimoireSystem")
            {
                standaloneGrimoireSystem = root;
                break;
            }
        }

        if (standaloneGrimoireSystem != null)
            Assert.That(standaloneGrimoireSystem.activeSelf, Is.False);
    }

    [Test]
    public void ElectricColoseum_ContainsMainStyleHandTrackingHierarchy()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var rigRoot = GameObject.Find("ArcanePlayerRig");
        Assert.That(rigRoot, Is.Not.Null);

        var inputCollection = rigRoot.transform.Find("00_InputCollection_PlayerXR");
        var xrOrigin = inputCollection != null ? inputCollection.Find("XR Origin") : null;
        var leftHandTracking = xrOrigin != null ? xrOrigin.Find("Left Hand Tracking") : null;
        var rightHandTracking = xrOrigin != null ? xrOrigin.Find("Right Hand Tracking") : null;

        Assert.That(inputCollection, Is.Not.Null);
        Assert.That(xrOrigin, Is.Not.Null);
        Assert.That(leftHandTracking, Is.Not.Null);
        Assert.That(rightHandTracking, Is.Not.Null);
    }

    [Test]
    public void ElectricColoseum_BossFlowCanResolveCombatTargetAndFeedback()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Assert.That(Object.FindAnyObjectByType<GolemCombatTarget>(), Is.Not.Null);
        Assert.That(Object.FindAnyObjectByType<FeedbackManager>(), Is.Not.Null);
        Assert.That(Object.FindAnyObjectByType<CombatManager>(), Is.Not.Null);
    }

    [Test]
    public void AndroidXrManager_AutomaticallyLoadsAndRuns()
    {
        const string xrSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        var contents = File.ReadAllText(xrSettingsPath);

        StringAssert.Contains("m_Name: Android Providers", contents);
        StringAssert.Contains("m_AutomaticLoading: 1", contents);
        StringAssert.Contains("m_AutomaticRunning: 1", contents);
    }
}
