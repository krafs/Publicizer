namespace Publicizer.Tests
{
    internal class NonPublic
    {
        protected NonPublic() : this("")
        {
        }

        protected NonPublic(string arg)
        {
            _ = arg;
            s_privateField = "";
            _ = s_privateField;
        }

        private static string? s_privateField;

        private class NestedPrivateClass
        {
            internal NestedPrivateClass()
            {
                s_privateField = default;
                _ = s_privateField;
                PrivateMethod();
                PrivateMethod(default);
                _ = PrivateProperty;
            }

            private static string? s_privateField;
        }

        private static void PrivateMethod()
        { }

        private static void PrivateMethod(int arg)
        {
            _ = arg;
        }

        private static string? PrivateProperty { get; set; }
    }
}
