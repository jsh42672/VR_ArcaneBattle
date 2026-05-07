using System;
using UnityEngine;

namespace ArcaneVR.UI
{
    /// <summary>
    /// Handles Grimoire summoning and dismissal. Manages three pages (Attributes, Skills, Golem Info). Fires OnGrimoireOpen and OnGrimoireClose events.
    /// </summary>
    public class GrimoireManager : MonoBehaviour
    {
        public event Action OnGrimoireOpen;
        public event Action OnGrimoireClose;

        public bool IsOpen { get; private set; }

        public void Open()
        {
            if (IsOpen)
                return;

            IsOpen = true;
            OnGrimoireOpen?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            OnGrimoireClose?.Invoke();
        }
    }
}
