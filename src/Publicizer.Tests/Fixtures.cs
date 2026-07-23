using System;
using System.IO;
using dnlib.DotNet;

namespace Publicizer.Tests;

/// <summary>
/// The characterization fixture: one small assembly covering every member shape the publicization
/// decision tree distinguishes, compiled once from source (see <see cref="Compiler"/>).
/// <see cref="LoadShapesModule"/> returns a fresh <see cref="ModuleDefMD"/> each call, so
/// publicization (which mutates in memory only) never leaks between tests.
/// </summary>
internal static class Fixtures
{
    // Member access levels, kinds, and modifiers are chosen so each publicization branch
    // (assembly-wide, member-pattern, do-not-publicize, virtual filter, compiler-generated
    // filter, nested-type walk, generics) has something to act on.
    private const string ShapesSource =
        """
        namespace Fixture;

        public abstract class Shapes
        {
            private int PrivateField;
            protected int ProtectedField;
            internal int InternalField;
            protected internal int ProtectedInternalField;
            private protected int PrivateProtectedField;
            public int PublicField;
            private static string StaticPrivateField = "x";

            private string PrivateAutoProp { get; set; }   // auto-property: compiler-generated backing field
            protected string ProtectedExprProp => "y";
            private string GetOnlyProp { get; }

            private Shapes() { }

            private string PrivateMethod() => "z";
            protected virtual void ProtectedVirtualMethod() { }
            protected abstract void ProtectedAbstractMethod();
            internal static void InternalStaticMethod() { }
            public void PublicMethod() { }

            [System.Runtime.CompilerServices.CompilerGenerated]
            private int MarkedCompilerGeneratedField;

            // Field-like event: the compiler emits a [CompilerGenerated] private backing field named the
            // same as the event, so publicizing it collides with the event by name (CS0229) - the original
            // reason for the IncludeCompilerGeneratedMembers filter (issue #9).
            public event System.Action FieldLikeEvent;

            // Nested type with a private member: publicizing the member must also walk up and publicize Inner's enclosing type.
            private class Inner
            {
                private int InnerPrivateField;
            }
        }

        // All members already public: the "nothing was publicized" path.
        public class NoPrivateMembers
        {
            public int AlreadyPublicField;
            public void AlreadyPublicMethod() { }
        }

        // Generic type: pins how member matching handles arity-mangled reflection names (e.g. "Fixture.GenericHolder`1.GenericField").
        public class GenericHolder<T>
        {
            private T GenericField;
            private T GenericMethod(T value) => value;
        }
        """;

    private static readonly byte[] ShapesAssembly = Compiler.Compile(ShapesSource);
    private static readonly Lazy<string> ShapesFilePath = new(WriteShapesToDisk);

    internal static ModuleDefMD LoadShapesModule() => ModuleDefMD.Load(ShapesAssembly);

    // The task and the hasher work off a file path, so materialize the compiled bytes once as "Fixture.dll".
    // The filename (minus extension) is the assembly name the Publicize items match against.
    internal static string ShapesPath() => ShapesFilePath.Value;

    private static string WriteShapesToDisk()
    {
        string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "PublicizerFixtures")).FullName;
        string path = Path.Combine(directory, "Fixture.dll");
        File.WriteAllBytes(path, ShapesAssembly);
        return path;
    }
}
