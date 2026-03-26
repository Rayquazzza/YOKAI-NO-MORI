using UnityEngine;

public interface IPoolingService 
{
    GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation);

    void ReturnToPool(GameObject prefab, GameObject obj);
}
