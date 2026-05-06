using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    [Header("시간 설정")]
    [Tooltip("하루 길이 (초)")]
    public float dayDuration = 120f;  // 2분 = 하루

    [Tooltip("시작 시간 (0~1, 0.25=새벽, 0.5=정오, 0.75=노을)")]
    [Range(0f, 1f)]
    public float startTime = 0.3f;

    [Header("자동 재생")]
    public bool autoPlay = true;
    public float timeSpeed = 1f;

    private float currentTime;

    void Start()
    {
        currentTime = startTime;
    }

    void Update()
    {
        if (autoPlay)
        {
            currentTime += (Time.deltaTime / dayDuration) * timeSpeed;
            if (currentTime >= 1f) currentTime -= 1f;
        }

        // 0~1 시간 → 0~360도 회전
        float angle = currentTime * 360f - 90f;
        transform.rotation = Quaternion.Euler(angle, -30f, 0f);
    }
}