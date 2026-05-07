using UnityEngine;
using ArcaneVR.Spell;

namespace ArcaneVR.UI
{
    /// <summary>
    /// Unified feedback handler. Manages screen vignette (HP), hand aura color (element), spatial audio, and boss state VFX triggers.
    /// </summary>
    public class FeedbackManager : MonoBehaviour
    {
        public void OnSpellCast(SpellId spellId)
        {
            // Placeholder hook for spell feedback. 담당 UI/VFX 구현에서 실제 피드백을 연결합니다.
        }
    }
}
