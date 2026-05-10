using ArcaneVR.Spell;

namespace ArcaneVR.Combat
{
    /// <summary>
    /// Implement this on objects that can receive spell attribute hit data.
    /// BossAI can adopt this later without changing projectile code.
    /// </summary>
    public interface ISpellTarget
    {
        void OnHit(SpellHitData hitData);
    }
}
