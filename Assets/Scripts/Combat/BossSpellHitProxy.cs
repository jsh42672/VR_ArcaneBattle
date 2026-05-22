using ArcaneVR.Spell;
using UnityEngine;

namespace ArcaneVR.Combat
{
    public class BossSpellHitProxy : MonoBehaviour, ISpellTarget
    {
        [SerializeField] private GolemCombatTarget target;

        private void Reset()
        {
            ResolveTarget();
        }

        private void OnValidate()
        {
            ResolveTarget();
        }

        private void Awake()
        {
            ResolveTarget();
        }

        public void OnHit(SpellHitData hitData)
        {
            if (target == null)
            {
                Debug.LogWarning($"{nameof(BossSpellHitProxy)} on {name} has no {nameof(GolemCombatTarget)} target.", this);
                return;
            }

            target.OnHit(hitData);
        }

        private void ResolveTarget()
        {
            if (target == null)
                target = GetComponentInParent<GolemCombatTarget>();
        }
    }
}
