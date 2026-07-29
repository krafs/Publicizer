// The compiler looks this type up by full name, so it cannot live in the Publicizer namespace.
#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace System.Runtime.CompilerServices;

/// <summary>
/// Compiler plumbing for <c>init</c> accessors. The compiler emits a modreq referencing this type,
/// which netstandard2.0 does not ship; targets that do ship it use theirs instead of this one.
/// Never referenced by hand.
/// </summary>
internal static class IsExternalInit;
