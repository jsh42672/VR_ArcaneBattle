using ArcaneVR.Boss;
using ArcaneVR.Combat;
using ArcaneVR.Input;
using ArcaneVR.Spell;
using ArcaneVR.UI;
using NUnit.Framework;
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
        Assert.That(rigRoot.GetComponentInChildren<MovementController>(true), Is.Not.Null);
        Assert.That(rigRoot.GetComponentInChildren<ArcaneVR.Input.DodgeDetector>(true), Is.Not.Null);
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
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var rigRoot = GameObject.Find("ArcanePlayerRig");
        var legacyXrOrigin = GameObject.Find("XR Origin");
        Assert.That(rigRoot, Is.Not.Null);
        Assert.That(legacyXrOrigin == null || !legacyXrOrigin.activeInHierarchy, Is.True,
            "Legacy XR Origin must not remain the active runtime authority after alignment.");
    }
}
