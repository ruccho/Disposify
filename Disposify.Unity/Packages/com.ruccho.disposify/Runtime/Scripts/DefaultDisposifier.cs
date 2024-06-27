using System;
using System.Dynamic;
using System.Reflection;

namespace Disposify
{
    internal class DefaultDisposifier : DynamicObject
    {
        public DefaultDisposifier(object target)
        {
            Target = target;
            TargetType = target.GetType();
        }

        public DefaultDisposifier(Type typeForStatic)
        {
            Target = null;
            TargetType = typeForStatic;
        }

        private object Target { get; }
        private Type TargetType { get; }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var flags = Target != null
                ? BindingFlags.Public | BindingFlags.Instance
                : BindingFlags.Public | BindingFlags.Static;

            var @event = TargetType.GetEvent(binder.Name, flags);

            if (@event != null)
            {
                result = new Subscriber(Target, @event);
                return true;
            }

            return base.TryGetMember(binder, out result);
        }

        #region Nested type: Subscriber

        private class Subscriber : DynamicObject
        {
            public Subscriber(object target, EventInfo eventInfo)
            {
                Target = target;
                EventInfo = eventInfo;
            }

            private object Target { get; }
            private EventInfo EventInfo { get; }

            public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
            {
                if (args[0] is not Delegate @delegate)
                    throw new ArgumentException("Argument must be a delegate", "args[0]");
                EventInfo.AddEventHandler(Target, @delegate);
                result = Disposable.Create(Target, @delegate,
                    (target, @delegate) => EventInfo.RemoveEventHandler(target, @delegate));
                return true;
            }
        }

        #endregion
    }
}