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
    }
}
