using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ServiceBootstrapper : MonoBehaviour
{
    private List<IDisposableService> _services = new List<IDisposableService>();

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Intialize();
    }

    private void Intialize()
    {
        var services = new object[]
      {
            new BoardGridService(),
            new GameStateService(),
            new YokaiEngine(),
            new TurnService(),
        // Add other services here
    };


        foreach(var service in services)
        {
            if (service is IDisposableService disposableService)
            {
                _services.Add(disposableService);
            }
            else
            {
                Debug.LogError($"Service {service.GetType().Name} does not implement IDisposableService.");
            }
        }
    }

    private void Start()
    {
        foreach(var service in _services)
        {
           if( service != null) service.Init();
        }
    }


    private void OnDestroy()
    {
        foreach(var service in _services)
        {
            if (service != null) service.Dispose();
        }
    }
}
