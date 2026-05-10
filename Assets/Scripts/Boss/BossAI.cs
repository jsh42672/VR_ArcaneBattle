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

        public BossState CurrentState { get; private set; } = BossState.Idle;

        public void ChangeState(BossState nextState)
        {
            if (CurrentState == nextState)
                return;

            CurrentState = nextState;
            OnStateChanged?.Invoke(CurrentState);
        }

        public void BeginCharge()
        {
            ChangeState(BossState.Charging);
        }

        public void EnterDefense()
        {
            ChangeState(BossState.Defense);
        }

        public void ExposeWeakness()
        {
            ChangeState(BossState.Weakness);
        }

        public void Die()
        {
            ChangeState(BossState.Dead);
        }
    }
}
