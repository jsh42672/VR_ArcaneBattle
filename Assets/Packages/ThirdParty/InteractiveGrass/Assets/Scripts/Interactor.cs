using UnityEngine;

[ExecuteAlways]
public class Interactor : MonoBehaviour
{
    [Header("Target")]
    public GameObject interactor;
    [SerializeField] private Renderer targetRenderer;

    [Header("Material")]
    [SerializeField] private bool useSharedMaterial = true;

    private static readonly int InteractorPositionId = Shader.PropertyToID("_InteractorPosition");
    private Material grassMat;

    private void OnEnable()
    {
        ResolveReferences();
        PushInteractorPosition();
    }

    private void OnValidate()
    {
        ResolveReferences();
        PushInteractorPosition();
    }

    private void Update()
    {
        ResolveReferences();
        PushInteractorPosition();
    }

    private void ResolveReferences()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (interactor == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                interactor = player;
            }
        }

        if (targetRenderer == null)
        {
            grassMat = null;
            return;
        }

        grassMat = useSharedMaterial || !Application.isPlaying
            ? targetRenderer.sharedMaterial
            : targetRenderer.material;
    }

    private void PushInteractorPosition()
    {
        if (interactor == null || grassMat == null)
        {
            return;
        }

        grassMat.SetVector(InteractorPositionId, interactor.transform.position);
    }
}
