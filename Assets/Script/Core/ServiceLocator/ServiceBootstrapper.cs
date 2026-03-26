using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ServiceBootstrapper : MonoBehaviour
{
    private List<IDisposableService> services = new List<IDisposableService>();

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
            new TurnService(),
            new YokaiEngine(),
            new PlayersService(),
        // Add other services here
    };


        foreach(var service in services)
        {
            if (service is IDisposableService disposableService)
            {
                this.services.Add(disposableService);
            }
            else
            {
                Debug.LogError($"Service {service.GetType().Name} does not implement IDisposableService.");
            }
        }
    }

    private void Start()
    {
        foreach(var service in services)
        {
           if( service != null) service.Init();
        }
    }


    private void OnDestroy()
    {
        foreach(var service in services)
        {
            if (service != null) service.Dispose();
        }
    }
}
