using UnityEngine;

namespace ArcaneVR.Input
{
    public class GesturePrototypeAura : MonoBehaviour
    {
        [SerializeField] private bool enablePrototypeAura;
        [SerializeField] private GestureDetector gestureDetector;
        [SerializeField] private OVRHand hand;
        [SerializeField] private ParticleSystem aura;

        private bool subscribed;

        private void Awake()
        {
            if (!enablePrototypeAura)
                return;

            if (gestureDetector == null)
                gestureDetector = FindAnyObjectByType<GestureDetector>();

            EnsureAura();
        }

        private void OnEnable()
        {
            if (!enablePrototypeAura)
                return;

            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(GestureDetector detector, OVRHand targetHand)
        {
            if (!enablePrototypeAura)
            {
                DisableAndClear();
                return;
            }

            Unsubscribe();
            gestureDetector = detector;
            hand = targetHand;
            EnsureAura();
            Subscribe();
        }

        public void DisableAndClear()
        {
            Unsubscribe();
            enablePrototypeAura = false;

            if (aura == null)
                return;

            aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            aura.gameObject.SetActive(false);
        }

        private void Subscribe()
        {
            if (subscribed || gestureDetector == null)
                return;

            gestureDetector.OnHandPoseConfirmed += HandlePoseConfirmed;
            gestureDetector.OnHandPoseCleared += HandlePoseCleared;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || gestureDetector == null)
                return;

            gestureDetector.OnHandPoseConfirmed -= HandlePoseConfirmed;
            gestureDetector.OnHandPoseCleared -= HandlePoseCleared;
            subscribed = false;
        }

        private void EnsureAura()
        {
            if (aura != null || hand == null)
                return;

            var auraObject = new GameObject($"PrototypeAura_{hand.GetHand()}");
            auraObject.transform.SetParent(hand.transform, false);
            auraObject.transform.localPosition = Vector3.zero;
            auraObject.transform.localRotation = Quaternion.identity;

            aura = auraObject.AddComponent<ParticleSystem>();
            var main = aura.main;
            main.loop = true;
            main.startLifetime = 0.35f;
            main.startSpeed = 0.025f;
            main.startSize = 0.035f;
            main.maxParticles = 80;
            main.startColor = Color.black;

            var emission = aura.emission;
            emission.rateOverTime = 28f;

            var shape = aura.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.13f;

            var renderer = aura.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                             Shader.Find("Particles/Standard Unlit") ??
                             Shader.Find("Standard");
                renderer.material = new Material(shader);
            }

            aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void HandlePoseConfirmed(bool isLeft, PoseType pose)
        {
            if (!MatchesHand(isLeft))
                return;

            EnsureAura();
            if (aura == null)
                return;

            var main = aura.main;
            main.startColor = PoseToColor(pose);
            aura.Play();
        }

        private void HandlePoseCleared(bool isLeft)
        {
            if (!MatchesHand(isLeft) || aura == null)
                return;

            aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private bool MatchesHand(bool isLeft)
        {
            if (hand == null)
                return !isLeft;

            var expected = isLeft ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight;
            return hand.GetHand() == expected;
        }

        private static Color PoseToColor(PoseType pose)
        {
            return pose switch
            {
                PoseType.OpenPalm => new Color(1f, 0.2f, 0.2f, 1f),
                PoseType.TwoFinger => new Color(0.2f, 0.4f, 1f, 1f),
                PoseType.ThumbsUp => new Color(1f, 0.86f, 0f, 1f),
                _ => Color.black
            };
        }
    }
}
