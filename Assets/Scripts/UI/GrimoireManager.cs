using System;
using ArcaneVR.Input;
using ArcaneVR.Spell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ArcaneVR.UI
{
    /// <summary>
    /// Handles grimoire summoning and dismissal. Keeps the original script GUID while supporting the richer grimoire scene setup.
    /// </summary>
    public class GrimoireManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject grimoireCanvas;
        [SerializeField] private GrimoireUI grimoireUI;
        [SerializeField] private Transform playerCamera;
        [SerializeField] private GestureDetector gestureDetector;
        [SerializeField] private GestureEventRouter gestureRouter;

        [Header("Input")]
        [SerializeField] private InputActionReference toggleAction;

        [Header("Settings")]
        [SerializeField] private float spawnDistance = 0.5f;
        [SerializeField] private float spawnHeightOffset = -0.2f;
        [SerializeField] private bool useRuntimeHandBook = true;
        [SerializeField] private Vector3 leftHandBookLocalPosition = new Vector3(0f, 0.055f, 0.085f);
        [SerializeField] private Vector3 leftHandBookLocalEuler = new Vector3(70f, 0f, 0f);
        [SerializeField] private float leftHandBookScale = 0.9f;

        [Header("Gesture Control")]
        [SerializeField] private bool enableGestureControl = true;
        [SerializeField] private bool requireLeftPalmFacingPlayer = true;
        [SerializeField] private float openPalmHoldDuration = 0.6f;
        [SerializeField] private float closeFistHoldDuration = 0.18f;
        [SerializeField] private float palmFacingDotThreshold = 0.58f;
        [SerializeField] private float toggleCooldown = 0.65f;

        [Header("Page Turn")]
        [SerializeField] private bool enableRightHandPageSwipe = true;
        [SerializeField] private bool requireRightOpenPalmForPageSwipe = true;
        [SerializeField] private float pageSwipeDistance = 0.22f;
        [SerializeField] private float pageSwipeMaxDuration = 0.75f;
        [SerializeField] private float pageSwipeCooldown = 0.35f;
        [SerializeField] private float pageSwipeVerticalTolerance = 0.28f;

        [Header("Magic Lock")]
        [SerializeField] private bool suppressMagicWhileOpen = true;

        public event Action OnGrimoireOpen;
        public event Action OnGrimoireClose;

        public bool IsOpen { get; private set; }
        public string LastGrimoireStatus { get; private set; } = "Grimoire: idle";

        private OVRHand leftOvrHand;
        private OVRHand rightOvrHand;
        private float leftOpenHoldTimer;
        private float leftFistHoldTimer;
        private float lastToggleTime = -999f;
        private float lastPageTurnTime = -999f;
        private bool pageSwipeActive;
        private Vector3 pageSwipeStartLocal;
        private float pageSwipeStartTime;
        private GameObject fallbackGrimoireRoot;
        private TextMesh fallbackPageText;
        private TextMesh fallbackLeftPageText;
        private TextMesh fallbackRightPageText;
        private int fallbackPageIndex;
        private AudioSource feedbackAudioSource;
        private AudioClip openClip;
        private AudioClip closeClip;
        private AudioClip pageClip;
        private bool magicSuppressionApplied;
        private float nextMagicSuppressionRefreshTime;
        private Transform currentBookAnchor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForArcaneScenes()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (!IsArcaneScene(sceneName) || FindAnyObjectByType<GrimoireManager>() != null)
                return;

            var host = GameObject.Find("GrimoireSystem") ??
                       GameObject.Find("BattleManager") ??
                       GameObject.Find("Arcane Test Hub") ??
                       new GameObject("GrimoireSystem");
            host.AddComponent<GrimoireManager>();
        }

        private static bool IsArcaneScene(string sceneName)
        {
            return sceneName == "Main" ||
                   sceneName == "DogeTest" ||
                   sceneName == "DodgeTest" ||
                   sceneName == "World" ||
                   sceneName == "FireColoseum" ||
                   sceneName == "IceColoseum" ||
                   sceneName == "ElectricColoseum";
        }

        private void Awake()
        {
            ResolveReferences();
            SetVisualActive(false);
            ApplyMagicSuppression(false);
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeGestureEvents();

            if (toggleAction != null)
            {
                toggleAction.action.Enable();
                toggleAction.action.performed += OnTogglePressed;
            }
        }

        private void OnDisable()
        {
            UnsubscribeGestureEvents();

            if (toggleAction != null)
                toggleAction.action.performed -= OnTogglePressed;

            ApplyMagicSuppression(false);
        }

        private void Update()
        {
            ResolveReferences();
            SubscribeGestureEvents();
            UpdateGestureControl();

            if (IsOpen)
            {
                PositionGrimoire();
                UpdatePageSwipe();
                ApplyMagicSuppression(true);
            }
            else
            {
                DisableSceneGrimoireCanvases();
            }
        }

        public void ToggleGrimoire()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        public void NextPage()
        {
            if (!IsOpen)
                return;

            if (grimoireUI != null)
                grimoireUI.NextPage();

            fallbackPageIndex = (fallbackPageIndex + 1) % 3;
            RefreshFallbackPage();
            PlayFeedback(pageClip ??= CreateFeedbackClip("ArcaneGrimoirePage", 520f, 660f, 0.16f, 0.22f), 0.55f);
            LastGrimoireStatus = "Grimoire: next page";
        }

        public void PreviousPage()
        {
            if (!IsOpen)
                return;

            if (grimoireUI != null)
                grimoireUI.PreviousPage();

            fallbackPageIndex = (fallbackPageIndex + 2) % 3;
            RefreshFallbackPage();
            PlayFeedback(pageClip ??= CreateFeedbackClip("ArcaneGrimoirePage", 520f, 660f, 0.16f, 0.22f), 0.55f);
            LastGrimoireStatus = "Grimoire: previous page";
        }

        public void Open()
        {
            if (IsOpen)
                return;

            ResolveReferences();
            IsOpen = true;
            SetVisualActive(true);
            PositionGrimoire();
            ApplyMagicSuppression(true);
            PlayFeedback(openClip ??= CreateFeedbackClip("ArcaneGrimoireOpen", 360f, 720f, 0.28f, 0.24f), 0.65f);

            LastGrimoireStatus = "Grimoire: open";
            OnGrimoireOpen?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            SetVisualActive(false);
            ApplyMagicSuppression(false);
            ResetGestureTimers();
            PlayFeedback(closeClip ??= CreateFeedbackClip("ArcaneGrimoireClose", 680f, 260f, 0.22f, 0.22f), 0.55f);

            LastGrimoireStatus = "Grimoire: close";
            OnGrimoireClose?.Invoke();
        }

        private void OnTogglePressed(InputAction.CallbackContext context)
        {
            ToggleGrimoire();
        }

        private void PositionGrimoire()
        {
            var visualRoot = ResolveVisualRoot();
            if (visualRoot == null)
                return;

            if (useRuntimeHandBook)
            {
                var anchor = ResolveLeftHandBookAnchor();
                if (anchor == null)
                {
                    currentBookAnchor = null;
                    ParkVisualRoot(visualRoot.transform);
                    visualRoot.SetActive(false);
                    return;
                }

                if (!visualRoot.activeSelf)
                    visualRoot.SetActive(true);

                if (currentBookAnchor != anchor)
                {
                    currentBookAnchor = anchor;
                    visualRoot.transform.SetParent(currentBookAnchor, false);
                }

                visualRoot.transform.localPosition = leftHandBookLocalPosition;
                visualRoot.transform.localRotation = Quaternion.Euler(leftHandBookLocalEuler);
                visualRoot.transform.localScale = Vector3.one * leftHandBookScale;
                return;
            }

            if (playerCamera == null)
                return;

            var targetPosition = playerCamera.position + playerCamera.forward * spawnDistance;
            targetPosition.y += spawnHeightOffset;
            visualRoot.transform.position = targetPosition;

            var lookAtPosition = playerCamera.position;
            lookAtPosition.y = visualRoot.transform.position.y;
            visualRoot.transform.LookAt(lookAtPosition);
            visualRoot.transform.Rotate(0f, 180f, 0f);
        }

        private void ResolveReferences()
        {
            if (playerCamera == null && Camera.main != null)
                playerCamera = Camera.main.transform;

            if (grimoireUI == null)
            {
                foreach (var ui in FindObjectsByType<GrimoireUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (ui == null)
                        continue;

                    grimoireUI = ui;
                    if (grimoireCanvas == null)
                        grimoireCanvas = grimoireUI.gameObject;
                    break;
                }
            }

            if (grimoireCanvas == null)
            {
                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (canvas == null || !canvas.name.Contains("Grimoire"))
                        continue;

                    grimoireCanvas = canvas.gameObject;
                    break;
                }
            }

            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();

            if (gestureRouter == null)
                gestureRouter = FindAnyObjectByType<GestureEventRouter>();

            if (leftOvrHand == null)
                leftOvrHand = FindBestOvrHand(true);

            if (rightOvrHand == null)
                rightOvrHand = FindBestOvrHand(false);
        }

        private void SubscribeGestureEvents()
        {
            if (gestureDetector != null)
            {
                gestureDetector.OnGrimTrigger -= HandleLegacyGrimoireTrigger;
                gestureDetector.OnGrimTrigger += HandleLegacyGrimoireTrigger;
                gestureDetector.OnLeftFistStart -= HandleLeftFistStarted;
                gestureDetector.OnLeftFistStart += HandleLeftFistStarted;
            }

            if (gestureRouter != null)
            {
                gestureRouter.OnLeftFistStart -= HandleLeftFistStarted;
                gestureRouter.OnLeftFistStart += HandleLeftFistStarted;
            }
        }

        private void UnsubscribeGestureEvents()
        {
            if (gestureDetector != null)
            {
                gestureDetector.OnGrimTrigger -= HandleLegacyGrimoireTrigger;
                gestureDetector.OnLeftFistStart -= HandleLeftFistStarted;
            }

            if (gestureRouter != null)
                gestureRouter.OnLeftFistStart -= HandleLeftFistStarted;
        }

        private void HandleLegacyGrimoireTrigger()
        {
            if (!enableGestureControl || IsOpen || Time.time - lastToggleTime < toggleCooldown)
                return;

            if (IsLeftPalmFacingPlayer())
                OpenFromGesture("legacy open palm");
        }

        private void HandleLeftFistStarted()
        {
            if (!enableGestureControl || !IsOpen || Time.time - lastToggleTime < toggleCooldown)
                return;

            CloseFromGesture("left fist");
        }

        private void UpdateGestureControl()
        {
            if (!enableGestureControl)
                return;

            if (IsOpen)
            {
                UpdateCloseGesture();
                return;
            }

            UpdateOpenGesture();
        }

        private void UpdateOpenGesture()
        {
            if (!IsLeftOpenPalm() || !IsLeftPalmFacingPlayer())
            {
                leftOpenHoldTimer = 0f;
                return;
            }

            leftOpenHoldTimer += Time.deltaTime;
            LastGrimoireStatus = $"Grimoire: open hold {Mathf.Clamp01(leftOpenHoldTimer / Mathf.Max(0.01f, openPalmHoldDuration)):0.0}";
            if (leftOpenHoldTimer < openPalmHoldDuration || Time.time - lastToggleTime < toggleCooldown)
                return;

            OpenFromGesture("left palm facing player");
        }

        private void UpdateCloseGesture()
        {
            if (!IsLeftFist())
            {
                leftFistHoldTimer = 0f;
                return;
            }

            leftFistHoldTimer += Time.deltaTime;
            LastGrimoireStatus = $"Grimoire: close hold {Mathf.Clamp01(leftFistHoldTimer / Mathf.Max(0.01f, closeFistHoldDuration)):0.0}";
            if (leftFistHoldTimer < closeFistHoldDuration || Time.time - lastToggleTime < toggleCooldown)
                return;

            CloseFromGesture("left fist");
        }

        private void OpenFromGesture(string source)
        {
            lastToggleTime = Time.time;
            ResetGestureTimers();
            Open();
            LastGrimoireStatus = $"Grimoire: opened by {source}";
        }

        private void CloseFromGesture(string source)
        {
            lastToggleTime = Time.time;
            Close();
            LastGrimoireStatus = $"Grimoire: closed by {source}";
        }

        private void UpdatePageSwipe()
        {
            if (!enableRightHandPageSwipe || Time.time - lastPageTurnTime < pageSwipeCooldown)
                return;

            if (!IsRightPageSwipePose() || !TryGetHandLocalPosition(false, out var localPosition))
            {
                pageSwipeActive = false;
                return;
            }

            if (!IsInPageTurnZone(localPosition))
            {
                pageSwipeActive = false;
                return;
            }

            if (!pageSwipeActive)
            {
                pageSwipeActive = true;
                pageSwipeStartLocal = localPosition;
                pageSwipeStartTime = Time.time;
                return;
            }

            if (Time.time - pageSwipeStartTime > pageSwipeMaxDuration)
            {
                pageSwipeActive = false;
                return;
            }

            var delta = localPosition - pageSwipeStartLocal;
            if (Mathf.Abs(delta.y) > pageSwipeVerticalTolerance)
            {
                pageSwipeActive = false;
                return;
            }

            if (Mathf.Abs(delta.x) < pageSwipeDistance)
                return;

            if (delta.x < 0f)
                NextPage();
            else
                PreviousPage();

            lastPageTurnTime = Time.time;
            pageSwipeActive = false;
        }

        private bool IsLeftOpenPalm()
        {
            var detectorOpen = gestureDetector != null &&
                               (gestureDetector.CurrentLeftPrototypePose == PoseType.OpenPalm ||
                                gestureDetector.CurrentLeftPose == PoseId.OpenPalm);
            var routerOpen = gestureRouter != null && gestureRouter.CurrentLeftPose == PoseType.OpenPalm;
            return detectorOpen || routerOpen || IsOvrHandOpen(leftOvrHand);
        }

        private bool IsLeftFist()
        {
            var detectorFist = gestureDetector != null &&
                               (gestureDetector.CurrentLeftPrototypePose == PoseType.Fist ||
                                gestureDetector.CurrentLeftPose == PoseId.Fist ||
                                gestureDetector.CurrentLeftPose == PoseId.FistPush);
            var routerFist = gestureRouter != null &&
                             (gestureRouter.LeftFistActive || gestureRouter.CurrentLeftPose == PoseType.Fist);
            return detectorFist || routerFist;
        }

        private bool IsRightPageSwipePose()
        {
            if (!requireRightOpenPalmForPageSwipe)
                return true;

            var detectorOpen = gestureDetector != null &&
                               (gestureDetector.CurrentRightPrototypePose == PoseType.OpenPalm ||
                                gestureDetector.CurrentRightPose == PoseId.OpenPalm);
            var routerOpen = gestureRouter != null && gestureRouter.CurrentRightPose == PoseType.OpenPalm;
            return detectorOpen || routerOpen;
        }

        private bool IsLeftPalmFacingPlayer()
        {
            if (!requireLeftPalmFacingPlayer)
                return true;

            if (playerCamera == null)
                return false;

            if (leftOvrHand == null)
                leftOvrHand = FindBestOvrHand(true);

            if (leftOvrHand == null || !leftOvrHand.IsTracked)
                return false;

            var handPose = leftOvrHand.PointerPose != null && leftOvrHand.IsPointerPoseValid
                ? leftOvrHand.PointerPose
                : leftOvrHand.transform;
            var toHead = playerCamera.position - handPose.position;
            if (toHead.sqrMagnitude <= 0.0001f)
                return false;

            var fromHead = handPose.position - playerCamera.position;
            if (Vector3.Dot(playerCamera.forward, fromHead.normalized) < 0.08f)
                return false;

            var localHandPosition = playerCamera.InverseTransformPoint(handPose.position);
            if (localHandPosition.z < 0.18f ||
                localHandPosition.z > 1.25f ||
                Mathf.Abs(localHandPosition.x) > 0.85f ||
                localHandPosition.y < -0.75f ||
                localHandPosition.y > 0.55f)
            {
                return false;
            }

            var palmTowardHeadDot = Vector3.Dot(handPose.up, toHead.normalized);
            var backOfHandTowardHeadDot = Vector3.Dot(-handPose.up, toHead.normalized);
            return palmTowardHeadDot >= palmFacingDotThreshold &&
                   backOfHandTowardHeadDot < 0.2f;
        }

        private static bool IsOvrHandOpen(OVRHand hand)
        {
            if (hand == null || !hand.IsTracked)
                return false;

            return hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) < 0.35f &&
                   hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle) < 0.35f &&
                   hand.GetFingerPinchStrength(OVRHand.HandFinger.Ring) < 0.45f &&
                   hand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky) < 0.45f;
        }

        private bool TryGetHandLocalPosition(bool leftHand, out Vector3 localPosition)
        {
            localPosition = Vector3.zero;
            if (playerCamera == null)
                return false;

            var hand = leftHand ? leftOvrHand : rightOvrHand;
            if (hand == null)
                hand = FindBestOvrHand(leftHand);

            if (hand == null || !hand.IsTracked || hand.PointerPose == null)
                return false;

            localPosition = playerCamera.InverseTransformPoint(hand.PointerPose.position);
            return true;
        }

        private bool IsInPageTurnZone(Vector3 localPosition)
        {
            return localPosition.z >= 0.18f &&
                   localPosition.z <= 1.15f &&
                   Mathf.Abs(localPosition.x) <= 0.9f &&
                   localPosition.y >= -0.75f &&
                   localPosition.y <= 0.35f;
        }

        private void ApplyMagicSuppression(bool suppress)
        {
            if (!suppressMagicWhileOpen)
                suppress = false;

            if (magicSuppressionApplied == suppress && Time.time < nextMagicSuppressionRefreshTime)
                return;

            magicSuppressionApplied = suppress;
            nextMagicSuppressionRefreshTime = Time.time + (suppress ? 1f : 0.25f);

            foreach (var caster in FindObjectsByType<SpellCaster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                caster.SetCastingSuppressed(suppress, "Grimoire");
        }

        private void SetVisualActive(bool active)
        {
            DisableSceneGrimoireCanvases();

            if (useRuntimeHandBook)
            {
                EnsureFallbackVisual();
                if (fallbackGrimoireRoot != null)
                    fallbackGrimoireRoot.SetActive(active);
                return;
            }

            if (grimoireCanvas != null)
            {
                grimoireCanvas.SetActive(active);
                return;
            }

            EnsureFallbackVisual();
            if (fallbackGrimoireRoot != null)
                fallbackGrimoireRoot.SetActive(active);
        }

        private GameObject ResolveVisualRoot()
        {
            if (useRuntimeHandBook)
            {
                EnsureFallbackVisual();
                return fallbackGrimoireRoot;
            }

            if (grimoireCanvas != null)
                return grimoireCanvas;

            EnsureFallbackVisual();
            return fallbackGrimoireRoot;
        }

        private Transform ResolveLeftHandBookAnchor()
        {
            if (leftOvrHand == null || !leftOvrHand.IsTracked)
                leftOvrHand = FindBestOvrHand(true);

            if (leftOvrHand == null || !leftOvrHand.IsTracked)
                return null;

            return leftOvrHand.PointerPose != null && leftOvrHand.IsPointerPoseValid
                ? leftOvrHand.PointerPose
                : leftOvrHand.transform;
        }

        private void DisableSceneGrimoireCanvases()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas == null || !canvas.name.Contains("Grimoire"))
                    continue;

                canvas.gameObject.SetActive(false);
            }

            if (grimoireCanvas != null && grimoireCanvas.GetComponent<Canvas>() != null)
                grimoireCanvas.SetActive(false);
        }

        private void EnsureFallbackVisual()
        {
            if (fallbackGrimoireRoot != null)
                return;

            fallbackGrimoireRoot = new GameObject("Runtime Hand Grimoire")
            {
                hideFlags = HideFlags.DontSave
            };
            ParkVisualRoot(fallbackGrimoireRoot.transform);
            fallbackGrimoireRoot.SetActive(false);

            var coverColor = new Color(0.07f, 0.028f, 0.018f, 1f);
            var coverEdgeColor = new Color(0.13f, 0.055f, 0.028f, 1f);
            var pageColor = new Color(0.86f, 0.76f, 0.55f, 1f);
            var pageShadowColor = new Color(0.55f, 0.44f, 0.27f, 1f);
            var goldColor = new Color(1f, 0.67f, 0.18f, 1f);
            var gemColor = new Color(0.18f, 0.72f, 1f, 1f);

            CreateBookPart("Left Cover", new Vector3(-0.105f, 0f, 0.014f), new Vector3(0.192f, 0.262f, 0.02f), coverColor, new Vector3(0f, 7f, 0f));
            CreateBookPart("Right Cover", new Vector3(0.105f, 0f, 0.014f), new Vector3(0.192f, 0.262f, 0.02f), coverColor, new Vector3(0f, -7f, 0f));
            CreateBookPart("Left Cover Edge", new Vector3(-0.191f, 0f, -0.004f), new Vector3(0.018f, 0.245f, 0.015f), coverEdgeColor, new Vector3(0f, 7f, 0f));
            CreateBookPart("Right Cover Edge", new Vector3(0.191f, 0f, -0.004f), new Vector3(0.018f, 0.245f, 0.015f), coverEdgeColor, new Vector3(0f, -7f, 0f));

            CreateBookPart("Left Page Block", new Vector3(-0.095f, 0f, -0.006f), new Vector3(0.164f, 0.224f, 0.012f), pageColor, new Vector3(0f, 5f, 0f));
            CreateBookPart("Right Page Block", new Vector3(0.095f, 0f, -0.006f), new Vector3(0.164f, 0.224f, 0.012f), pageColor, new Vector3(0f, -5f, 0f));
            CreateBookPart("Center Spine", new Vector3(0f, 0f, 0.004f), new Vector3(0.045f, 0.272f, 0.034f), new Color(0.035f, 0.018f, 0.012f, 1f));
            CreateBookPart("Spine Gold Band Upper", new Vector3(0f, 0.085f, -0.018f), new Vector3(0.054f, 0.012f, 0.01f), goldColor);
            CreateBookPart("Spine Gold Band Lower", new Vector3(0f, -0.085f, -0.018f), new Vector3(0.054f, 0.012f, 0.01f), goldColor);

            CreateBookPart("Left Page Stack", new Vector3(-0.183f, 0f, -0.016f), new Vector3(0.012f, 0.196f, 0.007f), pageShadowColor, new Vector3(0f, 5f, 0f));
            CreateBookPart("Right Page Stack", new Vector3(0.183f, 0f, -0.016f), new Vector3(0.012f, 0.196f, 0.007f), pageShadowColor, new Vector3(0f, -5f, 0f));

            CreateBookPart("Left Top Corner Metal", new Vector3(-0.175f, 0.108f, -0.022f), new Vector3(0.03f, 0.024f, 0.006f), goldColor, new Vector3(0f, 5f, 0f));
            CreateBookPart("Left Bottom Corner Metal", new Vector3(-0.175f, -0.108f, -0.022f), new Vector3(0.03f, 0.024f, 0.006f), goldColor, new Vector3(0f, 5f, 0f));
            CreateBookPart("Right Top Corner Metal", new Vector3(0.175f, 0.108f, -0.022f), new Vector3(0.03f, 0.024f, 0.006f), goldColor, new Vector3(0f, -5f, 0f));
            CreateBookPart("Right Bottom Corner Metal", new Vector3(0.175f, -0.108f, -0.022f), new Vector3(0.03f, 0.024f, 0.006f), goldColor, new Vector3(0f, -5f, 0f));

            CreateBookPart("Left Rune Line A", new Vector3(-0.095f, 0.052f, -0.024f), new Vector3(0.08f, 0.004f, 0.004f), goldColor, new Vector3(0f, 5f, 0f));
            CreateBookPart("Left Rune Line B", new Vector3(-0.095f, -0.052f, -0.024f), new Vector3(0.07f, 0.004f, 0.004f), goldColor, new Vector3(0f, 5f, 0f));
            CreateBookPart("Right Rune Line A", new Vector3(0.095f, 0.052f, -0.024f), new Vector3(0.08f, 0.004f, 0.004f), goldColor, new Vector3(0f, -5f, 0f));
            CreateBookPart("Right Rune Line B", new Vector3(0.095f, -0.052f, -0.024f), new Vector3(0.07f, 0.004f, 0.004f), goldColor, new Vector3(0f, -5f, 0f));

            CreateBookGem("Center Gem", new Vector3(0f, 0f, -0.034f), Vector3.one * 0.036f, gemColor);
            CreateBookLight("Center Gem Glow", new Vector3(0f, 0f, -0.055f), gemColor);

            fallbackPageText = CreateBookText("Runtime Grimoire Spine Text", new Vector3(0f, -0.002f, -0.045f), 0.0065f, 58, goldColor);
            fallbackLeftPageText = CreateBookText("Runtime Grimoire Left Text", new Vector3(-0.095f, 0.003f, -0.036f), 0.0075f, 58, new Color(0.30f, 0.18f, 0.07f, 1f));
            fallbackRightPageText = CreateBookText("Runtime Grimoire Right Text", new Vector3(0.095f, 0.003f, -0.036f), 0.0075f, 58, new Color(0.30f, 0.18f, 0.07f, 1f));

            RefreshFallbackPage();
            fallbackGrimoireRoot.SetActive(false);
        }

        private void ParkVisualRoot(Transform visualRoot)
        {
            if (visualRoot == null)
                return;

            visualRoot.SetParent(null, true);
            visualRoot.SetPositionAndRotation(ResolveParkingPosition(), Quaternion.identity);
            visualRoot.localScale = Vector3.one * leftHandBookScale;
        }

        private Vector3 ResolveParkingPosition()
        {
            if (playerCamera == null && Camera.main != null)
                playerCamera = Camera.main.transform;

            if (playerCamera != null)
                return playerCamera.position + playerCamera.forward * 0.6f + Vector3.down * 0.15f;

            var spawnPoint = GameObject.Find("PlayerSpawnPoint");
            if (spawnPoint != null)
                return spawnPoint.transform.position + Vector3.up * 1.2f;

            return Vector3.up * 1.6f;
        }

        private GameObject CreateBookPart(string objectName, Vector3 localPosition, Vector3 localScale, Color color)
        {
            return CreateBookPart(objectName, localPosition, localScale, color, Vector3.zero);
        }

        private GameObject CreateBookPart(string objectName, Vector3 localPosition, Vector3 localScale, Color color, Vector3 localEuler)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = objectName;
            part.transform.SetParent(fallbackGrimoireRoot.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;

            var collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = part.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = CreateRuntimeMaterial(color);

            return part;
        }

        private GameObject CreateBookGem(string objectName, Vector3 localPosition, Vector3 localScale, Color color)
        {
            var gem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gem.name = objectName;
            gem.transform.SetParent(fallbackGrimoireRoot.transform, false);
            gem.transform.localPosition = localPosition;
            gem.transform.localRotation = Quaternion.identity;
            gem.transform.localScale = localScale;

            var collider = gem.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = gem.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = CreateRuntimeMaterial(color);

            return gem;
        }

        private void CreateBookLight(string objectName, Vector3 localPosition, Color color)
        {
            var lightObject = new GameObject(objectName);
            lightObject.transform.SetParent(fallbackGrimoireRoot.transform, false);
            lightObject.transform.localPosition = localPosition;

            var pointLight = lightObject.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = color;
            pointLight.range = 0.28f;
            pointLight.intensity = 0.45f;
        }

        private TextMesh CreateBookText(string objectName, Vector3 localPosition, float localScale, int fontSize, Color color)
        {
            var textObject = new GameObject(objectName);
            textObject.transform.SetParent(fallbackGrimoireRoot.transform, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.identity;
            textObject.transform.localScale = Vector3.one * localScale;

            var textMesh = textObject.AddComponent<TextMesh>();
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = fontSize;
            textMesh.characterSize = 0.1f;
            textMesh.color = color;
            return textMesh;
        }

        private void RefreshFallbackPage()
        {
            if (fallbackPageText == null && fallbackLeftPageText == null && fallbackRightPageText == null)
                return;

            var pageName = fallbackPageIndex switch
            {
                0 => "FIRE",
                1 => "ICE",
                _ => "THUNDER"
            };

            var pageDescription = fallbackPageIndex switch
            {
                0 => "Burn\nIgnis",
                1 => "Slow\nGlacies",
                _ => "Stagger\nFulgur"
            };

            if (fallbackPageText != null)
                fallbackPageText.text = "ARCANE\nCODEX";

            if (fallbackLeftPageText != null)
                fallbackLeftPageText.text = $"{pageName}\n\n{pageDescription}";

            if (fallbackRightPageText != null)
                fallbackRightPageText.text = $"PAGE\n{fallbackPageIndex + 1}/3\n\nSPELL\nNOTES";
        }

        private void PlayFeedback(AudioClip clip, float volume)
        {
            if (clip == null)
                return;

            if (feedbackAudioSource == null)
            {
                feedbackAudioSource = GetComponent<AudioSource>();
                if (feedbackAudioSource == null)
                    feedbackAudioSource = gameObject.AddComponent<AudioSource>();

                feedbackAudioSource.playOnAwake = false;
                feedbackAudioSource.spatialBlend = 0.1f;
                feedbackAudioSource.dopplerLevel = 0f;
                feedbackAudioSource.ignoreListenerPause = true;
                feedbackAudioSource.volume = 1f;
            }

            feedbackAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void ResetGestureTimers()
        {
            leftOpenHoldTimer = 0f;
            leftFistHoldTimer = 0f;
            pageSwipeActive = false;
        }

        private static OVRHand FindBestOvrHand(bool leftHand)
        {
            var expected = leftHand ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
            foreach (var hand in FindObjectsByType<OVRHand>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hand != null && hand.GetHand() == expected && hand.enabled)
                    return hand;
            }

            return null;
        }

        private static Material CreateRuntimeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = "ArcaneRuntimeGrimoire",
                hideFlags = HideFlags.DontSave,
                color = color
            };

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            return material;
        }

        private static AudioClip CreateFeedbackClip(string clipName, float startFrequency, float endFrequency, float length, float gain)
        {
            const int sampleRate = 44100;
            var sampleCount = Mathf.CeilToInt(sampleRate * Mathf.Max(0.05f, length));
            var samples = new float[sampleCount];

            for (var i = 0; i < sampleCount; i++)
            {
                var t = (float)i / sampleRate;
                var n = sampleCount > 1 ? (float)i / (sampleCount - 1) : 0f;
                var frequency = Mathf.Lerp(startFrequency, endFrequency, Mathf.SmoothStep(0f, 1f, n));
                var envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(n)) * Mathf.Exp(-n * 1.5f);
                samples[i] = Mathf.Sin(t * frequency * Mathf.PI * 2f) * envelope * gain;
            }

            var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.hideFlags = HideFlags.DontSave;
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
