using System;
using UnityEngine;

namespace ArcaneVR.Boss
{
    /// <summary>
    /// Controls boss behavior. Selects attack patterns and delegates state transitions to BossStateMachine. Fires OnStateChanged event.
    /// </summary>
    public class BossAI : MonoBehaviour
    {
        public event Action<BossState> OnStateChanged;

        // TODO: Implement
    }
}
