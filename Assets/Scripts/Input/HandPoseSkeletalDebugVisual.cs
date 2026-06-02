using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Draws finger bone LineRenderers for a ShapeRecognizerActiveState.
    /// Green = feature condition met, Red = not met. Mirrors SDK's HandShapeSkeletalDebugVisual
    /// but wires FingerFeatureStateProvider directly to avoid cross-prefab reference issues.
    /// </summary>
    public class HandPoseSkeletalDebugVisual : MonoBehaviour
    {
        [SerializeField, Interface(typeof(IHand))]
        private Object _hand;

        [SerializeField]
        private ShapeRecognizerActiveState _shapeRecognizerActiveState;

        [SerializeField]
        private FingerFeatureStateProvider _fingerFeatureStateProvider;

        [SerializeField]
        private Color _activeColor = Color.green;

        [SerializeField]
        private Color _inactiveColor = Color.red;

        [SerializeField]
        private float _lineWidth = 0.005f;

        [SerializeField]
        private Material _lineMaterial;

        private IHand Hand => _hand as IHand;

        private readonly List<FingerFeatureLineEntry> _entries = new();

        private struct FingerFeatureLineEntry
        {
            public LineRenderer Line;
            public HandFinger Finger;
            public ShapeRecognizer.FingerFeatureConfig Config;
            public IReadOnlyList<HandJointId> Joints;
        }

        private void Start()
        {
            if (_shapeRecognizerActiveState == null || _fingerFeatureStateProvider == null || Hand == null)
            {
                Debug.LogWarning($"[HandPoseSkeletalDebugVisual] Missing references on {gameObject.name}", this);
                enabled = false;
                return;
            }

            foreach (var shapeRecognizer in _shapeRecognizerActiveState.Shapes)
            {
                foreach (var (finger, configs) in shapeRecognizer.GetFingerFeatureConfigs())
                {
                    foreach (var config in configs)
                    {
                        var provider = _fingerFeatureStateProvider.GetValueProvider(finger);
                        var joints = provider.GetJointsAffected(finger, config.Feature);

                        var go = new GameObject($"DebugLine_{finger}_{config.Feature}");
                        go.transform.SetParent(transform, false);

                        var lr = go.AddComponent<LineRenderer>();
                        lr.useWorldSpace = true;
                        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        lr.receiveShadows = false;
                        lr.startWidth = _lineWidth;
                        lr.endWidth = _lineWidth;
                        if (_lineMaterial != null) lr.material = _lineMaterial;

                        _entries.Add(new FingerFeatureLineEntry
                        {
                            Line = lr,
                            Finger = finger,
                            Config = config,
                            Joints = joints
                        });
                    }
                }
            }
        }

        private void Update()
        {
            if (Hand == null || !Hand.IsTrackedDataValid)
            {
                foreach (var e in _entries) e.Line.enabled = false;
                return;
            }

            foreach (var e in _entries)
            {
                e.Line.enabled = true;
                e.Line.positionCount = e.Joints.Count;

                for (int i = 0; i < e.Joints.Count; i++)
                {
                    if (Hand.GetJointPose(e.Joints[i], out var pose))
                        e.Line.SetPosition(i, pose.position);
                }

                bool active = _fingerFeatureStateProvider.IsStateActive(
                    e.Finger, e.Config.Feature, e.Config.Mode, e.Config.State);

                var color = active ? _activeColor : _inactiveColor;
                e.Line.startColor = color;
                e.Line.endColor = color;
            }
        }

        private void OnDestroy()
        {
            foreach (var e in _entries)
            {
                if (e.Line != null) Destroy(e.Line.gameObject);
            }
            _entries.Clear();
        }
    }
}
