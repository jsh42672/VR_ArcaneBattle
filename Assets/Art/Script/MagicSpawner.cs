using UnityEngine;

public class MagicSpawner : MonoBehaviour
{
    [Header("발사할 마법 프리팹 (중단 공격)")]
    public GameObject magicPrefab;

    [Header("스폰 위치 (손 끝)")]
    public Transform spawnPoint;

    [Header("충격파 프리팹 (하단 공격)")]
    public GameObject shockwavePrefab;

    [Header("충격파 생성 위치 (골렘 발밑 기준 앞쪽 거리)")]
    public float shockwaveForwardOffset = 1.5f;
    public float shockwaveRaycastHeight = 8f;
    public float shockwaveRaycastDistance = 30f;
    public float shockwaveHeightOffset = 0.02f;
    public float shockwaveScaleMultiplier = 1f;
    public LayerMask shockwaveGroundMask = ~0;

    [Header("슬래시 프리팹 (휘두르기 공격)")]
    public GameObject slashPrefab;
    public Transform slashSpawnPoint;

    [Header("슬래시 생성 위치 설정")]
    public float slashForwardOffset = 1.5f; // 골렘 앞쪽 거리
    public float slashHeightOffset = 2.0f;  // 골렘 가슴~어깨 높이

    [Header("마법 자동 삭제 시간 (초)")]
    public float destroyAfter = 3f;

    // 중단 공격 — 에너지볼 발사 (그대로)
    public void SpawnMagic()
    {
        if (magicPrefab == null || spawnPoint == null) return;

        Quaternion spawnRotation = Quaternion.LookRotation(transform.forward, Vector3.up);
        GameObject magic = Instantiate(magicPrefab, spawnPoint.position, spawnRotation);
        Destroy(magic, destroyAfter);
    }

    // 하단 공격 — 지진 충격파 (골렘 발밑 기준)
    public void SpawnShockwave()
    {
        Debug.Log("SpawnShockwave 호출! 시간: " + Time.time);
        if (shockwavePrefab == null) return;

        Vector3 groundPos = transform.position + transform.forward * shockwaveForwardOffset;
        Vector3 rayOrigin = groundPos + Vector3.up * shockwaveRaycastHeight;
        float rayDistance = shockwaveRaycastHeight + shockwaveRaycastDistance;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, shockwaveGroundMask, QueryTriggerInteraction.Ignore))
        {
            groundPos = hit.point + Vector3.up * shockwaveHeightOffset;
        }
        else
        {
            groundPos.y = transform.position.y + shockwaveHeightOffset;
        }

        Quaternion groundRotation = Quaternion.Euler(0f, transform.eulerAngles.y - 70f, 0f);

        GameObject shockwave = Instantiate(shockwavePrefab, groundPos, groundRotation);
        shockwave.transform.localScale *= shockwaveScaleMultiplier;
        Destroy(shockwave, destroyAfter);
    }

    // 휘두르기 공격 — 슬래시 이펙트
    public void SpawnSlash()
    {
        if (slashPrefab == null) return;

        // 1. 위치: 골렘 본체 위치 + 정면으로 조금 앞 + 위로 가슴 높이만큼
        Transform origin = slashSpawnPoint != null ? slashSpawnPoint : transform;

        Vector3 slashPos = origin.position
            + transform.forward * slashForwardOffset
            + Vector3.up * slashHeightOffset;

        // 2. 방향: 손목 뼈의 회전이 아닌, 골렘이 바라보는 정면을 기준으로 고정
        Quaternion slashRotation = Quaternion.LookRotation(transform.forward);

        // 만약 이펙트 프리팹 자체의 기본 각도가 누워있다면 아래처럼 보정할 수 있습니다.
        // Quaternion slashRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        GameObject slash = Instantiate(slashPrefab, slashPos, slashRotation);
        Destroy(slash, destroyAfter);
    }
}
