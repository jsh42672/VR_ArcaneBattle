using UnityEngine;

namespace ArcaneVR.Core
{
    /// <summary>
    /// Centralizes player rig lookup so battle/world code can prefer the working
    /// Main-scene Meta XR hand rig while still accepting older XR Origin scenes.
    /// </summary>
    public static class ArcanePlayerRigResolver
    {
        private const string OvrCameraRigName = "OVRCameraRig";
        private const string XrOriginName = "XR Origin";
        private const string XrOriginCameraRigName = "XROriginCameraRig";

        public static GameObject FindPlayerRigGameObject()
        {
            var transform = FindPlayerRigTransform();
            return transform != null ? transform.gameObject : null;
        }

        public static Transform FindPlayerRigTransform()
        {
            var cameraRig = FindOvrCameraRig();
            if (cameraRig != null)
                return cameraRig.transform;

            var namedRig = GameObject.Find(XrOriginName);
            if (namedRig != null)
                return namedRig.transform;

            namedRig = GameObject.Find(XrOriginCameraRigName);
            if (namedRig != null)
                return namedRig.transform;

            var taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            return taggedPlayer != null ? taggedPlayer.transform : null;
        }

        public static Transform FindHeadTransform()
        {
            if (Camera.main != null)
                return Camera.main.transform;

            var rig = FindPlayerRigTransform();
            if (rig == null)
                return null;

            var centerEye = rig.Find("TrackingSpace/CenterEyeAnchor");
            if (centerEye != null)
                return centerEye;

            centerEye = rig.Find("Camera Offset/Main Camera");
            return centerEye != null ? centerEye : rig;
        }

        public static Transform FindHeadTransform(Transform preferredReference)
        {
            var preferredRig = FindRigTransform(preferredReference);
            var preferredHead = FindHeadTransformOnRig(preferredRig);
            if (preferredHead != null)
                return preferredHead;

            return FindHeadTransform();
        }

        public static Transform FindHandTransform(bool isLeft, Transform preferredReference = null)
        {
            var preferredRig = FindRigTransform(preferredReference);
            var preferredHand = FindHandTransformOnRig(preferredRig, isLeft);
            if (preferredHand != null)
                return preferredHand;

            var ovrRig = FindOvrCameraRig();
            if (ovrRig != null)
            {
                ovrRig.EnsureGameObjectIntegrity();
                var ovrAnchor = isLeft ? ovrRig.leftHandAnchor : ovrRig.rightHandAnchor;
                if (ovrAnchor != null)
                    return ovrAnchor;
            }

            var wristName = isLeft ? "L_Wrist" : "R_Wrist";
            return GameObject.Find(wristName)?.transform;
        }

        public static bool ShareResolvedRig(Transform a, Transform b)
        {
            if (a == null || b == null)
                return false;

            return FindRigTransform(a) == FindRigTransform(b);
        }

        public static bool IsPlayerCollider(Collider other)
        {
            if (other == null)
                return false;

            if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
                return true;

            if (other.GetComponentInParent<CharacterController>() != null)
                return true;

            if (other.GetComponentInParent<OVRCameraRig>() != null)
                return true;

            var rootName = other.transform.root.name;
            return rootName.Contains(OvrCameraRigName) ||
                   rootName.Contains(XrOriginName) ||
                   rootName.Contains(XrOriginCameraRigName);
        }

        private static OVRCameraRig FindOvrCameraRig()
        {
            if (Camera.main != null)
            {
                var cameraRig = Camera.main.GetComponentInParent<OVRCameraRig>();
                if (cameraRig != null)
                    return cameraRig;
            }

            var namedRig = GameObject.Find(OvrCameraRigName);
            if (namedRig != null && namedRig.TryGetComponent(out OVRCameraRig ovrRig))
                return ovrRig;

            return Object.FindAnyObjectByType<OVRCameraRig>();
        }

        private static Transform FindRigTransform(Transform transform)
        {
            if (transform == null)
                return null;

            var ovrRig = transform.GetComponentInParent<OVRCameraRig>();
            if (ovrRig != null)
                return ovrRig.transform;

            var current = transform;
            while (current != null)
            {
                if (current.name is XrOriginName or XrOriginCameraRigName)
                    return current;

                current = current.parent;
            }

            return transform.root;
        }

        private static Transform FindHeadTransformOnRig(Transform rig)
        {
            if (rig == null)
                return null;

            if (rig.TryGetComponent(out OVRCameraRig ovrRig))
            {
                ovrRig.EnsureGameObjectIntegrity();
                return ovrRig.centerEyeAnchor != null
                    ? ovrRig.centerEyeAnchor
                    : rig.Find("TrackingSpace/CenterEyeAnchor");
            }

            var xrCamera = rig.Find("Camera Offset/Main Camera");
            if (xrCamera != null)
                return xrCamera;

            return rig.Find("TrackingSpace/CenterEyeAnchor");
        }

        private static Transform FindHandTransformOnRig(Transform rig, bool isLeft)
        {
            if (rig == null)
                return null;

            if (rig.TryGetComponent(out OVRCameraRig ovrRig))
            {
                ovrRig.EnsureGameObjectIntegrity();
                return isLeft ? ovrRig.leftHandAnchor : ovrRig.rightHandAnchor;
            }

            var directPath = isLeft ? "Left Hand Tracking/L_Wrist" : "Right Hand Tracking/R_Wrist";
            var wrist = rig.Find(directPath);
            return wrist != null ? wrist : FindDescendantByName(rig, isLeft ? "L_Wrist" : "R_Wrist");
        }

        private static Transform FindDescendantByName(Transform root, string childName)
        {
            if (root == null)
                return null;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.name == childName)
                    return child;
            }

            return null;
        }
    }
}
