namespace Publicizer.Tests
{
    public class AccessPrivateMethodOverload
    {
        public static void Main()
        {
            NonPublic.PrivateMethod(5);
        }
    }
}
