using System;
using ArcaneVR.Combat;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Handles Hand Pull movement: player extends hand forward then pulls back to move. Disabled during ConstraintController active state.
    /// </summary>
    public class MovementController : MonoBehaviour
    {
        [SerializeField] private GestureDetector gestureDetector;
        [SerializeField] private GestureEventRouter gestureRouter;
        [SerializeField] private ConstraintController constraintController;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform dominantHandTransform;
        [SerializeField] private Transform leftHandAnchor;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private bool useLeftFistPullMovement = true;
        [SerializeField] private float moveMultiplier = 2.5f;
        [SerializeField] private float extendDistance = 0.45f;
        [SerializeField] private float pullActivationDistance = 0.08f;

        public bool IsEnabled { get; set; } = true;
        public bool IsGrabbing => isGrabbing;
        public float maxSpeed = 3f;

        private bool isIndexPointing;
        private bool isPrimed;
        private bool isGrabbing;
        private bool detectorEventsSubscribed;
        private bool routerEventsSubscribed;
        private float farthestForwardDistance;
        private Vector3 lastHandPosition;

        public void ConfigureLeftFistPrototype(GestureDetector detector, Transform rigRoot, Transform leftHand)
        {
            ConfigureLeftFistPrototype(detector, gestureRouter, rigRoot, leftHand);
        }

        public void ConfigureLeftFistPrototype(
            GestureDetector detector,
            GestureEventRouter router,
            Transform rigRoot,
            Transform leftHand)
        {
            if (gestureDetector != detector || gestureRouter != router)
                UnsubscribeGestureSources();

            gestureDetector = detector;
            gestureRouter = router;
            playerRoot = rigRoot != null ? rigRoot : playerRoot;
            leftHandAnchor = leftHand;
            useLeftFistPullMovement = true;

            if (characterController == null && playerRoot != null)
                characterController = playerRoot.GetComponent<CharacterController>();

            SubscribeGestureSources();
        }

        private void Awake()
        {
            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;

            if (playerRoot == null)
                playerRoot = transform;

            if (characterController == null && playerRoot != null)
                characterController = playerRoot.GetComponent<CharacterController>();
        }

        private void OnValidate()
        {
            if (playerRoot == null)
                playerRoot = transform;

            if (characterController == null && playerRoot != null)
                characterController = playerRoot.GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            SubscribeGestureSources();

            if (constraintController != null)
            {
                constraintController.OnConstraintStarted += HandleConstraintStart;
                constraintController.OnConstraintEnded += HandleConstraintEnd;
            }
        }

        private void OnDisable()
        {
            UnsubscribeGestureSources();

            if (constraintController != null)
            {
                constraintController.OnConstraintStarted -= HandleConstraintStart;
                constraintController.OnConstraintEnded -= HandleConstraintEnd;
            }
        }

        private void SubscribeGestureSources()
        {
            if (!detectorEventsSubscribed)
            {
                if (gestureDetector != null)
                {
                    gestureDetector.OnPoseDetected += HandlePoseDetected;
                    gestureDetector.OnLeftFistStart += StartGrab;
                    gestureDetector.OnLeftFistEnd += EndGrab;
                    detectorEventsSubscribed = true;
                }
            }

            if (!routerEventsSubscribed)
            {
                if (gestureRouter != null)
                {
                    gestureRouter.OnLeftFistStart += StartGrab;
                    gestureRouter.OnLeftFistEnd += EndGrab;
                    routerEventsSubscribed = true;
                }
            }
        }

        private void UnsubscribeGestureSources()
        {
            if (detectorEventsSubscribed && gestureDetector != null)
            {
                gestureDetector.OnPoseDetected -= HandlePoseDetected;
                gestureDetector.OnLeftFistStart -= StartGrab;
                gestureDetector.OnLeftFistEnd -= EndGrab;
            }

            if (routerEventsSubscribed && gestureRouter != null)
            {
                gestureRouter.OnLeftFistStart -= StartGrab;
                gestureRouter.OnLeftFistEnd -= EndGrab;
            }

            detectorEventsSubscribed = false;
            routerEventsSubscribed = false;
        }

        private void Update()
        {
            if (useLeftFistPullMovement)
            {
                UpdateLeftFistPullMovement();
                return;
            }

            if (!IsEnabled || isIndexPointing == false || dominantHandTransform == null || headTransform == null)
                return;

            var handPosition = dominantHandTransform.position;
            var headToHand = handPosition - headTransform.position;
            var forwardDistance = Vector3.Dot(headToHand, headTransform.forward);

            if (!isPrimed && forwardDistance >= extendDistance)
            {
                isPrimed = true;
                farthestForwardDistance = forwardDistance;
                lastHandPosition = handPosition;
                return;
            }

            if (!isPrimed)
                return;

            farthestForwardDistance = Mathf.Max(farthestForwardDistance, forwardDistance);
            var pullDistance = farthestForwardDistance - forwardDistance;
            var handVelocity = Time.deltaTime > 0f ? (lastHandPosition - handPosition).magnitude / Time.deltaTime : 0f;
            lastHandPosition = handPosition;

            if (pullDistance < pullActivationDistance)
                return;

            var speed = Mathf.Clamp(handVelocity, 0f, maxSpeed);
            var move = headTransform.forward * (speed * Time.deltaTime);

            if (characterController != null && characterController.enabled)
                characterController.Move(move);
            else if (playerRoot != null)
                playerRoot.position += move;
        }

        private void HandlePoseDetected(PoseId left, PoseId right)
        {
            isIndexPointing = right == PoseId.IndexPoint;
            if (isIndexPointing)
                return;

            isPrimed = false;
            farthestForwardDistance = 0f;
        }

        private void HandleConstraintStart()
        {
            IsEnabled = false;
            isPrimed = false;
            isGrabbing = false;
        }

        private void HandleConstraintEnd()
        {
            IsEnabled = true;
        }

        private void StartGrab()
        {
            if (leftHandAnchor == null)
                return;

            isGrabbing = true;
            lastHandPosition = leftHandAnchor.position;
        }

        private void EndGrab()
        {
            isGrabbing = false;
        }

        private void UpdateLeftFistPullMovement()
        {
            if (!IsEnabled || !isGrabbing)
                return;

            if (playerRoot == null)
                playerRoot = transform;

            if (leftHandAnchor == null || Time.deltaTime <= 0f)
                return;

            var handPosition = leftHandAnchor.position;
            var handDelta = handPosition - lastHandPosition;
            lastHandPosition = handPosition;

            var moveDirection = new Vector3(-handDelta.x, 0f, -handDelta.z);
            var velocity = moveDirection * (moveMultiplier / Time.deltaTime);
            if (velocity.magnitude > maxSpeed)
                velocity = velocity.normalized * maxSpeed;

            var move = velocity * Time.deltaTime;
            if (characterController != null && characterController.enabled)
                characterController.Move(move);
            else if (playerRoot != null)
                playerRoot.position += move;
        }

    }
}
