using UnityEngine;

public class ProjectileMoveTest : MonoBehaviour
{
    public float speed = 5f;       // 발사 속도
    public float lifeTime = 4f;    // 이 시간 뒤 자동 삭제

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self);
    }
}