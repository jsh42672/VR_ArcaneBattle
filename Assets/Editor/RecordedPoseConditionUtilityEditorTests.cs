using ArcaneVR.Input;
using NUnit.Framework;
using UnityEngine;

public class RecordedPoseConditionUtilityEditorTests
{
    [Test]
    public void GetShortPoseName_CompactsRecordedPoseNames()
    {
        Assert.AreEqual("left_both_fwd", RecordedPoseConditionUtility.GetShortPoseName("Left_BothForward_OpenPalm"));
        Assert.AreEqual("right_both_fwd", RecordedPoseConditionUtility.GetShortPoseName("Right_BothForward_OpenPalm"));
        Assert.AreEqual("left_palm_together", RecordedPoseConditionUtility.GetShortPoseName("Left_PalmTogether_OpenPalm"));
        Assert.AreEqual("right_palm_together", RecordedPoseConditionUtility.GetShortPoseName("Right_PalmTogether_OpenPalm"));
        Assert.AreEqual("left_fist", RecordedPoseConditionUtility.GetShortPoseName("Left_Fist "));
    }

    [Test]
    public void AreBothHandsForward_RequiresBothTrackedHandsPastForwardThreshold()
    {
        var sample = new RecordedPoseHandSample(
            true,
            true,
            new Vector3(-0.2f, 1.2f, 0.45f),
            new Vector3(0.2f, 1.2f, 0.44f),
            new Vector3(0f, 1.2f, 0f),
            Vector3.forward);

        Assert.IsTrue(RecordedPoseConditionUtility.AreBothHandsForward(sample, 0.35f));
    }

    [Test]
    public void AreBothHandsForward_RejectsWhenEitherHandIsNotForward()
    {
        var sample = new RecordedPoseHandSample(
            true,
            true,
            new Vector3(-0.2f, 1.2f, 0.45f),
            new Vector3(0.2f, 1.2f, 0.1f),
            new Vector3(0f, 1.2f, 0f),
            Vector3.forward);

        Assert.IsFalse(RecordedPoseConditionUtility.AreBothHandsForward(sample, 0.35f));
    }

    [Test]
    public void ArePalmsTogether_RequiresBothTrackedHandsWithinDistance()
    {
        var sample = new RecordedPoseHandSample(
            true,
            true,
            new Vector3(-0.04f, 1.2f, 0.35f),
            new Vector3(0.04f, 1.2f, 0.35f),
            Vector3.zero,
            Vector3.forward);

        Assert.IsTrue(RecordedPoseConditionUtility.ArePalmsTogether(sample, 0.1f));
    }

    [Test]
    public void ShouldBlockSingleRightOpenPalm_BlocksOnlyNormalRightOpenPalmDuringBothForward()
    {
        Assert.IsTrue(RecordedPoseConditionUtility.ShouldBlockSingleRightOpenPalm(
            "Right_Fire_OpenPalm",
            MetaHandPoseGestureBridge.Handedness.Right,
            PoseType.OpenPalm,
            true));

        Assert.IsFalse(RecordedPoseConditionUtility.ShouldBlockSingleRightOpenPalm(
            "Right_BothForward_OpenPalm",
            MetaHandPoseGestureBridge.Handedness.Right,
            PoseType.OpenPalm,
            true));

        Assert.IsFalse(RecordedPoseConditionUtility.ShouldBlockSingleRightOpenPalm(
            "Left_Grimoire_OpenPalm",
            MetaHandPoseGestureBridge.Handedness.Left,
            PoseType.OpenPalm,
            true));
    }
}
