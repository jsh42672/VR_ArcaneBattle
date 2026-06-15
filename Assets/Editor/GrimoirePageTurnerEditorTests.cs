using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneVR.EditorTests
{
    public class GrimoirePageTurnerEditorTests
    {
        private const string TurnerTypeName = "CodexGenerated.GrimoirePages.GrimoirePageTurner, Assembly-CSharp";

        [Test]
        public void SetSpread_CreatesAllTurnableSheetsUpFront()
        {
            var fixture = new PageTurnerFixture();

            try
            {
                fixture.AssignAllRenderers();
                fixture.AssignPageMaterials(6);
                fixture.InvokeSetSpread(0);

                Assert.AreEqual(3, fixture.GetRuntimeSheetCount());
                fixture.AssertRuntimeSheetPages(0, 1, 2);
                fixture.AssertRuntimeSheetPages(1, 3, 4);
                fixture.AssertRuntimeSheetPages(2, 5, 6);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void SetSpread_ShowsReadSheetOnLeftAndUnreadSheetOnRight()
        {
            var fixture = new PageTurnerFixture();

            try
            {
                fixture.AssignAllRenderers();
                fixture.AssignPageMaterials(6);
                fixture.InvokeSetSpread(1);

                Assert.IsFalse(fixture.LeftFrontRenderer.gameObject.activeSelf);
                fixture.AssertRuntimeSheetMaterial(0, "backRenderer", fixture.PageMaterials[2], shouldBeActive: true);
                fixture.AssertRuntimeSheetMaterial(1, "frontRenderer", fixture.PageMaterials[3], shouldBeActive: true);
                fixture.AssertRuntimeSheetInactive(2);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void BeginManualTurn_ForwardRevealsNextUnreadSheetOnlyAfterActualDragProgress()
        {
            var fixture = new PageTurnerFixture();

            try
            {
                fixture.AssignAllRenderers();
                fixture.AssignPageMaterials(6);
                fixture.InvokeSetSpread(0);

                bool began = fixture.InvokeBeginManualTurn(true);

                Assert.IsTrue(began);
                fixture.AssertRuntimeSheetMaterial(0, "frontRenderer", fixture.PageMaterials[1], shouldBeActive: true);
                fixture.AssertRuntimeSheetMaterial(0, "backRenderer", fixture.PageMaterials[2], shouldBeActive: true);
                fixture.AssertRuntimeSheetInactive(1);

                fixture.InvokeSetManualTurnAngle(2f);
                fixture.AssertRuntimeSheetInactive(1);

                fixture.InvokeSetManualTurnAngle(12f);
                fixture.AssertRuntimeSheetMaterial(1, "frontRenderer", fixture.PageMaterials[3], shouldBeActive: true);
                fixture.AssertRuntimeSheetMaterial(1, "backRenderer", fixture.PageMaterials[4], shouldBeActive: true);
            }
            finally
            {
                fixture.Dispose();
            }
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
                LeftFrontRenderer.transform.localPosition = new Vector3(-0.12f, 0.01f, 0f);
                RightFrontRenderer.transform.localPosition = new Vector3(0.12f, 0.01f, 0f);

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

            public int GetRuntimeSheetCount()
            {
                IList runtimeSheets = GetRuntimeSheets();
                return runtimeSheets.Count;
            }

            public void AssertRuntimeSheetPages(int sheetIndex, int expectedFrontPage, int expectedBackPage)
            {
                object sheet = GetRuntimeSheets()[sheetIndex];
                Assert.AreEqual(expectedFrontPage, GetFieldValue<int>(sheet, "frontPageIndex"));
                Assert.AreEqual(expectedBackPage, GetFieldValue<int>(sheet, "backPageIndex"));
            }

            public void AssertRuntimeSheetMaterial(int sheetIndex, string rendererFieldName, Material expected, bool shouldBeActive)
            {
                object sheet = GetRuntimeSheets()[sheetIndex];
                Renderer renderer = GetFieldValue<Renderer>(sheet, rendererFieldName);
                Assert.IsNotNull(renderer);
                Assert.AreEqual(shouldBeActive, renderer.gameObject.activeSelf);
                Assert.IsNotNull(renderer.sharedMaterial);
                Assert.AreEqual(expected.color, renderer.sharedMaterial.color);
            }

            public void AssertRuntimeSheetInactive(int sheetIndex)
            {
                object sheet = GetRuntimeSheets()[sheetIndex];
                Renderer renderer = GetFieldValue<Renderer>(sheet, "frontRenderer");
                Assert.IsNotNull(renderer);
                Assert.IsFalse(renderer.gameObject.activeSelf);
            }

            private IList GetRuntimeSheets()
            {
                return GetFieldValue<IList>(turner, "runtimeSheets");
            }

            private void SetField(string fieldName, object value)
            {
                FieldInfo field = turnerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(field, $"Expected private field '{fieldName}' to exist.");
                field.SetValue(turner, value);
            }

            private static T GetFieldValue<T>(object target, string fieldName)
            {
                FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                Assert.IsNotNull(field, $"Expected field '{fieldName}' to exist on {target.GetType().Name}.");
                return (T)field.GetValue(target);
            }

            private static Renderer CreateRenderer(string name, Transform parent)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = name;
                go.transform.SetParent(parent, false);
                go.transform.localScale = new Vector3(0.25f, 0.3f, 1f);

                var collider = go.GetComponent<Collider>();
                if (collider != null)
                    UnityEngine.Object.DestroyImmediate(collider);

                return go.GetComponent<Renderer>();
            }

            public void Dispose()
            {
                if (PageMaterials != null)
                {
                    foreach (Material material in PageMaterials)
                    {
                        if (material != null)
                            UnityEngine.Object.DestroyImmediate(material);
                    }
                }

                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
