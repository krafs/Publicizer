namespace Fixture;

// A single assembly covering every member shape the publicization decision tree
// distinguishes. Loaded by the characterization tests via dnlib; never executed.
// Member access levels, kinds, and modifiers are chosen so each publicization
// branch (assembly-wide, member-pattern, do-not-publicize, virtual filter,
// compiler-generated filter, nested-type walk) has something to act on.
public abstract class Shapes
{
    // Fields across access levels.
    private int PrivateField;
    protected int ProtectedField;
    internal int InternalField;
    protected internal int ProtectedInternalField;
    private protected int PrivateProtectedField;
    public int PublicField;
    private static string StaticPrivateField = "x";

    // Properties: auto (compiler-generated backing field), expression, get-only.
    private string PrivateAutoProp { get; set; }
    protected string ProtectedExprProp => "y";
    private string GetOnlyProp { get; }

    // Constructor.
    private Shapes() { }

    // Methods across kinds/modifiers.
    private string PrivateMethod() => "z";
    protected virtual void ProtectedVirtualMethod() { }
    protected abstract void ProtectedAbstractMethod();
    internal static void InternalStaticMethod() { }
    public void PublicMethod() { }

    // Explicitly compiler-generated member (for the IncludeCompilerGeneratedMembers filter).
    [System.Runtime.CompilerServices.CompilerGenerated]
    private int MarkedCompilerGeneratedField;

    // Field-like event: the compiler emits a [CompilerGenerated] private backing field
    // named the same as the event ("FieldLikeEvent"). Publicizing that field collides
    // with the public event by name (CS0229) — the original reason for the
    // IncludeCompilerGeneratedMembers filter (issue #9).
    public event System.Action FieldLikeEvent;

    // Nested type with a private member: publicizing the member must also walk up
    // and publicize the enclosing type.
    private class Inner
    {
        private int InnerPrivateField;
    }
}

// A type whose members are all already public: the "nothing was publicized" path.
public class NoPrivateMembers
{
    public int AlreadyPublicField;
    public void AlreadyPublicMethod() { }
}

// Generic type: pins how member matching handles arity-mangled reflection names
// (e.g. "Fixture.GenericHolder`1.GenericField").
public class GenericHolder<T>
{
    private T GenericField;
    private T GenericMethod(T value) => value;
}
