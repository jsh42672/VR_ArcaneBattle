using System.Collections;
using UnityEngine;

namespace ArcaneVR.Combat
{
    public class AutoDodgeDetectorTester : MonoBehaviour
    {
        [SerializeField] private DodgeDetector dodgeDetector;

        [Header("Auto Test")]
        [SerializeField] private float startDelay = 3.0f;
        [SerializeField] private float interval = 5.0f;

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

            StartCoroutine(AutoTestRoutine());
        }

        private void OnDisable()
        {
            if (dodgeDetector == null) return;

            dodgeDetector.OnDodgeSuccess -= HandleDodgeSuccess;
            dodgeDetector.OnDodgeFail -= HandleDodgeFail;
        }

        private IEnumerator AutoTestRoutine()
        {
            yield return new WaitForSeconds(startDelay);

            while (true)
            {
                Debug.Log("[AutoDodgeTester] High Attack Test - Duck your head");
                dodgeDetector.BeginDodgeWindow(BossAttackType.High);
                yield return new WaitForSeconds(interval);

                Debug.Log("[AutoDodgeTester] Middle Attack Test - Move your body sideways");
                dodgeDetector.BeginDodgeWindow(BossAttackType.Middle);
                yield return new WaitForSeconds(interval);

                Debug.Log("[AutoDodgeTester] Low Attack Test - Dodge fail is expected");
                dodgeDetector.BeginDodgeWindow(BossAttackType.Low);
                yield return new WaitForSeconds(interval);
            }
        }

        private void HandleDodgeSuccess()
        {
            Debug.Log("[AutoDodgeTester] Dodge Success");
        }

        private void HandleDodgeFail()
        {
            Debug.Log("[AutoDodgeTester] Dodge Fail");
        }
    }
}