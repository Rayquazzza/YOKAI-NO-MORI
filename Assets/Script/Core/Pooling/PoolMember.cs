
using UnityEngine;

public class PoolMember : MonoBehaviour
{
    public GameObject myPrefab;

    public void ReturnToPool()
    {
        transform.SetParent(null);
        GameServiceLocator.Get<IPoolingService>().ReturnToPool(myPrefab, gameObject);
    }
}