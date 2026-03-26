using System;
using System.Collections.Generic;
using UnityEngine;

public class PoolService : MonoBehaviour, IPoolingService
{

    private Dictionary<int, Queue<GameObject>> poolDictionary = new Dictionary<int, Queue<GameObject>>();

    [SerializeField] private List<PoolPrewarmConfig> objectsToPrewarm;


    /// <summary>
    /// Prepare the pool with a certain number of instances.
    /// </summary>
    public void Prewarm(GameObject prefab, int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(prefab);
            obj.SetActive(false);
            ReturnToPool(prefab, obj);
        }

        foreach (var item in objectsToPrewarm)
        {
            Prewarm(item.prefab, item.amount);
        }
    }


    private void Awake()
    {
        GameServiceLocator.Register<IPoolingService>(this);
    }

    //==============================================
    // GET POOLS AND RETURN TO POOL
    //==============================================


    /// <summary>
    /// Recover an object from the pool or create a new one if none are available.
    /// </summary>
    public GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        // Get the unique ID for the prefab
        int id = prefab.GetInstanceID();

        if (!poolDictionary.ContainsKey(id))
        {
            poolDictionary.Add(id, new Queue<GameObject>());
        }

        GameObject obj;

        if (poolDictionary[id].Count > 0)
        {
            obj = poolDictionary[id].Dequeue();
        }
        else
        {
            obj = Instantiate(prefab);

            PoolMember member = obj.AddComponent<PoolMember>();
            member.myPrefab = prefab;
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        return obj;
    }

    /// <summary>
    /// Return an object to its pool.
    /// </summary>
    public void ReturnToPool(GameObject prefab, GameObject obj)
    {
        int id = prefab.GetInstanceID();
        obj.SetActive(false);
        poolDictionary[id].Enqueue(obj);
    }

    public void Dispose()
    {

    }

    public void Init()
    {

    }
}

[Serializable]
public class PoolPrewarmConfig
{
    public GameObject prefab;
    [Min(1)] public int amount;
}
