using UnityEngine;

public class EnergyCore : MonoBehaviour
{
    [Header("차오르기")]
    public float startDelay = 0.2f;
    public float chargeTime = 0.5f;
    public float targetScale = 0.2f;

    [Header("맥동 (차오른 뒤)")]
    public float pulseAmount = 0.1f;
    public float pulseSpeed = 3f;

    [Header("회전 (차징 + 발사 모두)")]
    public Vector3 rotateSpeed = new Vector3(0f, 40f, 0f);

    [Header("발사")]
    public float flightSpeed = 5f;
    public bool autoLaunch = true;
    public float autoLaunchDelay = 1.5f;

    private bool isLaunched = false;
    private float timer = 0f;

    // 발사되는 순간의 방향을 기억할 변수
    private Vector3 launchDirection;

    void OnEnable()
    {
        timer = 0f;
        isLaunched = false;
        transform.localScale = Vector3.zero;

        // [핵심 수정 부분]
        // 구체가 생성되자마자, Update에서 회전하기 '직전'에 올바른 정면 방향을 미리 저장합니다!
        launchDirection = transform.forward;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (autoLaunch && !isLaunched && timer >= autoLaunchDelay)
            Launch();

        // 구체는 계속 빙글빙글 돕니다.
        transform.Rotate(rotateSpeed * Time.deltaTime);

        if (isLaunched)
        {
            // 발사 순간 저장해둔 방향(launchDirection)으로만 직진합니다.
            // 구체가 아무리 회전해도 비행 궤적이 휘지 않습니다.
            transform.position += launchDirection * flightSpeed * Time.deltaTime;
        }
        else
        {
            // 차징 중: 차오름 / 맥동
            if (timer < startDelay) return;

            float chargeT = (timer - startDelay) / chargeTime;
            if (chargeT < 1f)
            {
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(chargeT), 3f);
                transform.localScale = Vector3.one * targetScale * eased;
            }
            else
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
                transform.localScale = Vector3.one * targetScale * pulse;
            }
        }
    }

    public void Launch()
    {
        isLaunched = true;

        // [핵심 수정 부분] 
        // 여기서 방향을 저장하면 안 됩니다. (이미 구체가 돌아가서 방향이 망가졌기 때문입니다.)
        // 따라서 이 부분에 있던 launchDirection = transform.forward; 코드를 삭제했습니다.

        transform.localScale = Vector3.one * targetScale;
    }
}