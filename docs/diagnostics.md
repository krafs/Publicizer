# Diagnostics

Every error and warning the `PublicizeAssemblies` task raises carries a `PUBxxxx` code, so it can be found, filtered, and suppressed the way MSBuild's own diagnostics are. Codes are defined in `src/Publicizer/DiagnosticCode.cs`; `DiagnosticCodeTests` fails if a code there is missing from the table below.

A code names a **failure class**, not a message. The malformed-`Type` spellings all report `PUB0005` and differ only in text, so suppressing one suppresses them all. Codes are permanent: a retired diagnostic keeps its number rather than freeing it for reuse, and a new one takes the next free number regardless of where it fits thematically.

| Code | Severity | Raised when |
|---|---|---|
| `PUB0001` | Error | An item mixes the colon form with `Namespace`/`Type` metadata. |
| `PUB0002` | Error | An item sets a member-level qualifier the structured syntax reserves but does not implement yet (`Field`, `Method`, `Property`, `Event`, `Accessor`, `Parameters`). |
| `PUB0003` | Error | An item sets `IncludeSubNamespaces` or `IncludeTypeContents`. A scope's descent is unconditional today and cannot be narrowed. |
| `PUB0004` | Error | A scope's `Namespace` is not a plain dotted namespace name. |
| `PUB0005` | Error | A scope's `Type` is malformed — a backtick or `+`, unbalanced braces, an empty name segment, or an empty or nested type argument list. |
| `PUB0006` | Error | A scope sets `MemberPattern`, which only the bare-assembly item accepts. |
| `PUB0007` | Error | A `DoNotPublicize` scope sets `IncludeVirtualMembers` or `IncludeCompilerGeneratedMembers`. A deny scope has no sweep for a filter to apply to. |
| `PUB0008` | Error | A scope nested inside another leaves a filter the enclosing scope sets unset. Whether an inner scope inherits its enclosing scope's filters or the assembly's is not decided yet, so it must be set explicitly. |
| `PUB0009` | Warning | An assembly is `DoNotPublicize`d as a whole while `Publicize` scopes name part of it. The scopes are more specific and win. |
| `PUB0010` | Warning | An assembly was marked for publicization, but nothing in it was publicized. |
| `PUB0011` | Error | `OutputDirectory` is not a usable directory path. |
| `PUB0012` | Error | The log file named by `PublicizerLogFilePath` could not be created. |

## Suppressing a warning

In an SDK-style project, the SDK feeds `NoWarn` and `WarningsAsErrors` through to MSBuild's own warning routing, so a `PUBxxxx` warning is silenced or promoted the same way a compiler warning is:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);PUB0010</NoWarn>
  <WarningsAsErrors>$(WarningsAsErrors);PUB0009</WarningsAsErrors>
</PropertyGroup>
```

Outside the SDK those two properties are the compiler's alone and do nothing here. Use MSBuild's equivalents instead: `MSBuildWarningsAsMessages` and `MSBuildWarningsAsErrors`.

Errors cannot be suppressed. Each one names an item the task refuses to guess at, and continuing would publicize something other than what was asked for.
