using System;
using NUnit.Framework;

namespace Disposify.Tests
{
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
            var a = 100;

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
        public void TestStatic1()
        {
            var a = 100;

            using (((C1?)null).Disposify().SomeEventStatic(v => ++v))
            {
                a = C1.InvokeStatic(a);
            }

            Assert.That(a, Is.EqualTo(101));


            using (((C1?)null).Disposify().SomeEventStatic(v => --v))
            {
                a = C1.InvokeStatic(a);
            }

            Assert.That(a, Is.EqualTo(100));


            a = C1.InvokeStatic(a);
            Assert.That(a, Is.EqualTo(0));
        }


        [Test]
        public void TestStatic2()
        {
            var a = 100;

            using (C2Disposifier.SomeEventStatic(v => ++v))
            {
                a = C2.InvokeStatic(a);
            }

            Assert.That(a, Is.EqualTo(101));


            using (C2Disposifier.SomeEventStatic(v => --v))
            {
                a = C2.InvokeStatic(a);
            }

            Assert.That(a, Is.EqualTo(100));


            a = C2.InvokeStatic(a);
            Assert.That(a, Is.EqualTo(0));
        }
    }

    public class C1
    {
        public static event Func<int, int> SomeEventStatic;
        public event Func<int, int> SomeEvent;

        public static int InvokeStatic(int a)
        {
            return SomeEventStatic?.Invoke(a) ?? 0;
        }

        public int Invoke(int a)
        {
            return SomeEvent?.Invoke(a) ?? 0;
        }
    }

    public static class C2
    {
        public static event Func<int, int> SomeEventStatic;

        public static int InvokeStatic(int a)
        {
            return SomeEventStatic?.Invoke(a) ?? 0;
        }
    }

    [GenerateDisposifier(typeof(C2))]
    public static partial class C2Disposifier
    {
    }
}