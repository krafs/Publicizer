namespace Publicizer.Tests
{
    public class AccessPrivateConstructor
    {
        public static void Main()
        {
            _ = new NonPublic();
        }
    }
}
