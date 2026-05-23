using UnityEngine;

public class MagicCircleSpawn : MonoBehaviour
{
    [Header("등장 시간 (초)")]
    public float appearTime = 0.35f;
    [Header("등장 후 최종 크기")]
    public float targetScale = 1f;

    private float timer = 0f;

    void OnEnable()
    {
        timer = 0f;                       // 켜질 때마다 처음부터
        transform.localScale = Vector3.zero;
    }

    void Update()
    {
        if (timer < appearTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / appearTime);
            // 끝으로 갈수록 부드럽게 멈추는 곡선 (펑 하고 커지는 느낌)
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.localScale = Vector3.one * targetScale * eased;
        }
    }
}