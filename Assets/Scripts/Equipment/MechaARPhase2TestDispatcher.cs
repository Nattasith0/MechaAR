#if UNITY_EDITOR
using System;
using UnityEngine;

/// <summary>
/// Runs editor integration checks in the PlayerLoop so Screen dimensions and UI
/// raycasts use the Game View context. No type or code is included in players.
/// </summary>
public sealed class MechaARPhase2TestDispatcher : MonoBehaviour
{
    public Action Frame;

    void LateUpdate()
    {
        Frame?.Invoke();
    }
}
#endif
