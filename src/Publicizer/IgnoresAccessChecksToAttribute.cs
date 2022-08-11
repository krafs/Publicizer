namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class IgnoresAccessChecksToAttribute : Attribute
    {
        internal IgnoresAccessChecksToAttribute(string assemblyName)
        {
        }
    }
}
