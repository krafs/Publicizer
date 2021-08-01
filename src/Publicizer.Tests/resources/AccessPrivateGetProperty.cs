namespace Publicizer.Tests
{
    public class AccessPrivateGetProperty
    {
        public static void Main()
        {
            _ = NonPublic.PrivateProperty;
        }
    }
}
