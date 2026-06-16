using System;
using System.Reflection;
using NUnit.Framework;

namespace ArcaneVR.EditorTests
{
    public class LeftAuraArchitectureEditorTests
    {
        const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags InstanceMethods = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void SpellCaster_ExposesLeftAuraManagerPath()
        {
            var type = Type.GetType("ArcaneVR.Spell.SpellCaster, Assembly-CSharp");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetField("leftElementAuraManager", InstanceFields));
            Assert.IsNotNull(type.GetField("_runtimeLeftAuraManager", InstanceFields));
            Assert.IsNotNull(type.GetMethod("ResolveLeftAuraManager", InstanceMethods));
            Assert.IsNotNull(type.GetMethod("ShowLeftElementAura", InstanceMethods));
            Assert.IsNotNull(type.GetMethod("HideLeftElementAura", InstanceMethods));
        }
    }
}
