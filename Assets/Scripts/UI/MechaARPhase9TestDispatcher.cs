#if UNITY_EDITOR
using System;
using UnityEngine;
public sealed class MechaARPhase9TestDispatcher : MonoBehaviour
{
    public Action Frame;
    void LateUpdate() { foreach (var diagnostics in UnityEngine.Object.FindObjectsByType<ARRuntimeDiagnostics>(FindObjectsInactive.Include, FindObjectsSortMode.None)) diagnostics.enabled = false; Frame?.Invoke(); }
}
#endif
