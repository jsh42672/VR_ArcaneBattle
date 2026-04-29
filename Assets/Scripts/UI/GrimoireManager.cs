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

        // TODO: Implement
    }
}
