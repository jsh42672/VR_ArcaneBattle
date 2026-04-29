using System;
using ArcaneVR.Spell;
using UnityEngine;

namespace ArcaneVR.Combat
{
    /// <summary>
    /// Unified combat handler. Processes spell-boss collision, applies elemental effects, manages player and boss HP. Fires OnPlayerHit and OnBossHit events.
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        public event Action<float> OnPlayerHit;
        public event Action<float, ElementType> OnBossHit;

        // TODO: Implement
    }
}
