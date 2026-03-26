using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public static class GameServiceLocator
{

    private const string LogTag = "<b><color=green>[Service Locator]</color></b>";
    private static readonly Dictionary<Type, ServiceEntry> services = new Dictionary<Type, ServiceEntry>();
    private static readonly Dictionary<Type, List<Type>> interfaceImplementations = new Dictionary<Type, List<Type>>();

    public static event Action<Type, object> OnServiceRegistered;
    public static event Action<Type> OnServiceUnregistered;


    [Serializable]
    private class ServiceEntry
    {
        public object service;
        public ServiceLifecycle lifecycle;
        public bool isOptional;
        public string debugName;

        public ServiceEntry(object service, ServiceLifecycle lifecycle = ServiceLifecycle.Persistent,
                          bool isOptional = false, string debugName = null)
        {
            this.service = service;
            this.lifecycle = lifecycle;
            this.isOptional = isOptional;
            this.debugName = debugName ?? service?.GetType().Name;
        }
    }


    public enum ServiceLifecycle
    {
        Persistent,     // Survit aux changements de sc�ne
        SceneScoped,    // D�truit au changement de sc�ne
        Temporary       // Peut �tre remplac� � tout moment
    }

    /// <summary>
    /// Enregistre un service avec options avanc�es
    /// </summary>
    public static void Register<T>(T service, ServiceLifecycle lifecycle = ServiceLifecycle.Persistent,
                                  bool isOptional = false, string debugName = null) where T : class
    {
        if (service == null)
        {
            Debug.LogError($"{LogTag} Cannot register null service of type {typeof(T).Name}");
            return;
        }

        var serviceType = typeof(T);
        var entry = new ServiceEntry(service, lifecycle, isOptional, debugName);

        if (services.ContainsKey(serviceType))
        {
            var existing = services[serviceType];
            if (existing.lifecycle != ServiceLifecycle.Temporary)
            {
                Debug.LogWarning($"{LogTag} Overriding existing {existing.lifecycle} service: {existing.debugName}");
            }
        }

        services[serviceType] = entry;

        RegisterInterfaces(service, serviceType);

        OnServiceRegistered?.Invoke(serviceType, service);
        Debug.Log($"{LogTag} Registered {lifecycle} service: {entry.debugName}");
    }

    /// <summary>
    /// R�cup�re un service avec gestion d'erreur robuste
    /// </summary>
    public static T Get<T>() where T : class
    {

        if (!Application.isPlaying) return null;


        var serviceType = typeof(T);

        if (services.TryGetValue(serviceType, out var entry))
        {
            return entry.service as T;
        }

        if (serviceType.IsInterface && TryGetByInterface<T>(out var interfaceService))
        {
            return interfaceService;
        }

        if (IsOptionalService(serviceType))
        {
            Debug.Log($"{LogTag} Optional service {serviceType.Name} not found, returning null");
            return null;
        }

        var errorMsg = $"{LogTag} Required service {serviceType.Name} not registered!\n" +
                      $"Available services: {string.Join(", ", GetRegisteredServiceNames())}";

        Debug.LogError(errorMsg);

        // Correction ici : On jette l'exception dans TOUS les cas pour arrêter le code
        // ou on retourne null si on veut éviter le crash (mais l'exception est mieux pour le debug).
        throw new System.InvalidOperationException(errorMsg);
    }

    /// <summary>
    /// V�rifie si un service est disponible
    /// </summary>
    public static bool IsAvailable<T>() where T : class
    {
        var serviceType = typeof(T);
        return services.ContainsKey(serviceType) ||
               (serviceType.IsInterface && HasInterfaceImplementation(serviceType));
    }

    /// <summary>
    /// Remplace un service temporairement (utile pour les tests)
    /// </summary>
    public static void Replace<T>(T newService, string debugName = "Temporary") where T : class
    {
        if (newService == null)
        {
            Debug.LogError($"{LogTag} Cannot replace with null service of type {typeof(T).Name}");
            return;
        }

        Register(newService, ServiceLifecycle.Temporary, false, debugName);
    }

    /// <summary>
    /// Nettoie les services selon leur lifecycle
    /// </summary>
    public static void CleanupSceneScopedServices()
    {
        var toRemove = new List<Type>();

        foreach (var kvp in services)
        {
            if (kvp.Value.lifecycle == ServiceLifecycle.SceneScoped)
            {
                toRemove.Add(kvp.Key);
                Debug.Log($"{LogTag} Cleaning up scene-scoped service: {kvp.Value.debugName}");
            }
        }

        foreach (var type in toRemove)
        {
            Unregister(type);
        }
    }

    public static void Unregister<T>() where T : class
    {
        Unregister(typeof(T));
    }

    private static void Unregister(Type serviceType)
    {
        if (services.Remove(serviceType))
        {
            OnServiceUnregistered?.Invoke(serviceType);
            Debug.Log($"{LogTag} Service : {serviceType.Name} unregistered");
        }
    }

    // === M�thodes priv�es d'aide ===

    private static void RegisterInterfaces(object service, Type serviceType)
    {
        var interfaces = serviceType.GetInterfaces();
        foreach (var interfaceType in interfaces)
        {
            if (!interfaceImplementations.ContainsKey(interfaceType))
                interfaceImplementations[interfaceType] = new List<Type>();

            if (!interfaceImplementations[interfaceType].Contains(serviceType))
                interfaceImplementations[interfaceType].Add(serviceType);
        }
    }

    private static bool TryGetByInterface<T>(out T service) where T : class
    {
        var interfaceType = typeof(T);
        if (interfaceImplementations.TryGetValue(interfaceType, out var implementations))
        {
            foreach (var implType in implementations)
            {
                if (services.TryGetValue(implType, out var entry))
                {
                    service = entry.service as T;
                    if (service != null) return true;
                }
            }
        }

        service = null;
        return false;
    }

    private static bool IsOptionalService(Type serviceType)
    {
        return services.TryGetValue(serviceType, out var entry) && entry.isOptional;
    }

    private static bool HasInterfaceImplementation(Type interfaceType)
    {
        return interfaceImplementations.ContainsKey(interfaceType) &&
               interfaceImplementations[interfaceType].Count > 0;
    }

    private static string[] GetRegisteredServiceNames()
    {
        return services.Keys.Select(t => t.Name).ToArray();
    }

    public static void LogAllServices()
    {
        Debug.Log($"=== Registered Services ({services.Count}) ===");
        foreach (var kvp in services)
        {
            var entry = kvp.Value;
            Debug.Log($"• {kvp.Key.Name} ({entry.lifecycle}) - {entry.debugName} " +
                      $"{(entry.isOptional ? "[Optional]" : "[Required]")}");
        }
    }
}