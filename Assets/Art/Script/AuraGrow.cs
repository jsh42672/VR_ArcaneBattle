using UnityEngine;

public class AuraGrow : MonoBehaviour
{
    [Header("오라가 최대치로 커지는 데 걸리는 시간")]
    public float growDuration = 1.5f;

    private Vector3 targetScale; // 원래 유지해야 할 최대 크기
    private float timer = 0f;

    void Awake()
    {
        // 게임 시작 시, 유니티 인스펙터에 설정해 둔 원래 크기(최대치)를 기억합니다.
        targetScale = transform.localScale;
    }

    void OnEnable()
    {
        // 보스 체력이 50%가 되어 SetActive(true)로 켜지는 순간 실행됩니다.
        // 크기를 0으로 돌려놓고 타이머를 초기화합니다.
        transform.localScale = Vector3.zero;
        timer = 0f;
    }

    void Update()
    {
        // 지정된 시간(growDuration) 동안 서서히 원래 크기(targetScale)로 키웁니다.
        if (timer < growDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / growDuration;

            // Vector3.Lerp를 사용해 0에서 원래 크기까지 부드럽게 전환합니다.
            transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, progress);
        }
        else
        {
            // 시간이 다 지나면 정확히 최대 크기로 고정합니다 (보험용)
            transform.localScale = targetScale;
        }
    }
}