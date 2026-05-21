using ArcaneVR.Combat;
using ArcaneVR.Core;
using ArcaneVR.Spell;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.Input
{
    public static class GestureSpellPrototypeBootstrap
    {
        private static readonly FieldInfo OvrSkeletonUpdateRootPoseField =
            typeof(OVRSkeleton).GetField("_updateRootPose", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Color TargetBaseColor = new Color(0.2f, 0.95f, 0.65f, 1f);
        private static readonly Color FloorColor = new Color(0.05f, 0.06f, 0.07f, 1f);
        private static readonly Color GridColor = new Color(0.16f, 0.38f, 0.56f, 1f);
        private static readonly Color MarkerColor = new Color(0.95f, 0.78f, 0.22f, 1f);
        private static bool logHandBindingDebug;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SetupPrototypeScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (!HandGestureDebugOverlay.IsPrototypeScene(sceneName))
            {
                return;
            }

            ConfigureOvrRuntime();

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

            NormalizeSceneOvrHands(out var leftHand, out var rightHand, out _);

            gestureDetector.BindHands(leftHand, rightHand);

            spellCaster.ConfigureGesturePrototype(
                gestureDetector,
                gestureRouter,
                rightHand,
                rightHand != null ? rightHand.transform : null,
                spawnRoot.transform);

            var legacyMovement = Object.FindAnyObjectByType<MovementController>();
            if (legacyMovement != null)
            {
                legacyMovement.IsEnabled = false;
                legacyMovement.enabled = false;
            }

            var handPullMovement = Object.FindAnyObjectByType<HandPullMovementController>();
            if (handPullMovement == null)
                handPullMovement = spellRoot.AddComponent<HandPullMovementController>();

            var playerRig = FindPlayerRig();
            var head = Camera.main != null ? Camera.main.transform : null;
            handPullMovement.ConfigurePrototype(playerRig, head);

            DisablePrototypeAuras();
            if (HandGestureDebugOverlay.ShouldAutoCreateOverlay(sceneName))
                EnsureDebugOverlay(gestureDetector);

            EnsureTestTarget();
            EnsureMovementReferenceWorld();
        }

        public static bool NormalizeSceneOvrHands(out OVRHand leftHand, out OVRHand rightHand, out OVRCameraRig cameraRig)
        {
            ConfigureOvrRuntime();

            cameraRig = FindActiveOvrCameraRig();
            leftHand = FindOvrHand(true);
            rightHand = FindOvrHand(false);

            NormalizeOvrHandParent(leftHand, true, cameraRig);
            NormalizeOvrHandParent(rightHand, false, cameraRig);

            return leftHand != null || rightHand != null;
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

            if (bestHand != null && logHandBindingDebug)
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
                score -= 200;
            if (path.Contains("Controller"))
                score -= 80;
            if (path.Contains(isLeft ? "LeftHandAnchor/" : "RightHandAnchor/"))
                score += 200;
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

        private static void ConfigureOvrRuntime()
        {
            var manager = OVRManager.instance != null
                ? OVRManager.instance
                : Object.FindAnyObjectByType<OVRManager>();

            if (manager == null)
                return;

            manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            manager.usePositionTracking = true;
            manager.enableDynamicResolution = true;

            var cameraRig = FindActiveOvrCameraRig();
            if (cameraRig != null)
                cameraRig.useFixedUpdateForTracking = false;
        }

        private static OVRCameraRig FindActiveOvrCameraRig()
        {
            if (Camera.main != null)
            {
                var mainRig = Camera.main.GetComponentInParent<OVRCameraRig>();
                if (mainRig != null)
                    return mainRig;
            }

            OVRCameraRig bestRig = null;
            var bestScore = int.MinValue;
            foreach (var rig in Object.FindObjectsByType<OVRCameraRig>(FindObjectsInactive.Include))
            {
                rig.EnsureGameObjectIntegrity();

                var score = rig.gameObject.activeInHierarchy ? 100 : 0;
                if (rig.centerEyeAnchor != null && rig.centerEyeAnchor.GetComponent<Camera>() != null)
                    score += 30;
                if (rig.trackingSpace != null)
                    score += 20;

                if (score <= bestScore)
                    continue;

                bestRig = rig;
                bestScore = score;
            }

            return bestRig;
        }

        private static void NormalizeOvrHandParent(OVRHand hand, bool isLeft, OVRCameraRig cameraRig)
        {
            if (hand == null)
                return;

            var anchor = FindHandAnchor(isLeft, cameraRig);
            if (anchor == null)
                return;

            EnableSelectedHand(hand);
            UseAnchorDrivenSkeletonRootPose(hand);

            if (hand.transform.parent != anchor)
            {
                hand.transform.SetParent(anchor, false);

                Debug.Log($"[ArcaneVR] Reparented {(isLeft ? "left" : "right")} OVRHand to {GetHierarchyPath(anchor)}");
            }

            hand.transform.localPosition = Vector3.zero;
            hand.transform.localRotation = Quaternion.identity;
            hand.transform.localScale = Vector3.one;

            AttachPointerPoseToTrackingSpace(hand, cameraRig);
            DisableDuplicateHands(hand, isLeft);
        }

        private static void EnableSelectedHand(OVRHand hand)
        {
            hand.gameObject.SetActive(true);
            hand.enabled = true;

            foreach (var skeleton in hand.GetComponentsInChildren<OVRSkeleton>(true))
                skeleton.enabled = true;

            foreach (var mesh in hand.GetComponentsInChildren<OVRMesh>(true))
                mesh.enabled = true;

            foreach (var meshRenderer in hand.GetComponentsInChildren<OVRMeshRenderer>(true))
                meshRenderer.enabled = true;

            foreach (var renderer in hand.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = true;
        }

        private static void UseAnchorDrivenSkeletonRootPose(OVRHand hand)
        {
            if (OvrSkeletonUpdateRootPoseField == null)
                return;

            foreach (var skeleton in hand.GetComponentsInChildren<OVRSkeleton>(true))
                OvrSkeletonUpdateRootPoseField.SetValue(skeleton, false);
        }

        private static void AttachPointerPoseToTrackingSpace(OVRHand hand, OVRCameraRig cameraRig)
        {
            if (cameraRig == null || cameraRig.trackingSpace == null)
                return;

            var pointerPose = hand.PointerPose;
            if (pointerPose != null && pointerPose.parent != cameraRig.trackingSpace)
                pointerPose.SetParent(cameraRig.trackingSpace, false);
        }

        private static Transform FindHandAnchor(bool isLeft, OVRCameraRig cameraRig)
        {
            if (cameraRig != null)
            {
                cameraRig.EnsureGameObjectIntegrity();
                var anchor = isLeft ? cameraRig.leftHandAnchor : cameraRig.rightHandAnchor;
                if (anchor != null)
                    return anchor;
            }

            var anchorName = isLeft ? "LeftHandAnchor" : "RightHandAnchor";
            return GameObject.Find(anchorName)?.transform;
        }

        private static void DisableDuplicateHands(OVRHand selectedHand, bool isLeft)
        {
            var expected = isLeft ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
            foreach (var hand in Object.FindObjectsByType<OVRHand>(FindObjectsInactive.Include))
            {
                if (hand == null || hand == selectedHand || hand.GetHand() != expected)
                    continue;

                hand.gameObject.SetActive(false);
                Debug.Log($"[ArcaneVR] Disabled duplicate {(isLeft ? "left" : "right")} OVRHand: {GetHierarchyPath(hand.transform)}");
            }
        }

        private static Transform FindPlayerRig()
        {
            GameObject rig = null;

            var cameraRig = FindActiveOvrCameraRig();
            if (cameraRig != null)
                rig = cameraRig.gameObject;

            rig = rig ?? ArcanePlayerRigResolver.FindPlayerRigGameObject();
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

        private static void EnsureMovementReferenceWorld()
        {
            if (GameObject.Find("Arcane Prototype Movement World") != null)
                return;

            var root = new GameObject("Arcane Prototype Movement World");

            CreateReferenceCube(
                "Floor",
                root.transform,
                new Vector3(0f, -0.08f, 4f),
                new Vector3(8f, 0.04f, 10f),
                FloorColor);

            for (int x = -4; x <= 4; x++)
            {
                CreateReferenceCube(
                    $"Grid_X_{x}",
                    root.transform,
                    new Vector3(x, -0.055f, 4f),
                    new Vector3(0.018f, 0.018f, 10f),
                    GridColor);
            }

            for (int z = -1; z <= 9; z++)
            {
                CreateReferenceCube(
                    $"Grid_Z_{z}",
                    root.transform,
                    new Vector3(0f, -0.052f, z),
                    new Vector3(8f, 0.018f, 0.018f),
                    GridColor);
            }

            for (int z = 1; z <= 8; z += 2)
            {
                CreateReferenceCube(
                    $"Left_Marker_{z}m",
                    root.transform,
                    new Vector3(-2.7f, 0.6f, z),
                    new Vector3(0.16f, 1.2f, 0.16f),
                    MarkerColor);

                CreateReferenceCube(
                    $"Right_Marker_{z}m",
                    root.transform,
                    new Vector3(2.7f, 0.6f, z),
                    new Vector3(0.16f, 1.2f, 0.16f),
                    MarkerColor);
            }

            CreateReferenceCube(
                "Origin_Cross_X",
                root.transform,
                new Vector3(0f, -0.035f, 0f),
                new Vector3(1.2f, 0.04f, 0.06f),
                Color.white);

            CreateReferenceCube(
                "Origin_Cross_Z",
                root.transform,
                new Vector3(0f, -0.03f, 0f),
                new Vector3(0.06f, 0.04f, 1.2f),
                Color.white);

            CreateReferenceText(
                "MovementLabel",
                root.transform,
                "ARCANE FIELD",
                new Vector3(0f, 1.75f, 6.4f),
                new Color(0.85f, 0.95f, 1f, 1f));
        }

        private static GameObject CreateReferenceCube(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;

            var collider = cube.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateRuntimeMaterial(color);

            return cube;
        }

        private static void CreateReferenceText(
            string name,
            Transform parent,
            string text,
            Vector3 position,
            Color color)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.position = position;
            host.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var textMesh = host.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 56;
            textMesh.characterSize = 0.035f;
            textMesh.color = color;
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
