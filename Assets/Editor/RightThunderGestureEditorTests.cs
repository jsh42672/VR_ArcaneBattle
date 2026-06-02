using System;
using NUnit.Framework;

namespace ArcaneVR.EditorTests
{
    public class RightThunderGestureEditorTests
    {
        [Test]
        public void RightThunderGesture_ExposesExpectedPrototypeDefaults()
        {
            var type = Type.GetType("ArcaneVR.Input.RightThunderGesture, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("defaultRangeMeters"));
            Assert.IsNotNull(type.GetField("defaultDamage"));
            Assert.IsNotNull(type.GetField("defaultChargeGraceSeconds"));
            Assert.IsNotNull(type.GetField("defaultContinuousFireSeconds"));
            Assert.IsNotNull(type.GetField("defaultShootPoseGraceSeconds"));
            Assert.IsNotNull(type.GetField("defaultLaserDownAngleDegrees"));
            Assert.IsNotNull(type.GetMethod("CreateDefaultHitData"));
        }
    }
}
