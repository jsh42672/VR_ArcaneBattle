using UnityEngine;
using System;

namespace ArcaneVR.Training
{
    public class Training_Waypoint : MonoBehaviour
    {
        public event Action<Training_Waypoint> OnReached;
        public string waypointID;
        public bool isGoal = false;

        [SerializeField] private Color inactiveColor = Color.gray;
        [SerializeField] private Color activeColor = Color.yellow;
        [SerializeField] private Color reachedColor = Color.green;

        private Renderer _renderer;
        private bool _isActive = false;
        private bool _isReached = false;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            SetState(false);
        }

        public void SetState(bool active)
        {
            _isActive = active;
            if (_renderer != null)
                _renderer.material.color = active ? activeColor : inactiveColor;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isActive || _isReached) return;

            if (other.CompareTag("Player") || other.name.Contains("XR Origin"))
            {
                _isReached = true;
                if (_renderer != null)
                    _renderer.material.color = reachedColor;
                OnReached?.Invoke(this);
            }
        }
    }
}
