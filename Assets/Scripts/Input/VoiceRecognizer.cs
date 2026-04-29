using System;
using ArcaneVR.Spell;
using UnityEngine;

namespace ArcaneVR.Input
{
    /// <summary>
    /// Listens for English voice commands (Fire, Ice, Thunder) via STT and fires OnVoiceCommand event with the detected ElementType.
    /// </summary>
    public class VoiceRecognizer : MonoBehaviour
    {
        public event Action<ElementType> OnVoiceCommand;

        // TODO: Implement
    }
}
