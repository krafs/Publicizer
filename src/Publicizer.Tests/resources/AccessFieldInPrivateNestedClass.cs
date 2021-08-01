namespace Publicizer.Tests
{
    public class AccessFieldInPrivateNestedClass
    {
        public static void Main()
        {
            _ = NonPublic.NestedPrivateClass.s_privateField;
        }
    }
}
