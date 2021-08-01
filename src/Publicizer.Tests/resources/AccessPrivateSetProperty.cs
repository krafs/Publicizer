namespace Publicizer.Tests
{
    public class AccessPrivateSetProperty
    {
        public static void Main()
        {
            NonPublic.PrivateProperty = default;
        }
    }
}
