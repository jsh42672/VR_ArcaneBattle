using ArcaneVR.Combat;
using ArcaneVR.Input;
using ArcaneVR.UI;
using UnityEngine;

namespace ArcaneVR.Spell
{
    /// <summary>
    /// Instantiates and fires spell prefabs at hand position with head-direction aim assist. Reads spell data from SpellDatabase.
    /// </summary>
    public class SpellCaster : MonoBehaviour
    {
        [SerializeField] private SpellDatabase spellDatabase;
        [SerializeField] private CombinationChecker combinationChecker;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private FeedbackManager feedbackManager;
        [SerializeField] private Transform leftHandSpawnPoint;
        [SerializeField] private Transform rightHandSpawnPoint;
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform spellSpawnRoot;
        [SerializeField] private float fallbackProjectileLifetime = 5f;
        [SerializeField] private bool useDebugPrimitiveProjectiles = true;
        [SerializeField] private float debugProjectileScale = 1f;

        [Header("Gesture Spell Prototype")]
        [SerializeField] private bool enableGesturePrototype;
        [SerializeField] private GestureDetector gestureDetector;
        [SerializeField] private GestureEventRouter gestureRouter;
        [SerializeField] private OVRHand prototypeHand;
        [SerializeField] private Transform prototypeSpawnPoint;
        [SerializeField] private Transform prototypeSpellSpawnRoot;
        [SerializeField] private float prototypeThrustThreshold = 0.65f;
        [SerializeField] private float prototypeSpellSpeed = 12f;
        [SerializeField] private float prototypeCooldown = 0.5f;
        [SerializeField] private float prototypeProjectileScale = 0.12f;
        [SerializeField] private float prototypeSpawnForwardOffset = 0.18f;
        [SerializeField] private float prototypeMinimumProjectileSpeed = 8f;
        [SerializeField] private bool prototypeAimAtViewCenter = true;
        [SerializeField] private float prototypeAimDistance = 18f;
        [SerializeField] private GameObject spellPrefabOpenPalm;
        [SerializeField] private GameObject spellPrefabFist;
        [SerializeField] private GameObject spellPrefabThumbsUp;

        private PoseType currentPrototypePose = PoseType.None;
        private Vector3 previousPrototypeWristPosition;
        private float prototypeCooldownTimer;
        private bool hasPreviousPrototypeWristPosition;
        private bool prototypeEventsSubscribed;
        private bool prototypeRouterEventsSubscribed;
        private bool prototypeReadyForThrust = true;
        private float prototypeLastForwardSpeed;
        private float prototypeLastHandForwardSpeed;
        private float prototypeLastHeadForwardSpeed;
        private float prototypeLastAwayFromHeadSpeed;
        private string prototypeDebugStatus = "CAST: waiting";

        public SpellDatabase Database
        {
            get => spellDatabase;
            set => spellDatabase = value;
        }

        public string PrototypeDebugStatus => prototypeDebugStatus;

        private void Awake()
        {
            if (spellDatabase == null)
                spellDatabase = Resources.Load<SpellDatabase>("ArcaneVR/SpellDatabase");

            if (combinationChecker == null)
                combinationChecker = FindAnyObjectByType<CombinationChecker>();

            if (combatManager == null)
                combatManager = FindAnyObjectByType<CombatManager>();

            if (feedbackManager == null)
                feedbackManager = FindAnyObjectByType<FeedbackManager>();

            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();

            if (gestureRouter == null)
                gestureRouter = FindAnyObjectByType<GestureEventRouter>();

            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;

            ResolvePrototypeReferences();
        }

        private void OnEnable()
        {
            if (combinationChecker != null)
                combinationChecker.OnCombinationSuccess += HandleCombinationSuccess;

            SubscribePrototypeEvents();
            SubscribePrototypeRouterEvents();
        }

        private void OnDisable()
        {
            if (combinationChecker != null)
                combinationChecker.OnCombinationSuccess -= HandleCombinationSuccess;

            UnsubscribePrototypeEvents();
            UnsubscribePrototypeRouterEvents();
        }

        private void Update()
        {
            UpdateGesturePrototypeCasting();
        }

        private void HandleCombinationSuccess(SpellId spellId)
        {
            Cast(spellId);
        }

        public void ConfigureGesturePrototype(GestureDetector detector, OVRHand hand, Transform spawnPoint, Transform spawnRoot)
        {
            ConfigureGesturePrototype(detector, FindAnyObjectByType<GestureEventRouter>(), hand, spawnPoint, spawnRoot);
        }

        public void ConfigureGesturePrototype(
            GestureDetector detector,
            GestureEventRouter router,
            OVRHand hand,
            Transform spawnPoint,
            Transform spawnRoot)
        {
            if (gestureDetector != detector)
                UnsubscribePrototypeEvents();

            if (gestureRouter != router)
                UnsubscribePrototypeRouterEvents();

            gestureDetector = detector;
            gestureRouter = router;
            prototypeHand = hand;
            prototypeSpawnPoint = spawnPoint;
            prototypeSpellSpawnRoot = spawnRoot;
            enableGesturePrototype = true;
            prototypeThrustThreshold = Mathf.Min(prototypeThrustThreshold, 0.65f);
            ResolvePrototypeReferences();
            SubscribePrototypeEvents();
            SubscribePrototypeRouterEvents();
        }

        private void SubscribePrototypeEvents()
        {
            if (prototypeEventsSubscribed)
                return;

            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();

            if (gestureDetector == null)
                return;

            gestureDetector.OnRightPoseConfirmed += HandleRightPrototypePoseConfirmed;
            gestureDetector.OnRightPoseCleared += HandleRightPrototypePoseCleared;
            prototypeEventsSubscribed = true;
        }

        private void SubscribePrototypeRouterEvents()
        {
            if (prototypeRouterEventsSubscribed)
                return;

            if (gestureRouter == null)
                gestureRouter = FindAnyObjectByType<GestureEventRouter>();

            if (gestureRouter == null)
                return;

            gestureRouter.OnRightPoseConfirmed += HandleRightPrototypePoseConfirmed;
            gestureRouter.OnRightPoseCleared += HandleRightPrototypePoseCleared;
            prototypeRouterEventsSubscribed = true;
        }

        private void UnsubscribePrototypeEvents()
        {
            if (!prototypeEventsSubscribed || gestureDetector == null)
                return;

            gestureDetector.OnRightPoseConfirmed -= HandleRightPrototypePoseConfirmed;
            gestureDetector.OnRightPoseCleared -= HandleRightPrototypePoseCleared;
            prototypeEventsSubscribed = false;

            UnsubscribePrototypeRouterEvents();
        }

        private void UnsubscribePrototypeRouterEvents()
        {
            if (!prototypeRouterEventsSubscribed || gestureRouter == null)
                return;

            gestureRouter.OnRightPoseConfirmed -= HandleRightPrototypePoseConfirmed;
            gestureRouter.OnRightPoseCleared -= HandleRightPrototypePoseCleared;
            prototypeRouterEventsSubscribed = false;
        }

        private void HandleRightPrototypePoseConfirmed(PoseType pose)
        {
            if (currentPrototypePose != pose)
                prototypeReadyForThrust = true;

            currentPrototypePose = pose;
            hasPreviousPrototypeWristPosition = false;
        }

        private void HandleRightPrototypePoseCleared()
        {
            currentPrototypePose = PoseType.None;
            hasPreviousPrototypeWristPosition = false;
            prototypeReadyForThrust = true;
        }

        private void UpdateGesturePrototypeCasting()
        {
            if (!enableGesturePrototype)
            {
                prototypeDebugStatus = "CAST: disabled";
                return;
            }

            prototypeCooldownTimer -= Time.deltaTime;
            ResolvePrototypeReferences();
            SyncPrototypePoseFromDetector();

            var spawnPoint = ResolvePrototypeSpawnPoint();
            if (spawnPoint == null || Time.deltaTime <= 0f)
            {
                prototypeDebugStatus = "CAST: no spawn point";
                return;
            }

            var wristPosition = ResolvePrototypeCastOrigin(spawnPoint);
            if (!hasPreviousPrototypeWristPosition)
            {
                previousPrototypeWristPosition = wristPosition;
                hasPreviousPrototypeWristPosition = true;
                prototypeDebugStatus = $"CAST pose:{currentPrototypePose} speed:0.00/{prototypeThrustThreshold:0.00} init";
                return;
            }

            var wristVelocity = (wristPosition - previousPrototypeWristPosition) / Time.deltaTime;
            previousPrototypeWristPosition = wristPosition;

            var fireDirection = ResolvePrototypeFireDirection(spawnPoint, wristPosition);
            var forwardSpeed = ResolvePrototypeForwardSpeed(spawnPoint, wristPosition, wristVelocity);
            prototypeLastForwardSpeed = forwardSpeed;
            prototypeDebugStatus =
                $"CAST pose:{currentPrototypePose} speed:{forwardSpeed:0.00}/{prototypeThrustThreshold:0.00} aim:{prototypeLastHandForwardSpeed:0.00} head:{prototypeLastHeadForwardSpeed:0.00} away:{prototypeLastAwayFromHeadSpeed:0.00} cd:{Mathf.Max(0f, prototypeCooldownTimer):0.00}";

            if (forwardSpeed < prototypeThrustThreshold * 0.25f)
                prototypeReadyForThrust = true;

            if (forwardSpeed <= prototypeThrustThreshold ||
                currentPrototypePose == PoseType.None ||
                prototypeCooldownTimer > 0f ||
                !prototypeReadyForThrust)
            {
                return;
            }

            FirePrototypeSpell(currentPrototypePose, wristPosition, fireDirection);
            prototypeCooldownTimer = prototypeCooldown;
            prototypeReadyForThrust = false;
        }

        private void SyncPrototypePoseFromDetector()
        {
            if (gestureRouter != null && gestureRouter.HasReceivedGestureEvent)
            {
                if (currentPrototypePose != gestureRouter.CurrentRightPose)
                    currentPrototypePose = gestureRouter.CurrentRightPose;
                return;
            }

            if (gestureDetector == null)
                return;

            var detectorPose = gestureDetector.CurrentRightPrototypePose;
            if (detectorPose == PoseType.None)
                detectorPose = ConvertPoseIdToPrototypePose(gestureDetector.CurrentRightPose);

            if (currentPrototypePose != detectorPose)
                currentPrototypePose = detectorPose;
        }

        private static PoseType ConvertPoseIdToPrototypePose(PoseId pose)
        {
            return pose switch
            {
                PoseId.OpenPalm => PoseType.OpenPalm,
                PoseId.Fist => PoseType.Fist,
                PoseId.FistPush => PoseType.Fist,
                _ => PoseType.None
            };
        }

        private void ResolvePrototypeReferences()
        {
            if (prototypeSpellSpawnRoot == null)
                prototypeSpellSpawnRoot = spellSpawnRoot;

            if (gestureRouter == null)
                gestureRouter = FindAnyObjectByType<GestureEventRouter>();

            if (prototypeSpawnPoint == null)
                prototypeSpawnPoint = rightHandSpawnPoint != null ? rightHandSpawnPoint : leftHandSpawnPoint;

            if (prototypeHand != null)
                return;

            foreach (var hand in FindObjectsByType<OVRHand>(FindObjectsInactive.Include))
            {
                if (hand.GetHand() != OVRPlugin.Hand.HandRight)
                    continue;

                prototypeHand = hand;
                if (prototypeSpawnPoint == null)
                    prototypeSpawnPoint = hand.transform;
                return;
            }
        }

        private Transform ResolvePrototypeSpawnPoint()
        {
            if (prototypeSpawnPoint != null)
                return prototypeSpawnPoint;

            if (prototypeHand != null)
                return prototypeHand.transform;

            return rightHandSpawnPoint != null ? rightHandSpawnPoint : leftHandSpawnPoint;
        }

        private Vector3 ResolvePrototypeCastOrigin(Transform fallback)
        {
            if (TryGetPrototypePointerPose(out var pointerPosition) &&
                IsUsablePrototypeHandPosition(pointerPosition))
            {
                return pointerPosition;
            }

            if (TryGetPrototypeBonePosition(
                    out var indexTipPosition,
                    OVRSkeleton.BoneId.XRHand_IndexTip,
                    OVRSkeleton.BoneId.Hand_IndexTip) &&
                IsUsablePrototypeHandPosition(indexTipPosition))
            {
                return indexTipPosition;
            }

            if (TryGetPrototypeBonePosition(
                    out var palmPosition,
                    OVRSkeleton.BoneId.XRHand_Palm,
                    OVRSkeleton.BoneId.XRHand_Wrist,
                    OVRSkeleton.BoneId.Hand_WristRoot) &&
                IsUsablePrototypeHandPosition(palmPosition))
            {
                return palmPosition;
            }

            if (fallback != null)
                return fallback.position;

            return transform.position;
        }

        private bool TryGetPrototypePointerPose(out Vector3 position)
        {
            position = Vector3.zero;
            if (prototypeHand == null || !prototypeHand.IsPointerPoseValid || prototypeHand.PointerPose == null)
                return false;

            position = prototypeHand.PointerPose.position;
            return true;
        }

        private bool TryGetPrototypeBonePosition(out Vector3 position, params OVRSkeleton.BoneId[] boneIds)
        {
            position = Vector3.zero;
            if (prototypeHand == null)
                return false;

            foreach (var skeleton in prototypeHand.GetComponentsInChildren<OVRSkeleton>(true))
            {
                if (TryGetSkeletonBonePosition(skeleton, out position, boneIds))
                    return true;
            }

            var expectedHand = prototypeHand.GetHand();
            foreach (var skeleton in FindObjectsByType<OVRSkeleton>(FindObjectsInactive.Include))
            {
                if (!MatchesPrototypeHand(skeleton, expectedHand))
                    continue;

                if (TryGetSkeletonBonePosition(skeleton, out position, boneIds))
                    return true;
            }

            return false;
        }

        private static bool TryGetSkeletonBonePosition(OVRSkeleton skeleton, out Vector3 position, params OVRSkeleton.BoneId[] boneIds)
        {
            position = Vector3.zero;
            if (skeleton == null || skeleton.Bones == null)
                return false;

            foreach (var requestedId in boneIds)
            {
                foreach (var bone in skeleton.Bones)
                {
                    if (bone == null || bone.Transform == null || bone.Id != requestedId)
                        continue;

                    position = bone.Transform.position;
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesPrototypeHand(OVRSkeleton skeleton, OVRPlugin.Hand expectedHand)
        {
            var skeletonType = skeleton.GetSkeletonType();
            return expectedHand switch
            {
                OVRPlugin.Hand.HandLeft => skeletonType == OVRSkeleton.SkeletonType.HandLeft ||
                                           skeletonType == OVRSkeleton.SkeletonType.XRHandLeft,
                OVRPlugin.Hand.HandRight => skeletonType == OVRSkeleton.SkeletonType.HandRight ||
                                            skeletonType == OVRSkeleton.SkeletonType.XRHandRight,
                _ => false
            };
        }

        private bool IsUsablePrototypeHandPosition(Vector3 position)
        {
            var head = ResolvePrototypeHeadTransform();
            return head == null || Vector3.Distance(position, head.position) > 0.12f;
        }

        private Vector3 ResolvePrototypeFireDirection(Transform spawnPoint, Vector3 wristPosition)
        {
            var head = ResolvePrototypeHeadTransform();
            var headForward = head != null ? head.forward : transform.forward;
            var handForward = spawnPoint.forward;
            var awayFromHead = head != null ? wristPosition - head.position : Vector3.zero;

            if (prototypeAimAtViewCenter && head != null)
            {
                var aimPoint = ResolvePrototypeViewCenterAimPoint(head);
                var viewCenterDirection = aimPoint - wristPosition;
                if (viewCenterDirection.sqrMagnitude > 0.001f)
                    return viewCenterDirection.normalized;
            }

            if (awayFromHead.sqrMagnitude > 0.001f)
                awayFromHead.Normalize();

            var direction = handForward;
            if (direction.sqrMagnitude < 0.001f || Vector3.Dot(direction.normalized, headForward.normalized) < 0.15f)
                direction = headForward;

            if (direction.sqrMagnitude < 0.001f)
                direction = awayFromHead;

            return direction.sqrMagnitude < 0.001f ? transform.forward : direction.normalized;
        }

        private Vector3 ResolvePrototypeViewCenterAimPoint(Transform head)
        {
            var headForward = head.forward.sqrMagnitude > 0.001f ? head.forward.normalized : transform.forward.normalized;
            return head.position + headForward * Mathf.Max(1f, prototypeAimDistance);
        }

        private float ResolvePrototypeForwardSpeed(Transform spawnPoint, Vector3 wristPosition, Vector3 wristVelocity)
        {
            var head = ResolvePrototypeHeadTransform();
            var aimForward = ResolvePrototypeFireDirection(spawnPoint, wristPosition);
            var headForward = head != null && head.forward.sqrMagnitude > 0.001f ? head.forward.normalized : transform.forward.normalized;
            var awayFromHead = Vector3.zero;

            if (head != null)
            {
                awayFromHead = wristPosition - head.position;
                if (awayFromHead.sqrMagnitude > 0.001f)
                    awayFromHead.Normalize();
            }

            prototypeLastHandForwardSpeed = aimForward == Vector3.zero ? 0f : Vector3.Dot(wristVelocity, aimForward);
            prototypeLastHeadForwardSpeed = headForward == Vector3.zero ? 0f : Vector3.Dot(wristVelocity, headForward);
            prototypeLastAwayFromHeadSpeed = awayFromHead == Vector3.zero ? 0f : Vector3.Dot(wristVelocity, awayFromHead);

            return Mathf.Max(prototypeLastHandForwardSpeed, prototypeLastHeadForwardSpeed, prototypeLastAwayFromHeadSpeed);
        }

        private Transform ResolvePrototypeHeadTransform()
        {
            if (headTransform != null)
                return headTransform;

            if (Camera.main != null)
            {
                headTransform = Camera.main.transform;
                return headTransform;
            }

            return null;
        }

        private void FirePrototypeSpell(PoseType pose, Vector3 originPosition, Vector3 direction)
        {
            direction = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : (ResolvePrototypeHeadTransform() != null ? ResolvePrototypeHeadTransform().forward : transform.forward).normalized;

            var data = ResolvePrototypeSpellData(pose);
            var element = data != null ? data.element : ResolveDefaultPrototypeElement(pose);
            var statusEffect = data != null ? data.statusEffect : ResolveDefaultPrototypeStatusEffect(pose);
            var damage = data != null ? data.damage : ResolveDefaultPrototypeDamage(pose);
            var statusDuration = data != null ? data.statusDuration : ResolveDefaultPrototypeStatusDuration(pose);
            var projectileSpeed = Mathf.Max(
                prototypeMinimumProjectileSpeed,
                data != null ? data.projectileSpeed : prototypeSpellSpeed);
            var prefab = data != null && data.prefab != null ? data.prefab : ResolvePrototypePrefab(pose);
            var spawnPosition = originPosition + direction * prototypeSpawnForwardOffset;
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            var projectileObject = prefab != null
                ? Instantiate(prefab, spawnPosition, rotation)
                : CreatePrototypeProjectileObject(pose, element, spawnPosition, rotation);

            if (prototypeSpellSpawnRoot != null && prototypeSpellSpawnRoot.parent == null)
                projectileObject.transform.SetParent(prototypeSpellSpawnRoot, true);

            var projectile = projectileObject.GetComponent<SpellProjectile>();
            if (projectile == null)
                projectile = projectileObject.AddComponent<SpellProjectile>();

            EnsurePrototypeProjectilePhysics(projectileObject);
            projectile.InitializePrototype(pose, projectileSpeed, direction, element, statusEffect, damage, statusDuration);
            prototypeDebugStatus = $"CAST fired:{pose} {element}/{statusEffect} speed:{prototypeLastForwardSpeed:0.00}";
            Debug.Log($"[SPELL CAST] {pose} | {element} | {statusEffect} | DMG:{damage} | Dir:{direction}");
        }

        private SpellDatabase.PoseSpellData ResolvePrototypeSpellData(PoseType pose)
        {
            if (spellDatabase == null)
                spellDatabase = Resources.Load<SpellDatabase>("ArcaneVR/SpellDatabase");

            return spellDatabase != null && spellDatabase.TryGet(pose, out var data) ? data : null;
        }

        private GameObject ResolvePrototypePrefab(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => spellPrefabOpenPalm,
                PoseType.Fist => spellPrefabFist,
                PoseType.ThumbsUp => spellPrefabThumbsUp,
                _ => null
            };
        }

        private GameObject CreatePrototypeProjectileObject(PoseType pose, ElementType element, Vector3 position, Quaternion rotation)
        {
            var projectileObject = new GameObject($"PrototypeSpell_{pose}");
            projectileObject.transform.SetPositionAndRotation(position, rotation);
            var color = GetPrototypeSpellColor(pose, element);

            var collider = projectileObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = prototypeProjectileScale * 0.5f;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = $"{pose}_Sphere";
            visual.transform.SetParent(projectileObject.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * prototypeProjectileScale;

            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
                Destroy(visualCollider);

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateDebugMaterial(color);

            var light = projectileObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = 1.4f;
            light.intensity = 2f;

            return projectileObject;
        }

        private static void EnsurePrototypeProjectilePhysics(GameObject projectileObject)
        {
            var collider = projectileObject.GetComponent<Collider>();
            if (collider == null)
            {
                var sphereCollider = projectileObject.AddComponent<SphereCollider>();
                sphereCollider.radius = 0.06f;
                collider = sphereCollider;
            }

            collider.isTrigger = true;

            var rigidbody = projectileObject.GetComponent<Rigidbody>();
            if (rigidbody == null)
                rigidbody = projectileObject.AddComponent<Rigidbody>();

            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
        }

        private static Color GetPrototypePoseColor(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => new Color(1f, 0.2f, 0.2f, 1f),
                PoseType.Fist => new Color(0.2f, 0.4f, 1f, 1f),
                PoseType.ThumbsUp => new Color(1f, 0.86f, 0f, 1f),
                _ => Color.white
            };
        }

        private static Color GetPrototypeSpellColor(PoseType pose, ElementType element)
        {
            return element == ElementType.None ? GetPrototypePoseColor(pose) : GetElementColor(element);
        }

        private static ElementType ResolveDefaultPrototypeElement(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => ElementType.Fire,
                PoseType.Fist => ElementType.Ice,
                PoseType.ThumbsUp => ElementType.Thunder,
                _ => ElementType.None
            };
        }

        private static StatusEffect ResolveDefaultPrototypeStatusEffect(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => StatusEffect.Burn,
                PoseType.Fist => StatusEffect.Slow,
                PoseType.ThumbsUp => StatusEffect.Stagger,
                _ => StatusEffect.None
            };
        }

        private static float ResolveDefaultPrototypeDamage(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => 10f,
                PoseType.Fist => 8f,
                PoseType.ThumbsUp => 12f,
                _ => 0f
            };
        }

        private static float ResolveDefaultPrototypeStatusDuration(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => 3f,
                PoseType.Fist => 3f,
                PoseType.ThumbsUp => 1f,
                _ => 0f
            };
        }

        public bool Cast(SpellId spellId)
        {
            if (spellDatabase == null)
            {
                Debug.LogWarning("SpellCaster needs a SpellDatabase reference.");
                return false;
            }

            var data = spellDatabase.Get(spellId);
            if (data == null)
            {
                Debug.LogWarning($"SpellDatabase has no entry for {spellId}.");
                return false;
            }

            if (combatManager != null && !combatManager.TryConsumeMana(data.manaCost))
                return false;

            var spawnPoint = ResolveSpawnPoint(spellId);
            var spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
            var direction = ResolveAimDirection(spawnPosition);
            spawnPosition += direction * 0.25f;
            var element = data.element;
            if (combinationChecker != null && IsSingleSpell(spellId) && combinationChecker.CurrentElement != ElementType.None)
                element = combinationChecker.CurrentElement;

            var projectileObject = CreateProjectileObject(data, element, spawnPosition, Quaternion.LookRotation(direction, Vector3.up));
            var projectile = projectileObject.GetComponent<SpellProjectile>();

            if (projectile == null)
                projectile = projectileObject.AddComponent<SpellProjectile>();

            projectile.Initialize(
                spellId,
                element,
                data.damage,
                data.projectileSpeed,
                data.statusEffect,
                data.statusDuration,
                direction,
                combatManager);

            if (spellSpawnRoot != null)
                projectileObject.transform.SetParent(spellSpawnRoot, true);

            Destroy(projectileObject, fallbackProjectileLifetime);
            feedbackManager?.OnSpellCast(spellId);
            return true;
        }

        private Transform ResolveSpawnPoint(SpellId spellId)
        {
            if (spellId == SpellId.Single_Pointer || spellId == SpellId.Single_Wave || spellId == SpellId.Single_Strike)
                return rightHandSpawnPoint != null ? rightHandSpawnPoint : leftHandSpawnPoint;

            return leftHandSpawnPoint != null ? leftHandSpawnPoint : rightHandSpawnPoint;
        }

        private Vector3 ResolveAimDirection(Vector3 spawnPosition)
        {
            if (headTransform == null)
                return transform.forward;

            var direction = headTransform.forward;
            if (direction.sqrMagnitude < 0.001f)
                direction = headTransform.position + headTransform.forward * 10f - spawnPosition;

            return direction.normalized;
        }

        private GameObject CreateProjectileObject(SpellDatabase.SpellData data, ElementType element, Vector3 position, Quaternion rotation)
        {
            if (!useDebugPrimitiveProjectiles && data.prefab != null)
                return Instantiate(data.prefab, position, rotation);

            var projectileObject = new GameObject($"Spell_{data.spellId}_DebugShape");
            projectileObject.name = $"Spell_{data.spellId}";
            projectileObject.transform.SetPositionAndRotation(position, rotation);

            var collider = projectileObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = GetDebugColliderRadius(data.spellId) * debugProjectileScale;

            var rigidbody = projectileObject.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;

            CreateDebugProjectileVisual(data.spellId, element, projectileObject.transform);
            return projectileObject;
        }

        private void CreateDebugProjectileVisual(SpellId spellId, ElementType element, Transform parent)
        {
            var color = GetElementColor(element);
            switch (spellId)
            {
                case SpellId.Single_Pointer:
                    AddPrimitiveVisual("Pointer_Sphere", PrimitiveType.Sphere, parent, Vector3.zero, Vector3.one * 0.18f, color);
                    break;
                case SpellId.Single_Wave:
                    AddPrimitiveVisual("Wave_Plate", PrimitiveType.Cube, parent, Vector3.zero, new Vector3(0.48f, 0.08f, 0.18f), color);
                    AddPrimitiveVisual("Wave_Crest", PrimitiveType.Sphere, parent, new Vector3(0f, 0.08f, 0f), new Vector3(0.28f, 0.08f, 0.16f), Color.Lerp(color, Color.white, 0.35f));
                    break;
                case SpellId.Single_Strike:
                    AddPrimitiveVisual("Strike_Capsule", PrimitiveType.Capsule, parent, Vector3.zero, new Vector3(0.18f, 0.38f, 0.18f), color);
                    parent.GetChild(parent.childCount - 1).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case SpellId.Combo_FireIce:
                    AddPrimitiveVisual("FireIce_LeftOrb", PrimitiveType.Sphere, parent, new Vector3(-0.13f, 0f, 0f), Vector3.one * 0.18f, GetElementColor(ElementType.Fire));
                    AddPrimitiveVisual("FireIce_RightOrb", PrimitiveType.Sphere, parent, new Vector3(0.13f, 0f, 0f), Vector3.one * 0.18f, GetElementColor(ElementType.Ice));
                    break;
                case SpellId.Combo_IceThunder:
                    AddPrimitiveVisual("IceThunder_Cylinder", PrimitiveType.Cylinder, parent, Vector3.zero, new Vector3(0.24f, 0.16f, 0.24f), Color.Lerp(GetElementColor(ElementType.Ice), GetElementColor(ElementType.Thunder), 0.5f));
                    parent.GetChild(parent.childCount - 1).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case SpellId.Combo_ThunderFire:
                    AddPrimitiveVisual("ThunderFire_Diamond", PrimitiveType.Cube, parent, Vector3.zero, Vector3.one * 0.28f, Color.Lerp(GetElementColor(ElementType.Thunder), GetElementColor(ElementType.Fire), 0.45f));
                    parent.GetChild(parent.childCount - 1).localRotation = Quaternion.Euler(45f, 45f, 0f);
                    break;
                default:
                    AddPrimitiveVisual("Fallback_Sphere", PrimitiveType.Sphere, parent, Vector3.zero, Vector3.one * 0.18f, color);
                    break;
            }

            var light = parent.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = IsSingleSpell(spellId) ? 1.4f : 2.2f;
            light.intensity = IsSingleSpell(spellId) ? 1.6f : 2.6f;
        }

        private void AddPrimitiveVisual(string name, PrimitiveType primitiveType, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
        {
            var visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale * debugProjectileScale;

            var collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = CreateDebugMaterial(color);
        }

        private static Material CreateDebugMaterial(Color color)
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
            else if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            return material;
        }

        private static Color GetElementColor(ElementType element)
        {
            return element switch
            {
                ElementType.Fire => new Color(1f, 0.22f, 0.06f, 1f),
                ElementType.Ice => new Color(0.24f, 0.78f, 1f, 1f),
                ElementType.Thunder => new Color(1f, 0.88f, 0.12f, 1f),
                _ => new Color(0.75f, 0.75f, 0.85f, 1f)
            };
        }

        private static float GetDebugColliderRadius(SpellId spellId)
        {
            return IsSingleSpell(spellId) ? 0.22f : 0.34f;
        }

        private static bool IsSingleSpell(SpellId spellId)
        {
            return spellId == SpellId.Single_Pointer ||
                   spellId == SpellId.Single_Wave ||
                   spellId == SpellId.Single_Strike;
        }
    }
}
