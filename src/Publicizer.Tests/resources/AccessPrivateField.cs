namespace Publicizer.Tests
{
    public class AccessPrivateField
    {
        public static void Main()
        {
            _ = NonPublic.s_privateField;
        }
    }
}
