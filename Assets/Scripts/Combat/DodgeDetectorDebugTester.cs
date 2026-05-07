using UnityEngine;
using UnityKeyboard = UnityEngine.InputSystem.Keyboard;

namespace ArcaneVR.Combat
{
    public class DodgeDetectorDebugTester : MonoBehaviour
    {
        [SerializeField] private DodgeDetector dodgeDetector;

        private void Awake()
        {
            if (dodgeDetector == null)
            {
                dodgeDetector = GetComponent<DodgeDetector>();
            }
        }

        private void OnEnable()
        {
            if (dodgeDetector == null) return;

            dodgeDetector.OnDodgeSuccess += HandleDodgeSuccess;
            dodgeDetector.OnDodgeFail += HandleDodgeFail;
        }

        private void OnDisable()
        {
            if (dodgeDetector == null) return;

            dodgeDetector.OnDodgeSuccess -= HandleDodgeSuccess;
            dodgeDetector.OnDodgeFail -= HandleDodgeFail;
        }

        private void Update()
        {
            if (dodgeDetector == null) return;

            UnityKeyboard keyboard = UnityKeyboard.current;

            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                Debug.Log("[DodgeTester] Start High Attack Dodge Test");
                dodgeDetector.BeginDodgeWindow(BossAttackType.High);
            }

            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                Debug.Log("[DodgeTester] Start Middle Attack Dodge Test");
                dodgeDetector.BeginDodgeWindow(BossAttackType.Middle);
            }

            if (keyboard.digit3Key.wasPressedThisFrame)
            {
                Debug.Log("[DodgeTester] Start Low Attack Dodge Test");
                dodgeDetector.BeginDodgeWindow(BossAttackType.Low);
            }
        }

        private void HandleDodgeSuccess()
        {
            Debug.Log("[DodgeTester] 회피 성공");
        }

        private void HandleDodgeFail()
        {
            Debug.Log("[DodgeTester] 회피 실패");
        }
    }
}