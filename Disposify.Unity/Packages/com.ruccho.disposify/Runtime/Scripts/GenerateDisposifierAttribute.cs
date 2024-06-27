using System;

namespace Disposify
{
    public class GenerateDisposifierAttribute : Attribute
    {
        public GenerateDisposifierAttribute(Type target)
        {
            Target = target;
        }

        public Type Target { get; }
    }
}