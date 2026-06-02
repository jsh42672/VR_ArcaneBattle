using System;
using NUnit.Framework;

namespace ArcaneVR.EditorTests
{
    public class ArcaneTimeFocusControllerEditorTests
    {
        [Test]
        public void ArcaneTimeFocusController_ExposesReusableFocusDefaults()
        {
            var type = Type.GetType("ArcaneVR.Input.ArcaneTimeFocusController, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("defaultSlowTimeScale"));
            Assert.IsNotNull(type.GetField("defaultGrayscaleSaturation"));
            Assert.IsNotNull(type.GetMethod("RequestFocus"));
            Assert.IsNotNull(type.GetMethod("ReleaseFocus"));
            Assert.IsNotNull(type.GetMethod("ClearAllFocus"));
        }

        [Test]
        public void GrimoireFocusModeController_ExposesFocusState()
        {
            var type = Type.GetType("ArcaneVR.Input.GrimoireFocusModeController, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetProperty("IsFocusApplied"));
            Assert.IsNotNull(type.GetMethod("SyncFocusState"));
        }
    }
}
