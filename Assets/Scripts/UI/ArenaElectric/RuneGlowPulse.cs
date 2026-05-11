using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경기장 바닥의 룬/문양이 파동치듯 빛나는 글로우 효과 (URP HDR Emission)
///
/// 사용법:
///   1. 빈 GameObject에 컴포넌트 추가
///   2. Inspector의 "Rune Renderers" 배열에 룬 메시/오브젝트의 Renderer 연결
///   3. 룬 머티리얼의 Emission 체크박스를 켜야 합니다 (URP Lit or Unlit)
/// </summary>
public class RuneGlowPulse : MonoBehaviour
{
    [Header("룬 오브젝트 연결")]
    [Tooltip("글로우 효과를 적용할 Renderer 목록 (룬, 바닥 문양 등)")]
    public Renderer[] runeRenderers;

    [Header("글로우 색상 (HDR — Inspector에서 HDR 컬러피커 사용)")]
    [ColorUsage(true, true)]
    [Tooltip("파동의 최소 밝기 색상")]
    public Color minEmission = new Color(0f, 0.3f, 1.2f, 1f);

    [ColorUsage(true, true)]
    [Tooltip("파동의 최대 밝기 색상 (Bloom이 터지는 구간)")]
    public Color maxEmission = new Color(0f, 1.8f, 6f, 1f);

    [Header("파동 설정")]
    [Tooltip("파동 속도 (Hz 단위, 1 = 1초에 한 번 맥동)")]
    [Range(0.1f, 5f)]
    public float pulseFrequency = 0.8f;

    [Tooltip("각 룬마다 위상 차이를 줘서 물결처럼 순서대로 빛나게 함")]
    public bool wavePropagation = true;

    [Tooltip("물결 전파 강도 (0이면 모두 동시에 빛남)")]
    [Range(0f, 2f)]
    public float waveSpread = 1.2f;

    [Header("추가 효과")]
    [Tooltip("랜덤한 깜빡임 강도 (0이면 깜빡임 없음)")]
    [Range(0f, 1f)]
    public float flickerStrength = 0.15f;

    [Tooltip("바닥 전체를 덮는 큰 글로우 메시가 있으면 여기 연결 (선택)")]
    public Renderer floorGlowRenderer;

    [ColorUsage(true, true)]
    public Color floorMinEmission = new Color(0f, 0.05f, 0.3f, 1f);

    [ColorUsage(true, true)]
    public Color floorMaxEmission = new Color(0f, 0.4f, 1.5f, 1f);

    // --- 내부 ---
    private MaterialPropertyBlock[] _propBlocks;
    private MaterialPropertyBlock   _floorBlock;
    private float[]                 _phaseOffsets;
    private static readonly int     _emissionID = Shader.PropertyToID("_EmissionColor");

    void Start()
    {
        if (runeRenderers == null || runeRenderers.Length == 0)
        {
            Debug.LogWarning("[RuneGlowPulse] runeRenderers가 비어 있습니다. Inspector에서 룬 Renderer를 연결하세요.");
            return;
        }

        _propBlocks  = new MaterialPropertyBlock[runeRenderers.Length];
        _phaseOffsets = new float[runeRenderers.Length];

        for (int i = 0; i < runeRenderers.Length; i++)
        {
            _propBlocks[i] = new MaterialPropertyBlock();

            // 물결 전파: 공간적 위치 기반 위상 오프셋
            if (wavePropagation && runeRenderers[i] != null)
            {
                Vector3 pos = runeRenderers[i].transform.position;
                float angle = Mathf.Atan2(pos.z - transform.position.z,
                                           pos.x - transform.position.x);
                _phaseOffsets[i] = angle * waveSpread;
            }
            else
            {
                _phaseOffsets[i] = 0f;
            }

            // Emission 활성화 (런타임에서 켜기)
            if (runeRenderers[i] != null)
            {
                foreach (var mat in runeRenderers[i].materials)
                    mat.EnableKeyword("_EMISSION");
            }
        }

        // 바닥 글로우 설정
        if (floorGlowRenderer != null)
        {
            _floorBlock = new MaterialPropertyBlock();
            foreach (var mat in floorGlowRenderer.materials)
                mat.EnableKeyword("_EMISSION");
        }
    }

    void Update()
    {
        if (_propBlocks == null) return;

        float time = Time.time * pulseFrequency * Mathf.PI * 2f;

        for (int i = 0; i < runeRenderers.Length; i++)
        {
            if (runeRenderers[i] == null) continue;

            // 사인파 + 위상 + 깜빡임
            float raw     = Mathf.Sin(time + _phaseOffsets[i]);          // -1 ~ 1
            float t       = (raw + 1f) * 0.5f;                           //  0 ~ 1
            float flicker = 1f - flickerStrength * Mathf.PerlinNoise(
                                    Time.time * 8f + i * 3.7f, 0f);

            Color emission = Color.LerpUnclamped(minEmission, maxEmission, t) * flicker;

            runeRenderers[i].GetPropertyBlock(_propBlocks[i]);
            _propBlocks[i].SetColor(_emissionID, emission);
            runeRenderers[i].SetPropertyBlock(_propBlocks[i]);
        }

        // 바닥 전체 글로우
        if (floorGlowRenderer != null && _floorBlock != null)
        {
            float t       = (Mathf.Sin(time * 0.5f) + 1f) * 0.5f; // 느린 맥동
            Color emission = Color.LerpUnclamped(floorMinEmission, floorMaxEmission, t);

            floorGlowRenderer.GetPropertyBlock(_floorBlock);
            _floorBlock.SetColor(_emissionID, emission);
            floorGlowRenderer.SetPropertyBlock(_floorBlock);
        }
    }

    // ─── 외부 API ────────────────────────────────────────────────────
    /// <summary>
    /// 전투 시작/보스 등장 등 이벤트 시 강도를 순간적으로 올림
    /// intensity: 0(끔) ~ 1(기본) ~ 2(최대)
    /// </summary>
    public void SetIntensity(float intensity)
    {
        intensity     = Mathf.Clamp(intensity, 0f, 2f);
        pulseFrequency = Mathf.Lerp(0.3f, 3f, intensity / 2f);
    }

    /// <summary>특정 룬을 색상으로 강조 (예: 활성화된 마법진)</summary>
    public void HighlightRune(int index, Color highlightColor, float duration)
    {
        if (index < 0 || index >= runeRenderers.Length) return;
        StartCoroutine(HighlightRoutine(index, highlightColor, duration));
    }

    System.Collections.IEnumerator HighlightRoutine(int idx, Color col, float dur)
    {
        Color original = maxEmission;
        maxEmission    = col;
        yield return new WaitForSeconds(dur);
        maxEmission    = original;
    }
}
