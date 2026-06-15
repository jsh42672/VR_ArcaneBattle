using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneVR.EditorTests
{
    public class GrimoirePageTurnerEditorTests
    {
        private const string TurnerTypeName = "CodexGenerated.GrimoirePages.GrimoirePageTurner, Assembly-CSharp";

        [Test]
        public void SetSpread_AssignsDifferentFrontAndBackContentForVisiblePages()
        {
            var fixture = new PageTurnerFixture();

            try
            {
                fixture.AssignAllRenderers();
                fixture.AssignPageMaterials(6);
                fixture.InvokeSetSpread(1);

                AssertRendererMatches(fixture.LeftFrontRenderer, fixture.PageMaterials[2]);
                AssertRendererMatches(fixture.LeftBackRenderer, fixture.PageMaterials[1]);
                AssertRendererMatches(fixture.RightFrontRenderer, fixture.PageMaterials[3]);
                AssertRendererMatches(fixture.RightBackRenderer, fixture.PageMaterials[4]);
                AssertRendererMatches(fixture.TurningFrontRenderer, fixture.PageMaterials[3]);
                AssertRendererMatches(fixture.TurningBackRenderer, fixture.PageMaterials[4]);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void BeginManualTurn_UsesNextPageOnBackOfForwardTurningSheet()
        {
            var fixture = new PageTurnerFixture();

            try
            {
                fixture.AssignAllRenderers();
                fixture.AssignPageMaterials(6);
                fixture.InvokeSetSpread(0);

                bool began = fixture.InvokeBeginManualTurn(true);

                Assert.IsTrue(began);
                AssertRendererMatches(fixture.TurningFrontRenderer, fixture.PageMaterials[1]);
                AssertRendererMatches(fixture.TurningBackRenderer, fixture.PageMaterials[2]);
                Assert.IsFalse(fixture.RightFrontRenderer.gameObject.activeSelf);
                Assert.IsFalse(fixture.RightBackRenderer.gameObject.activeSelf);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void SetManualTurnAngle_RevealsUnderlyingNextPageOnlyAfterActualDragProgress()
        {
            var fixture = new PageTurnerFixture();

            try
            {
                fixture.AssignAllRenderers();
                fixture.AssignPageMaterials(6);
                fixture.InvokeSetSpread(0);

                bool began = fixture.InvokeBeginManualTurn(true);

                Assert.IsTrue(began);
                Assert.IsFalse(fixture.RightFrontRenderer.gameObject.activeSelf);

                fixture.InvokeSetManualTurnAngle(2f);
                Assert.IsFalse(fixture.RightFrontRenderer.gameObject.activeSelf);

                fixture.InvokeSetManualTurnAngle(12f);
                Assert.IsTrue(fixture.RightFrontRenderer.gameObject.activeSelf);
                Assert.IsTrue(fixture.RightBackRenderer.gameObject.activeSelf);
                AssertRendererMatches(fixture.RightFrontRenderer, fixture.PageMaterials[3]);
                AssertRendererMatches(fixture.RightBackRenderer, fixture.PageMaterials[4]);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static void AssertRendererMatches(Renderer renderer, Material expected)
        {
            Assert.IsNotNull(renderer);
            Assert.IsNotNull(renderer.sharedMaterial);
            Assert.AreEqual(expected.color, renderer.sharedMaterial.color);
        }

        private sealed class PageTurnerFixture : IDisposable
        {
            private readonly Type turnerType;
            private readonly GameObject root;
            private readonly object turner;

            public PageTurnerFixture()
            {
                turnerType = Type.GetType(TurnerTypeName);
                Assert.IsNotNull(turnerType, "GrimoirePageTurner type not found.");

                root = new GameObject("GrimoirePageTurnerEditorTests_Root");
                turner = root.AddComponent(turnerType);

                LeftFrontRenderer = CreateRenderer("LeftFront", root.transform);
                LeftBackRenderer = CreateRenderer("LeftBack", root.transform);
                RightFrontRenderer = CreateRenderer("RightFront", root.transform);
                RightBackRenderer = CreateRenderer("RightBack", root.transform);

                var pivot = new GameObject("TurningPivot");
                pivot.transform.SetParent(root.transform, false);
                TurningPivot = pivot.transform;
                TurningFrontRenderer = CreateRenderer("TurningFront", TurningPivot);
                TurningBackRenderer = CreateRenderer("TurningBack", TurningPivot);
            }

            public Renderer LeftFrontRenderer { get; }
            public Renderer LeftBackRenderer { get; }
            public Renderer RightFrontRenderer { get; }
            public Renderer RightBackRenderer { get; }
            public Transform TurningPivot { get; }
            public Renderer TurningFrontRenderer { get; }
            public Renderer TurningBackRenderer { get; }
            public Material[] PageMaterials { get; private set; }

            public void AssignAllRenderers()
            {
                SetField("leftPageRenderer", LeftFrontRenderer);
                SetField("leftPageBackRenderer", LeftBackRenderer);
                SetField("rightPageRenderer", RightFrontRenderer);
                SetField("rightPageBackRenderer", RightBackRenderer);
                SetField("turningPagePivot", TurningPivot);
                SetField("turningPageRenderer", TurningFrontRenderer);
                SetField("turningPageBackRenderer", TurningBackRenderer);
                SetField("turningPageRenderers", Array.Empty<Renderer>());
                SetField("turningPageStrips", Array.Empty<Transform>());
            }

            public void AssignPageMaterials(int count)
            {
                PageMaterials = new Material[count];
                for (int i = 0; i < count; i++)
                {
                    var material = new Material(Shader.Find("Sprites/Default"));
                    material.name = $"PageMaterial_{i}";
                    material.color = Color.HSVToRGB(i / (float)count, 0.9f, 0.9f);
                    PageMaterials[i] = material;
                }

                SetField("pageMaterials", PageMaterials);
            }

            public void InvokeSetSpread(int spreadIndex)
            {
                MethodInfo method = turnerType.GetMethod("SetSpread");
                Assert.IsNotNull(method);
                method.Invoke(turner, new object[] { spreadIndex });
            }

            public bool InvokeBeginManualTurn(bool forward)
            {
                MethodInfo method = turnerType.GetMethod("BeginManualTurn");
                Assert.IsNotNull(method);
                object result = method.Invoke(turner, new object[] { forward });
                return result is bool boolResult && boolResult;
            }

            public void InvokeSetManualTurnAngle(float angle)
            {
                MethodInfo method = turnerType.GetMethod("SetManualTurnAngle");
                Assert.IsNotNull(method);
                method.Invoke(turner, new object[] { angle });
            }

            private void SetField(string fieldName, object value)
            {
                FieldInfo field = turnerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(field, $"Expected private field '{fieldName}' to exist.");
                field.SetValue(turner, value);
            }

            private static Renderer CreateRenderer(string name, Transform parent)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = name;
                go.transform.SetParent(parent, false);
                var collider = go.GetComponent<Collider>();
                if (collider != null)
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }

                return go.GetComponent<Renderer>();
            }

            public void Dispose()
            {
                if (PageMaterials != null)
                {
                    foreach (Material material in PageMaterials)
                    {
                        if (material != null)
                        {
                            UnityEngine.Object.DestroyImmediate(material);
                        }
                    }
                }

                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
    }
}
