using UnityEngine;

public class RingRotator : MonoBehaviour
{
    [Header("회전 속도 (도/초). 음수면 반대 방향)")]
    public float rotationSpeed = 30f;

    void Update()
    {
        // Quad의 정면 축(Z) 기준으로 회전
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }
}