using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;

namespace ArcaneVR.Input
{
    /// <summary>
    /// 마도서가 열려있을 때 오른손으로 좌우 스와이프하면 페이지를 넘깁니다.
    ///   손 왼쪽 → NextPage (앞으로)
    ///   손 오른쪽 → PreviousPage (뒤로)
    /// </summary>
    public class RightPageTurnGesture : MonoBehaviour
    {
        [Header("── XR Hands 참조 ──")]
        [SerializeField] private XRHandTrackingEvents handTrackingEvents;
        [Tooltip("Right Hand Tracking > R_Wrist Transform")]
        [SerializeField] private Transform wristTransform;

        [Header("── 마도서 연동 ──")]
        [Tooltip("마도서가 열려있는지 확인하는 LeftGrimoireGesture")]
        [SerializeField] private LeftGrimoireGesture leftGrimoireGesture;

        [Header("── 페이지 스와이프 설정 ──")]
        [Tooltip("스와이프로 인정할 최소 수평 이동 거리 (m)")]
        [SerializeField] private float swipeDistance = 0.18f;
        [Tooltip("스와이프 최대 허용 시간 (초). 이 시간 안에 swipeDistance를 이동해야 함")]
        [SerializeField] private float swipeMaxDuration = 0.6f;
        [Tooltip("스와이프 중 허용되는 최대 수직 이동 (m). 너무 대각선이면 무시")]
        [SerializeField] private float swipeVerticalTolerance = 0.2f;
        [Tooltip("페이지 전환 쿨다운 (초)")]
        [SerializeField] private float swipeCooldown = 0.4f;

        [Header("── 디버그 ──")]
        [SerializeField] private bool debugLog = true;

        [Header("── 이벤트 ──")]
        public UnityEvent onNextPage;
        public UnityEvent onPreviousPage;

        private bool _swipeActive;
        private Vector3 _swipeStartPos;
        private float _swipeStartTime;
        private float _lastPageTurnTime = -999f;
        private int _jointUpdateCount;

        private void OnEnable()
        {
            if (handTrackingEvents != null)
                handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);
            StartCoroutine(ReconnectSubsystem());
        }

        private System.Collections.IEnumerator ReconnectSubsystem()
        {
            var subsystems = new System.Collections.Generic.List<XRHandSubsystem>();
            while (true)
            {
                UnityEngine.SubsystemManager.GetSubsystems(subsystems);
                if (subsystems.Count > 0 && subsystems[0].running) break;
                subsystems.Clear();
                yield return new WaitForSeconds(0.5f);
            }
            if (handTrackingEvents != null)
            {
                handTrackingEvents.enabled = false;
                handTrackingEvents.enabled = true;
                if (debugLog) Debug.Log("[페이지] XRHandTrackingEvents 재연결 완료", this);
            }
        }

        private void OnDisable()
        {
            if (handTrackingEvents != null)
                handTrackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
            _swipeActive = false;
        }

        private void OnJointsUpdated(XRHandJointsUpdatedEventArgs args)
        {
            if (!isActiveAndEnabled) return;
            _jointUpdateCount++;

            // 마도서가 열려있지 않으면 동작하지 않음
            if (leftGrimoireGesture == null || !leftGrimoireGesture.IsActive)
            {
                _swipeActive = false;
                return;
            }

            // 쿨다운 중이면 스킵
            if (Time.unscaledTime - _lastPageTurnTime < swipeCooldown)
            {
                _swipeActive = false;
                return;
            }

            // 오른손 손목 위치 가져오기
            Vector3 wristPos;
            if (wristTransform != null)
            {
                wristPos = wristTransform.position;
            }
            else
            {
                if (!args.hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wristPose)) return;
                wristPos = wristPose.position;
            }

            if (!_swipeActive)
            {
                // 스와이프 시작
                _swipeActive = true;
                _swipeStartPos = wristPos;
                _swipeStartTime = Time.unscaledTime;
                return;
            }

            // 시간 초과 → 리셋
            if (Time.unscaledTime - _swipeStartTime > swipeMaxDuration)
            {
                _swipeStartPos = wristPos;
                _swipeStartTime = Time.unscaledTime;
                return;
            }

            var delta = wristPos - _swipeStartPos;

            // 수직 이동이 너무 크면 무시
            if (Mathf.Abs(delta.y) > swipeVerticalTolerance)
            {
                _swipeStartPos = wristPos;
                _swipeStartTime = Time.unscaledTime;
                return;
            }

            // 수평 이동이 충분하면 페이지 전환
            if (Mathf.Abs(delta.x) >= swipeDistance)
            {
                _lastPageTurnTime = Time.unscaledTime;
                _swipeActive = false;

                if (delta.x < 0f)
                {
                    // 손이 왼쪽으로 → 이전 페이지
                    leftGrimoireGesture?.PreviousPage();
                    onPreviousPage?.Invoke();
                    if (debugLog) Debug.Log($"[페이지] 이전 페이지 (스와이프 거리={delta.x:F2}m)", this);
                }
                else
                {
                    // 손이 오른쪽으로 → 다음 페이지
                    leftGrimoireGesture?.NextPage();
                    onNextPage?.Invoke();
                    if (debugLog) Debug.Log($"[페이지] 다음 페이지 (스와이프 거리={delta.x:F2}m)", this);
                }
            }
        }
    }
}
