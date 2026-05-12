using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경기장 바닥과 필러 사이를 흐르는 입체적인 전기 번개 효과 (URP 호환)
/// </summary>
public class FloorElectricFlow : MonoBehaviour
{
    [Header("메인 번개 설정")]
    public int arcCount = 15;
    public int segmentsPerArc = 24;
    public float startWidth = 0.4f;
    public float endWidth = 0.12f;
    public float arenaRadius = 9.5f;
    public float heightOffset = 0.05f;

    [Header("3D 아크 효과")]
    public float minMidHeight = 0.3f;
    public float maxMidHeight = 1.2f;

    [Header("번개 모양")]
    public float displacementAmount = 0.8f;
    public float displacementFalloff = 0.55f;

    [Header("타이밍")]
    public float minLifetime = 0.05f;
    public float maxLifetime = 0.18f;
    public float minCooldown = 0.05f;
    public float maxCooldown = 0.25f;

    [Header("색상 (URP HDR)")]
    [ColorUsage(true, true)]
    public Color coreColor = new Color(0.3f, 0.8f, 1f, 1f);
    [ColorUsage(true, true)]
    public Color glowColor = new Color(0f, 0.4f, 1f, 1f);

    [Header("필러 번개 설정")]
    public Vector3[] pillarPositions; 
    public float pillarFlashIntervalMin = 1f;
    public float pillarFlashIntervalMax = 3f;
    public float pillarWidth = 0.2f;

    [Header("머티리얼")]
    public Material lightningMaterial;

    private LineRenderer[] _mainArcs;
    private LineRenderer[] _layeredArcs;
    private LineRenderer[] _pillarArcs;
    private Coroutine[]    _mainRoutines;
    private Coroutine[]    _pillarRoutines;

    void Start()
    {
        Debug.Log($"[FloorElectricFlow] Initializing amplified effects.");

        if (lightningMaterial == null)
            lightningMaterial = CreateDefaultMaterial();

        SetupMainArcs();
        SetupPillarArcs();
    }

    void SetupMainArcs()
    {
        _mainArcs = new LineRenderer[arcCount];
        _layeredArcs = new LineRenderer[arcCount];
        _mainRoutines = new Coroutine[arcCount];

        for (int i = 0; i < arcCount; i++)
        {
            _mainArcs[i] = CreateLineRenderer($"MainArc_{i}", startWidth, endWidth);
            _layeredArcs[i] = CreateLineRenderer($"LayeredArc_{i}", startWidth * 0.25f, endWidth * 0.25f);
            
            _mainRoutines[i] = StartCoroutine(MainArcRoutine(i, Random.Range(0f, maxCooldown)));
        }
    }

    void SetupPillarArcs()
    {
        if (pillarPositions == null || pillarPositions.Length == 0)
        {
            // Pillars based on visual symmetry in colosseum
            pillarPositions = new Vector3[] {
                new Vector3(8.5f, 18f, 8.5f),
                new Vector3(-8.5f, 18f, 8.5f),
                new Vector3(8.5f, 18f, -8.5f),
                new Vector3(-8.5f, 18f, -8.5f)
            };
        }

        _pillarArcs = new LineRenderer[pillarPositions.Length];
        _pillarRoutines = new Coroutine[pillarPositions.Length];

        for (int i = 0; i < pillarPositions.Length; i++)
        {
            _pillarArcs[i] = CreateLineRenderer($"PillarArc_{i}", pillarWidth, pillarWidth * 0.5f);
            _pillarRoutines[i] = StartCoroutine(PillarArcRoutine(i));
        }
    }

    LineRenderer CreateLineRenderer(string name, float sWidth, float eWidth)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = lightningMaterial;
        lr.startWidth = sWidth;
        lr.endWidth = eWidth;
        lr.positionCount = segmentsPerArc + 1;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(coreColor, 0.5f), new GradientColorKey(glowColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        lr.colorGradient = grad;
        lr.enabled = false;
        return lr;
    }

    IEnumerator MainArcRoutine(int index, float initialDelay)
    {
        yield return new WaitForSeconds(initialDelay);
        while (true)
        {
            RefreshMainArc(index);
            _mainArcs[index].enabled = true;
            _layeredArcs[index].enabled = true;

            float lifetime = Random.Range(minLifetime, maxLifetime);
            float elapsed = 0f;
            while (elapsed < lifetime)
            {
                float flicker = Random.Range(0.02f, 0.06f);
                yield return new WaitForSeconds(flicker);
                elapsed += flicker;
                RefreshMainArc(index);
            }

            _mainArcs[index].enabled = false;
            _layeredArcs[index].enabled = false;
            yield return new WaitForSeconds(Random.Range(minCooldown, maxCooldown));
        }
    }

    IEnumerator PillarArcRoutine(int index)
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(pillarFlashIntervalMin, pillarFlashIntervalMax));
            
            RefreshPillarArc(index);
            _pillarArcs[index].enabled = true;
            yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
            _pillarArcs[index].enabled = false;
        }
    }

    void RefreshMainArc(int index)
    {
        float angle1 = Random.Range(0f, Mathf.PI * 2f);
        float angle2 = angle1 + Random.Range(Mathf.PI * 0.7f, Mathf.PI * 1.3f);
        
        float r1 = Random.Range(5.0f, arenaRadius);
        float r2 = Random.Range(5.0f, arenaRadius);

        Vector3 start = transform.position + new Vector3(Mathf.Cos(angle1) * r1, heightOffset, Mathf.Sin(angle1) * r1);
        Vector3 end = transform.position + new Vector3(Mathf.Cos(angle2) * r2, heightOffset, Mathf.Sin(angle2) * r2);

        float midHeight = Random.Range(minMidHeight, maxMidHeight);
        Vector3[] points = GenerateArcPoints(start, end, segmentsPerArc, midHeight);

        _mainArcs[index].positionCount = points.Length;
        _mainArcs[index].SetPositions(points);

        Vector3[] layeredPoints = new Vector3[points.Length];
        Vector3 offset = Vector3.up * 0.05f + Random.insideUnitSphere * 0.05f;
        for (int i = 0; i < points.Length; i++) layeredPoints[i] = points[i] + offset;
        _layeredArcs[index].positionCount = layeredPoints.Length;
        _layeredArcs[index].SetPositions(layeredPoints);
    }

    void RefreshPillarArc(int index)
    {
        Vector3 start = RandomPointOnFloor(arenaRadius * 0.5f, arenaRadius * 0.9f);
        Vector3 end = pillarPositions[index];

        Vector3[] points = GenerateArcPoints(start, end, segmentsPerArc, 0f);
        _pillarArcs[index].positionCount = points.Length;
        _pillarArcs[index].SetPositions(points);
    }

    Vector3 RandomPointOnFloor(float minR, float maxR)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float r = Random.Range(minR, maxR);
        return transform.position + new Vector3(Mathf.Cos(angle) * r, heightOffset, Mathf.Sin(angle) * r);
    }

    Vector3[] GenerateArcPoints(Vector3 start, Vector3 end, int segments, float midH)
    {
        List<Vector3> pts = new List<Vector3> { start };
        SubdivideArc(pts, start, end, displacementAmount, segments, midH);
        pts.Add(end);
        return pts.ToArray();
    }

    void SubdivideArc(List<Vector3> pts, Vector3 a, Vector3 b, float displacement, int depth, float midH)
    {
        if (depth <= 1) return;
        Vector3 mid = (a + b) * 0.5f;
        
        if (depth > segmentsPerArc / 2)
        {
             mid.y += midH;
        }
        
        Vector3 perp = Vector3.Cross((b - a).normalized, Vector3.up);
        if (perp == Vector3.zero) perp = Vector3.right;
        mid += perp * Random.Range(-displacement, displacement);
        mid += Vector3.up * Random.Range(-displacement * 0.5f, displacement * 0.5f);

        SubdivideArc(pts, a, mid, displacement * displacementFalloff, depth / 2, midH * 0.5f);
        pts.Add(mid);
        SubdivideArc(pts, mid, b, displacement * displacementFalloff, depth / 2, midH * 0.5f);
    }

    public void SetIntensity(float intensity)
    {
        intensity = Mathf.Clamp(intensity, 0f, 3f);
        foreach (var lr in _mainArcs) { lr.startWidth = startWidth * intensity; lr.endWidth = endWidth * intensity; }
        minCooldown = Mathf.Lerp(0.5f, 0.05f, intensity / 2f);
        maxCooldown = Mathf.Lerp(1.2f, 0.2f, intensity / 2f);
    }

    Material CreateDefaultMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material mat = new Material(shader);
        mat.SetFloat("_Surface", 1.0f);
        mat.SetFloat("_Blend", 1.0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        return mat;
    }

    void OnDestroy()
    {
        if (_mainRoutines != null) foreach (var r in _mainRoutines) if (r != null) StopCoroutine(r);
        if (_pillarRoutines != null) foreach (var r in _pillarRoutines) if (r != null) StopCoroutine(r);
    }
}
