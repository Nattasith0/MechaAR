#if UNITY_EDITOR
using System;
using UnityEngine;
public sealed class MechaARPhase6TestDispatcher : MonoBehaviour
{
    public Action Frame;
    void LateUpdate() { Frame?.Invoke(); }
}
#endif
