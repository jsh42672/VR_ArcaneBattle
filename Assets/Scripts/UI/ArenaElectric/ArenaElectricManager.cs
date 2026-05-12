using UnityEngine;

/// <summary>
/// FloorElectricFlow + RuneGlowPulse 통합 매니저
///
/// 사용법:
///   1. 경기장 빈 GameObject에 이 컴포넌트 추가
///   2. Inspector에서 FloorElectricFlow, RuneGlowPulse 컴포넌트 연결
///   3. 전투 시작 시 ArenaElectricManager.Instance.StartBattle() 호출
/// </summary>
public class ArenaElectricManager : MonoBehaviour
{
    public static ArenaElectricManager Instance { get; private set; }

    [Header("컴포넌트 연결")]
    public FloorElectricFlow electricFlow;
    public RuneGlowPulse     runeGlow;

    [Header("기본 강도 (대기 상태)")]
    [Range(0f, 2f)] public float idleIntensity   = 0.6f;

    [Header("전투 강도")]
    [Range(0f, 2f)] public float battleIntensity = 1.6f;

    [Header("전환 속도 (초)")]
    public float transitionDuration = 1.5f;

    // --- 내부 ---
    private float _currentIntensity;
    private float _targetIntensity;
    private bool  _transitioning;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // 컴포넌트 자동 탐색 (같은 GameObject에 있을 경우)
        if (electricFlow == null) electricFlow = GetComponentInChildren<FloorElectricFlow>();
        if (runeGlow     == null) runeGlow     = GetComponentInChildren<RuneGlowPulse>();

        _currentIntensity = idleIntensity;
        _targetIntensity  = idleIntensity;
        ApplyIntensity(_currentIntensity);
    }

    void Update()
    {
        if (!_transitioning) return;

        _currentIntensity = Mathf.MoveTowards(
            _currentIntensity, _targetIntensity,
            Time.deltaTime / transitionDuration
        );
        ApplyIntensity(_currentIntensity);

        if (Mathf.Approximately(_currentIntensity, _targetIntensity))
            _transitioning = false;
    }

    // ─── 공개 API ─────────────────────────────────────────────────────

    /// <summary>전투 시작 — 전기 효과 강화</summary>
    public void StartBattle()
    {
        SetIntensity(battleIntensity);
        Debug.Log("[ArenaElectricManager] 전투 시작: 전기 강도 상승");
    }

    /// <summary>전투 종료 — 대기 상태로 복귀</summary>
    public void EndBattle()
    {
        SetIntensity(idleIntensity);
        Debug.Log("[ArenaElectricManager] 전투 종료: 전기 강도 감소");
    }

    /// <summary>강도를 즉시 또는 서서히 변경</summary>
    public void SetIntensity(float intensity, bool instant = false)
    {
        _targetIntensity = Mathf.Clamp(intensity, 0f, 2f);
        if (instant)
        {
            _currentIntensity = _targetIntensity;
            ApplyIntensity(_currentIntensity);
            _transitioning = false;
        }
        else
        {
            _transitioning = true;
        }
    }

    /// <summary>보스 등장 연출 — 잠깐 최대로 터뜨렸다가 전투 강도로</summary>
    public void BossEntrance()
    {
        StartCoroutine(BossEntranceRoutine());
    }

    System.Collections.IEnumerator BossEntranceRoutine()
    {
        // 0.5초 동안 최대 강도
        SetIntensity(2f, true);
        yield return new WaitForSeconds(0.5f);
        // 2초 동안 번쩍임
        for (int i = 0; i < 4; i++)
        {
            SetIntensity(0.2f, true);
            yield return new WaitForSeconds(0.1f);
            SetIntensity(2f, true);
            yield return new WaitForSeconds(0.15f);
        }
        // 전투 강도로 전환
        SetIntensity(battleIntensity);
    }

    // ─── 내부 헬퍼 ───────────────────────────────────────────────────
    void ApplyIntensity(float intensity)
    {
        electricFlow?.SetIntensity(intensity);
        runeGlow?.SetIntensity(intensity);
    }

    // ─── Unity Editor 테스트 버튼 (에디터 전용) ───────────────────────
#if UNITY_EDITOR
    [ContextMenu("테스트: 전투 시작")]
    void TestBattleStart() => StartBattle();

    [ContextMenu("테스트: 전투 종료")]
    void TestBattleEnd() => EndBattle();

    [ContextMenu("테스트: 보스 등장")]
    void TestBossEntrance() => BossEntrance();
#endif
}
