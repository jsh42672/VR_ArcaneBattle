using System.Collections;
using ArcaneVR.Combat;
using ArcaneVR.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneVR.Boss
{
    [DefaultExecutionOrder(60)]
    public class BossBattleRuntimeBinder : MonoBehaviour
    {
        private const float SpawnBackDistance = 6f;
        private const float DesiredHeadHeightAboveGround = 1.65f;
        private bool spawnAligned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureForActiveScene()
        {
            EnsureForScene(SceneManager.GetActiveScene().name);
        }

        private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene.name);
        }

        private static void EnsureForScene(string sceneName)
        {
            if (!IsBattleScene(sceneName))
                return;

            if (FindAnyObjectByType<BossBattleRuntimeBinder>() != null)
                return;

            var host = GameObject.Find("BattleManager") ?? new GameObject("BattleManager");
            host.AddComponent<BossBattleRuntimeBinder>();
        }

        private static bool IsBattleScene(string sceneName)
        {
            return sceneName == "ElectricColoseum" ||
                   sceneName == "FireColoseum" ||
                   sceneName == "IceColoseum";
        }

        private void Start()
        {
            StartCoroutine(BindWhenSceneIsReady());
        }

        private void Update()
        {
            EnsureBossRuntime();
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
            var golemTarget = ResolveOrCreateGolemTarget();
            if (golemTarget == null)
                return;

            EnsureBossPhysics(golemTarget);

            if (FindAnyObjectByType<BossAI>() == null)
                golemTarget.gameObject.AddComponent<BossAI>();

            if (FindAnyObjectByType<BossStateMachine>() == null)
                golemTarget.gameObject.AddComponent<BossStateMachine>();

            var chase = BossChaseController.EnsureForTarget(golemTarget);
            if (chase != null)
                chase.ApplyPresentationDefaults();
        }

        private static GolemCombatTarget ResolveOrCreateGolemTarget()
        {
            var existing = FindAnyObjectByType<GolemCombatTarget>();
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
