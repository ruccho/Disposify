namespace Disposify
{
    public static class DisposifyExtensions
    {
        public static dynamic Disposify<T>(this T? target)
        {
            return target != null ?
                new DefaultDisposifier(target) : 
                new DefaultDisposifier(target.GetType());
        }
    }
}