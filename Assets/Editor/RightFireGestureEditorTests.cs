using System;
using System.Reflection;
using NUnit.Framework;

namespace ArcaneVR.EditorTests
{
    public class RightFireGestureEditorTests
    {
        [Test]
        public void RightFireGesture_ExposesProjectileTuningFields()
        {
            var type = Type.GetType("ArcaneVR.Input.RightFireGesture, Assembly-CSharp");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("projectileSpeed", flags));
            Assert.IsNotNull(type.GetField("projectileScale", flags));
            Assert.IsNotNull(type.GetField("projectileEulerOffset", flags));
            Assert.IsNotNull(type.GetField("explosionPrefab", flags));
            Assert.IsNotNull(type.GetField("explosionScale", flags));
            Assert.IsNotNull(type.GetField("explosionLifetime", flags));
            Assert.IsNotNull(type.GetField("explosionMaterialOverride", flags));
        }

        [Test]
        public void FireballProjectile_CanReceiveGestureLaunchTuning()
        {
            var type = Type.GetType("FireballProjectile, Assembly-CSharp");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetMethod("ConfigureLaunch", flags));
            Assert.IsNotNull(type.GetMethod("ConfigureImpact", flags));
        }
    }
}
