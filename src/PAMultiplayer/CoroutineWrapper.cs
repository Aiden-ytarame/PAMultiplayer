using System.Collections;
using UnityEngine;

namespace PAMultiplayer;

/// <summary>
/// Coroutine have no IsCompleted bool by default, so if you start multiple coroutines you cannot check which ones are ready
/// This wrapper is to remediate that.
/// </summary>
public class CoroutineWrapper
{
    public bool IsCompleted { get; private set; } 
    public Coroutine Coroutine { get; private set; }
    
    public CoroutineWrapper(MonoBehaviour mb, IEnumerator coroutine)
    {
        Coroutine = mb.StartCoroutine(Wrapper(coroutine));
    }

    private IEnumerator Wrapper(IEnumerator coroutine)
    {
        IsCompleted = false;
        try
        {
            yield return coroutine;
        }
        finally
        {
            IsCompleted = true;
        }
    }
}