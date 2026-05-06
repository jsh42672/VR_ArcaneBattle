using UnityEngine;

public class FixTree : MonoBehaviour
{
    void Start()
    {
        FixRotation();
    }

    void FixRotation()
    {
        // 모든 자식 메쉬 가져오기
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        CombineInstance[] combine = new CombineInstance[meshFilters.Length];

        // 회전 매트릭스 생성 (90도 회전)
        Matrix4x4 rotationMatrix = Matrix4x4.Rotate(Quaternion.Euler(-90, 0, 0));

        for (int i = 0; i < meshFilters.Length; i++)
        {
            combine[i].mesh = meshFilters[i].sharedMesh;
            combine[i].transform = rotationMatrix * meshFilters[i].transform.localToWorldMatrix;
            meshFilters[i].gameObject.SetActive(false); // 원본 숨기기
        }

        // 합친 메쉬 생성
        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combine);

        // 컴포넌트 추가
        MeshFilter mf = gameObject.AddComponent<MeshFilter>();
        MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();
        mf.sharedMesh = combinedMesh;

        // 머티리얼 복사
        if (meshFilters.Length > 0)
        {
            mr.sharedMaterial = meshFilters[0].GetComponent<MeshRenderer>().sharedMaterial;
        }

        Debug.Log("Tree fixed!");
    }
}