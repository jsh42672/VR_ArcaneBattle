using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Guard active state for recorded Meta hand pose selectors.
    /// Prevents cached shape states from selecting a pose when the target hand is not currently tracked.
    /// </summary>
    public class MetaHandTrackingActiveState : MonoBehaviour, IActiveState
    {
        [SerializeField] private HandRef hand;
        [SerializeField] private bool requireHighConfidence = true;

        public bool Active
        {
            get
            {
                if (hand == null)
                    return false;

                try
                {
                    if (!hand.IsConnected || !hand.IsTrackedDataValid)
                        return false;

                    return !requireHighConfidence || hand.IsHighConfidence;
                }
                catch (System.NullReferenceException)
                {
                    return false;
                }
            }
        }

        public void Configure(HandRef targetHand, bool highConfidenceRequired = true)
        {
            hand = targetHand;
            requireHighConfidence = highConfidenceRequired;
        }
    }
}
