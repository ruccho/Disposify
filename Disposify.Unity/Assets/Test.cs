using System;
using UnityEngine;
using Disposify;

public class Test : MonoBehaviour
{
    void Start()
    {
        int a = 100;
        
        using (((C1)null).Disposify().SomeEventStatic(v => ++v))
        {
            a = C1.InvokeStatic(a);
        }
        
        Debug.Assert(a == 101);
        
        
        using (((C1)null).Disposify().SomeEventStatic(v => --v))
        {
            a = C1.InvokeStatic(a);
        }
        
        Debug.Assert(a == 100);
        
            
        a = C1.InvokeStatic(a);
        Debug.Assert(a == 0);

        Debug.Log("COMPLETE!");
    }
}

public class C1
{
    public static event Func<int, int>? SomeEventStatic;
    public event Func<int, int>? SomeEvent;

    public static int InvokeStatic(int a) => SomeEventStatic?.Invoke(a) ?? 0;
    public int Invoke(int a) => SomeEvent?.Invoke(a) ?? 0;
}
