using ArcaneVR.Combat;
using ArcaneVR.Spell;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.Input
{
    public static class GestureSpellPrototypeBootstrap
    {
        private static readonly Color TargetBaseColor = new Color(0.2f, 0.95f, 0.65f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SetupPrototypeScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (!HandGestureDebugOverlay.IsPrototypeScene(sceneName))
            {
                return;
            }

            var gestureDetector = Object.FindAnyObjectByType<GestureDetector>();
            if (gestureDetector == null)
            {
                var inputRoot = new GameObject("Gesture Prototype Input");
                gestureDetector = inputRoot.AddComponent<GestureDetector>();
            }

            var gestureRouter = Object.FindAnyObjectByType<GestureEventRouter>();
            if (gestureRouter == null)
            {
                var routerHost = GameObject.Find("InputManager") ?? gestureDetector.gameObject;
                gestureRouter = routerHost.AddComponent<GestureEventRouter>();
            }

            gestureDetector.BindGestureEventRouter(gestureRouter);

            var spellRoot = GameObject.Find("Gesture Prototype SpellSystem");
            if (spellRoot == null)
                spellRoot = new GameObject("Gesture Prototype SpellSystem");

            var spawnRoot = GameObject.Find("SpellSpawnRoot");
            if (spawnRoot == null)
                spawnRoot = new GameObject("SpellSpawnRoot");

            var spellCaster = Object.FindAnyObjectByType<SpellCaster>();
            if (spellCaster == null)
                spellCaster = spellRoot.AddComponent<SpellCaster>();

            var rightHand = FindOvrHand(false);
            var leftHand = FindOvrHand(true);
            gestureDetector.BindHands(leftHand, rightHand);

            spellCaster.ConfigureGesturePrototype(
                gestureDetector,
                gestureRouter,
                rightHand,
                rightHand != null ? rightHand.transform : null,
                spawnRoot.transform);

            var movement = Object.FindAnyObjectByType<MovementController>();
            if (movement == null)
                movement = spellRoot.AddComponent<MovementController>();

            movement.ConfigureLeftFistPrototype(
                gestureDetector,
                gestureRouter,
                FindPlayerRig(),
                leftHand != null ? leftHand.transform : null);

            DisablePrototypeAuras();
            EnsureDebugOverlay(gestureDetector);
            EnsureTestTarget();
        }

        private static OVRHand FindOvrHand(bool isLeft)
        {
            var expected = isLeft ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
            OVRHand bestHand = null;
            var bestScore = int.MinValue;

            foreach (var hand in Object.FindObjectsByType<OVRHand>(FindObjectsInactive.Include))
            {
                if (hand.GetHand() != expected)
                    continue;

                var score = ScoreHandBindingCandidate(hand, isLeft);
                if (score <= bestScore)
                    continue;

                bestHand = hand;
                bestScore = score;
            }

            if (bestHand != null)
                Debug.Log($"[ArcaneVR] Bound {(isLeft ? "left" : "right")} OVRHand: {GetHierarchyPath(bestHand.transform)} score:{bestScore}");

            return bestHand;
        }

        private static int ScoreHandBindingCandidate(OVRHand hand, bool isLeft)
        {
            if (hand == null)
                return int.MinValue;

            var score = 0;
            if (hand.gameObject.activeInHierarchy)
                score += 100;
            if (hand.enabled)
                score += 20;
            if (hand.IsTracked)
                score += 30;
            if (hand.HandConfidence == OVRHand.TrackingConfidence.High)
                score += 30;
            if (hand.IsPointerPoseValid)
                score += 10;
            if (hand.GetComponentInChildren<OVRSkeleton>(true) != null)
                score += 10;
            if (hand.GetComponentInChildren<OVRMeshRenderer>(true) != null)
                score += 10;

            var path = GetHierarchyPath(hand.transform);
            if (path.Contains("Detached"))
                score -= 80;
            if (path.Contains("Controller"))
                score -= 25;
            if (path.Contains(isLeft ? "LeftHandAnchor/" : "RightHandAnchor/"))
                score += 30;
            if (path.Contains(isLeft ? "RightHand" : "LeftHand"))
                score -= 120;

            return score;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }

            return path;
        }

        private static Transform FindPlayerRig()
        {
            var rig = GameObject.Find("XR Origin") ??
                      GameObject.Find("OVRCameraRig") ??
                      GameObject.Find("XROriginCameraRig");
            return rig != null ? rig.transform : null;
        }

        private static void DisablePrototypeAuras()
        {
            foreach (var aura in Object.FindObjectsByType<GesturePrototypeAura>(FindObjectsInactive.Include))
                aura.DisableAndClear();

            foreach (var particleSystem in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include))
            {
                if (!particleSystem.name.Contains("PrototypeAura") &&
                    !particleSystem.name.Contains("AuraEffect"))
                {
                    continue;
                }

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.gameObject.SetActive(false);
            }
        }

        private static void EnsureDebugOverlay(GestureDetector gestureDetector)
        {
            var overlay = Object.FindAnyObjectByType<HandGestureDebugOverlay>();
            if (overlay == null)
            {
                var host = new GameObject("Arcane Gesture Text Overlay");
                overlay = host.AddComponent<HandGestureDebugOverlay>();
            }

            overlay.Configure(gestureDetector);
        }

        private static void EnsureTestTarget()
        {
            var existingReceiver = Object.FindAnyObjectByType<DebugHitReceiver>();
            if (existingReceiver != null)
            {
                existingReceiver.gameObject.name = "TestTarget";
                ConfigureTargetVisual(existingReceiver.gameObject);
                return;
            }

            var existingTarget = GameObject.Find("TestTarget");
            if (existingTarget != null)
            {
                if (existingTarget.GetComponent<DebugHitReceiver>() == null)
                    existingTarget.AddComponent<DebugHitReceiver>();

                ConfigureTargetVisual(existingTarget);
                return;
            }

            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "TestTarget";
            target.transform.position = new Vector3(0f, 1.25f, 4.5f);
            target.transform.localScale = Vector3.one * 0.45f;
            target.AddComponent<DebugHitReceiver>();

            var rigidbody = target.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;

            ConfigureTargetVisual(target);
        }

        private static void ConfigureTargetVisual(GameObject target)
        {
            if (target == null)
                return;

            var renderer = target.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateRuntimeMaterial(TargetBaseColor);

            var receiver = target.GetComponent<DebugHitReceiver>();
            if (receiver != null)
                receiver.ConfigureBaseColor(TargetBaseColor);
        }

        private static Material CreateRuntimeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Hidden/Internal-Colored") ??
                         Shader.Find("Standard");

            var material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            return material;
        }
    }
}
