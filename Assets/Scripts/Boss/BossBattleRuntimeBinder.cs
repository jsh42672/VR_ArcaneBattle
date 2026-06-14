using System.Collections;
using ArcaneVR.Combat;
using ArcaneVR.Core;
using ArcaneVR.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.Boss
{
    [DefaultExecutionOrder(60)]
    public class BossBattleRuntimeBinder : MonoBehaviour
    {
        private const float SpawnBackDistance = 6f;
        private const float DesiredHeadHeightAboveGround = 1.65f;

        [Header("디버그 / 테스트")]
        [Tooltip("체크하면 골렘이 움직이거나 공격하지 않습니다.\n플레이 중에도 실시간으로 켜고 끌 수 있습니다.")]
        [SerializeField] private bool 골렘_이동_공격_정지;

        private bool spawnAligned;
        private bool _runtimeBound;
        private GolemCombatTarget _cachedGolemTarget;
        private BossChaseController _cachedChase;
        private BossStateMachine _cachedStateMachine;

        private void Start()
        {
            StartCoroutine(BindWhenSceneIsReady());
        }

        private void Update()
        {
            if (!_runtimeBound)
            {
                EnsureBossRuntime();
                return;
            }

            SyncDebugFreeze();
        }

        private void SyncDebugFreeze()
        {
            if (_cachedChase != null)
                _cachedChase.DebugFreeze = 골렘_이동_공격_정지;
            if (_cachedStateMachine != null)
                _cachedStateMachine.DebugFreeze = 골렘_이동_공격_정지;
        }

        private IEnumerator BindWhenSceneIsReady()
        {
            for (var i = 0; i < 45; i++)
            {
                EnsureBossRuntime();
                if (!spawnAligned)
                    TryAlignPlayerSpawn();

                yield return null;
            }
        }

        private void EnsureBossRuntime()
        {
            if (_cachedGolemTarget == null)
                _cachedGolemTarget = ResolveOrCreateGolemTarget();

            var golemTarget = _cachedGolemTarget;
            if (golemTarget == null)
                return;

            EnsureBossPhysics(golemTarget);
            EnsureBattleHelpers(golemTarget);

            if (FindAnyObjectByType<BossAI>(FindObjectsInactive.Include) == null)
                golemTarget.gameObject.AddComponent<BossAI>();

            if (FindAnyObjectByType<BossStateMachine>(FindObjectsInactive.Include) == null)
                golemTarget.gameObject.AddComponent<BossStateMachine>();

            _cachedChase = BossChaseController.EnsureForTarget(golemTarget);
            if (_cachedChase != null)
                _cachedChase.ApplyPresentationDefaults();

            _cachedStateMachine = FindAnyObjectByType<BossStateMachine>(FindObjectsInactive.Include);

            _runtimeBound = true;
            SyncDebugFreeze();
        }

        private static void EnsureBattleHelpers(GolemCombatTarget golemTarget)
        {
            if (golemTarget == null)
                return;

            var combatManager = FindAnyObjectByType<CombatManager>();
            var feedbackManager = FindAnyObjectByType<FeedbackManager>();

            var patternHost = FindSceneObject("BossPatternBridge") ??
                              FindOrCreateScenePath("ArcanePlayerRig", "GameSystems", "CombatSystems", "BossPatternBridge");
            var attackHost = FindSceneObject("BossAttackControllers") ??
                             FindOrCreateScenePath("ArcanePlayerRig", "GameSystems", "CombatSystems", "BossAttackControllers");
            var uiFeedbackHost = feedbackManager != null
                ? feedbackManager.gameObject
                : FindOrCreateScenePath("ArcanePlayerRig", "GameSystems", "UIManagers", "FeedbackManager");
            var uiHealthHost = FindSceneObject("BossHealthBarUI") ??
                               FindOrCreateScenePath("ArcanePlayerRig", "GameSystems", "UIManagers", "BossHealthBarUI");

            EnsureComponent(patternHost, () => patternHost.AddComponent<BossPatternCombatBridge>());
            EnsureComponent(attackHost, () => attackHost.AddComponent<BossAttackTelegraphController>());
            EnsureComponent(attackHost, () => attackHost.AddComponent<BossAttackEffectController>());
            EnsureComponent(attackHost, () => attackHost.AddComponent<BossAttackAnimatorBridge>());
            EnsureComponent(attackHost, () => attackHost.AddComponent<BossCombatFeedbackController>());
            EnsureComponent(attackHost, () => attackHost.AddComponent<DodgePlayerDamageBridge>());
            EnsureComponent(attackHost, () => attackHost.AddComponent<BarrierPlayerDamageBridge>());
            EnsureComponent(attackHost, () => attackHost.AddComponent<BarrierVisualController>());
            if (feedbackManager == null)
                EnsureComponent(uiFeedbackHost, () => uiFeedbackHost.AddComponent<FeedbackManager>());
            EnsureComponent(uiHealthHost, () => uiHealthHost.AddComponent<BossHealthBarUI>());
            EnsureComponent(golemTarget.gameObject, () => golemTarget.gameObject.AddComponent<BossElementStatusBridge>());
            EnsureComponent(golemTarget.gameObject, () => golemTarget.gameObject.AddComponent<BossElementStatusVfx>());
            EnsureComponent(golemTarget.gameObject, () => golemTarget.gameObject.AddComponent<LightningAuraController>());
        }

        private static GolemCombatTarget ResolveOrCreateGolemTarget()
        {
            var existing = FindAnyObjectByType<GolemCombatTarget>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            var candidate = FindSceneObject("attack_golemn") ??
                            FindSceneObject("Golem_Placeholder") ??
                            FindSceneObject("GolemPlaceholder") ??
                            FindSceneObject("Golem") ??
                            FindObjectByNamePart("golem") ??
                            FindObjectByNamePart("boss");

            return candidate != null
                ? candidate.GetComponent<GolemCombatTarget>() ?? candidate.AddComponent<GolemCombatTarget>()
                : null;
        }

        private static GameObject FindSceneObject(string objectName)
        {
            var direct = GameObject.Find(objectName);
            if (direct != null)
                return direct;

            var activeScene = SceneManager.GetActiveScene();
            foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform == null ||
                    transform.gameObject.scene != activeScene ||
                    transform.hideFlags != HideFlags.None ||
                    transform.name != objectName)
                {
                    continue;
                }

                return transform.gameObject;
            }

            return null;
        }

        private static GameObject FindObjectByNamePart(string namePart)
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform == null ||
                    transform.gameObject.scene != activeScene ||
                    transform.hideFlags != HideFlags.None ||
                    !transform.name.ToLowerInvariant().Contains(namePart))
                {
                    continue;
                }

                return transform.gameObject;
            }

            return null;
        }

        private static GameObject FindOrCreateScenePath(params string[] pathSegments)
        {
            if (pathSegments == null || pathSegments.Length == 0)
                return null;

            GameObject current = null;
            foreach (var segment in pathSegments)
            {
                if (string.IsNullOrEmpty(segment))
                    continue;

                var next = current == null
                    ? FindSceneObject(segment)
                    : FindChildByName(current.transform, segment);

                if (next == null)
                {
                    next = new GameObject(segment);
                    if (current != null)
                        next.transform.SetParent(current.transform, false);
                }

                current = next;
            }

            return current;
        }

        private static GameObject FindChildByName(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            foreach (Transform child in parent)
            {
                if (child != null && child.name == childName)
                    return child.gameObject;
            }

            return null;
        }

        private static T EnsureComponent<T>(GameObject host, System.Func<T> createComponent) where T : Component
        {
            var existing = FindAnyObjectByType<T>();
            if (existing != null)
                return existing;

            if (host == null || createComponent == null)
                return null;

            return host.GetComponent<T>() ?? createComponent();
        }

        private static void EnsureBossPhysics(GolemCombatTarget golemTarget)
        {
            if (golemTarget == null)
                return;

            if (golemTarget.GetComponentInChildren<Collider>(true) == null &&
                TryGetRendererBounds(golemTarget.transform, out var bounds))
            {
                var collider = golemTarget.gameObject.AddComponent<CapsuleCollider>();
                var scale = golemTarget.transform.lossyScale;
                var horizontalScale = Mathf.Max(0.001f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
                var verticalScale = Mathf.Max(0.001f, Mathf.Abs(scale.y));
                collider.center = golemTarget.transform.InverseTransformPoint(bounds.center);
                collider.radius = Mathf.Max(bounds.extents.x, bounds.extents.z) / horizontalScale;
                collider.height = Mathf.Max(collider.radius * 2f, bounds.size.y / verticalScale);
                collider.direction = 1;
            }

            var body = golemTarget.GetComponent<Rigidbody>();
            if (body == null)
                body = golemTarget.gameObject.AddComponent<Rigidbody>();

            body.isKinematic = true;
            body.useGravity = false;
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds(root.position, Vector3.zero);
            var initialized = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return initialized;
        }

        private void TryAlignPlayerSpawn()
        {
            var head = ArcanePlayerRigResolver.FindHeadTransform();
            var movementRoot = ResolvePlayerMovementRoot();
            if (head == null || movementRoot == null)
                return;

            var playerSpawnPoint = FindSceneObject("PlayerSpawnPoint");
            var desiredHeadPosition = playerSpawnPoint != null
                ? ResolveDesiredHeadPositionAtSpawnPoint(playerSpawnPoint.transform.position)
                : ResolveDesiredHeadPositionFromLegacyMarker();

            if (!desiredHeadPosition.HasValue)
                return;

            var desiredHead = desiredHeadPosition.Value;
            var horizontalDelta = Vector3.ProjectOnPlane(head.position - desiredHead, Vector3.up).magnitude;
            var verticalDelta = Mathf.Abs(head.position.y - desiredHead.y);
            if (horizontalDelta < 2.0f && verticalDelta < 0.35f)
            {
                spawnAligned = true;
                return;
            }

            movementRoot.position += desiredHead - head.position;
            spawnAligned = true;
        }

        private static Vector3? ResolveDesiredHeadPositionFromLegacyMarker()
        {
            var marker = FindSceneObject("CombatZone_Marker") ??
                         FindSceneObject("Portal_Exit");
            if (marker == null)
                return null;

            var golemTarget = ResolveOrCreateGolemTarget();
            var awayFromBoss = golemTarget != null
                ? marker.transform.position - golemTarget.transform.position
                : -marker.transform.forward;
            awayFromBoss.y = 0f;
            if (awayFromBoss.sqrMagnitude < 0.0001f)
                awayFromBoss = -marker.transform.forward;
            awayFromBoss.Normalize();

            return ResolveDesiredHeadPosition(marker.transform.position, awayFromBoss);
        }

        private static Vector3 ResolveDesiredHeadPositionAtSpawnPoint(Vector3 spawnPointPosition)
        {
            var basePosition = spawnPointPosition;
            if (TryResolveGroundHeight(basePosition, out var groundY))
                basePosition.y = groundY;

            return basePosition + Vector3.up * DesiredHeadHeightAboveGround;
        }

        private static Vector3 ResolveDesiredHeadPosition(Vector3 markerPosition, Vector3 awayFromBoss)
        {
            var basePosition = markerPosition + awayFromBoss * SpawnBackDistance;
            if (TryResolveGroundHeight(basePosition, out var groundY))
                basePosition.y = groundY;
            else
                basePosition.y = markerPosition.y;

            return basePosition + Vector3.up * DesiredHeadHeightAboveGround;
        }

        private static bool TryResolveGroundHeight(Vector3 position, out float groundY)
        {
            var origin = new Vector3(position.x, position.y + 30f, position.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
            var bestDistance = float.PositiveInfinity;
            var found = false;
            groundY = position.y;

            foreach (var hit in hits)
            {
                if (hit.collider == null ||
                    hit.collider.isTrigger ||
                    ArcanePlayerRigResolver.IsPlayerCollider(hit.collider) ||
                    hit.distance >= bestDistance ||
                    hit.normal.y < 0.35f)
                {
                    continue;
                }

                bestDistance = hit.distance;
                groundY = hit.point.y;
                found = true;
            }

            if (found)
                return true;

            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain == null || terrain.terrainData == null)
                    continue;

                var terrainPosition = terrain.transform.position;
                var terrainSize = terrain.terrainData.size;
                if (position.x < terrainPosition.x ||
                    position.z < terrainPosition.z ||
                    position.x > terrainPosition.x + terrainSize.x ||
                    position.z > terrainPosition.z + terrainSize.z)
                {
                    continue;
                }

                groundY = terrain.SampleHeight(position) + terrainPosition.y;
                return true;
            }

            return false;
        }

        private static Transform ResolvePlayerMovementRoot()
        {
            var head = ArcanePlayerRigResolver.FindHeadTransform();
            if (head != null)
            {
                var rig = head.GetComponentInParent<OVRCameraRig>();
                if (rig != null)
                {
                    rig.EnsureGameObjectIntegrity();
                    if (rig.trackingSpace != null)
                        return rig.trackingSpace;
                }
            }

            var rigRoot = ArcanePlayerRigResolver.FindPlayerRigTransform();
            if (rigRoot == null)
                return null;

            var ovrRig = rigRoot.GetComponent<OVRCameraRig>() ?? rigRoot.GetComponentInChildren<OVRCameraRig>(true);
            if (ovrRig != null)
            {
                ovrRig.EnsureGameObjectIntegrity();
                if (ovrRig.trackingSpace != null)
                    return ovrRig.trackingSpace;
            }

            var trackingSpace = rigRoot.Find("TrackingSpace");
            return trackingSpace != null ? trackingSpace : rigRoot;
        }
    }
}
