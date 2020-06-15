using System;
using UnityEngine;
using UnityEngine.ResourceManagement.Util;

internal class MonoBehaviourCallbackHooks : ComponentSingleton<MonoBehaviourCallbackHooks>
{
    public event Action<float> OnUpdateDelegate;

    protected override string GetGameObjectName() => "ResourceManagerCallbacks";

    // Update is called once per frame
    void Update()
    {
        if (OnUpdateDelegate != null)
            OnUpdateDelegate(Time.unscaledDeltaTime);
    }
}
