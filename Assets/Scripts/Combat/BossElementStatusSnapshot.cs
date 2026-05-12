using System;

namespace ArcaneVR.Combat
{
    [Serializable]
    public struct BossElementStatusSnapshot
    {
        public float currentHealth;
        public float maxHealth;
        public bool isBarrierActive;
        public bool isWeakExposed;
        public bool isSlowed;
        public bool isBurning;
        public bool isStaggered;
        public bool isChargeCounterWindowOpen;
        public float receivedDamageMultiplier;
        public float movementSpeedMultiplier;
        public float actionSpeedMultiplier;
        public bool canAct;
        public float barrierRemaining;
        public float weakRemaining;
        public float slowRemaining;
        public float burnRemaining;
        public float staggerRemaining;
        public float chargeCounterRemaining;
        public string combatCue;
    }
}
