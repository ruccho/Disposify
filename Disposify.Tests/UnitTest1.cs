using System;

namespace Disposify.Tests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void TestInstance()
    {
        var c = new C1();
        int a = 100;
        
        using (c.Disposify().SomeEvent(v => ++v))
        {
            a = c.Invoke(a);
        }
            
        Assert.That(a, Is.EqualTo(101));
        
        
        using (c.Disposify().SomeEvent(v => --v))
        {
            a = c.Invoke(a);
        }
        
        Assert.That(a, Is.EqualTo(100));
        
            
        a = c.Invoke(a);
        Assert.That(a, Is.EqualTo(0));
    }
    

    [Test]
    public void TestStatic()
    {
        int a = 100;
        
        using (((C1)null).Disposify().SomeEventStatic(v => ++v))
        {
            a = C1.InvokeStatic(a);
        }
            
        Assert.That(a, Is.EqualTo(101));
        
        
        using (((C1)null).Disposify().SomeEventStatic(v => --v))
        {
            a = C1.InvokeStatic(a);
        }
        
        Assert.That(a, Is.EqualTo(100));
        
            
        a = C1.InvokeStatic(a);
        Assert.That(a, Is.EqualTo(0));
    }
}

public class C1
{
    public static event Func<int, int>? SomeEventStatic;
    public event Func<int, int>? SomeEvent;

    public static int InvokeStatic(int a) => SomeEventStatic?.Invoke(a) ?? 0;
    public int Invoke(int a) => SomeEvent?.Invoke(a) ?? 0;
}