using System.Collections;
using UnityEngine;
using CodexGenerated.GrimoirePages;

/// <summary>
/// 플레이 모드 진입 시 첫 페이지부터 마지막까지 순서대로 자동으로 넘깁니다.
/// 테스트 전용.
/// </summary>
public class GrimoireAutoPageTest : MonoBehaviour
{
    [SerializeField] private GrimoirePageTurner pageTurner;
    [Tooltip("시작 전 대기 시간 (초)")]
    [SerializeField] private float initialDelay = 1.5f;
    [Tooltip("페이지 넘긴 후 다음 페이지까지 일시정지 (초)")]
    [SerializeField] private float pauseBetweenPages = 0.6f;

    private void Start()
    {
        if (pageTurner == null)
            pageTurner = GetComponent<GrimoirePageTurner>();

        if (pageTurner == null)
        {
            Debug.LogWarning("[AutoPage] GrimoirePageTurner 없음", this);
            return;
        }

        StartCoroutine(AutoTurnAll());
    }

    private IEnumerator AutoTurnAll()
    {
        pageTurner.SetSpread(0);
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            pageTurner.NextPage();

            // 코루틴은 다음 프레임부터 실행 → 한 프레임 대기 후 IsTurning 확인
            yield return null;

            // IsTurning이 여전히 false = NextPage가 동작 안 함 = 마지막 스프레드
            if (!pageTurner.IsTurning)
            {
                Debug.Log("[AutoPage] 마지막 페이지 도달. 완료", this);
                yield break;
            }

            // 애니메이션 끝날 때까지 대기
            while (pageTurner.IsTurning)
                yield return null;

            // 다음 페이지 전 짧은 대기
            yield return new WaitForSeconds(pauseBetweenPages);
        }
    }
}
