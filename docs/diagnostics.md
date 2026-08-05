# Diagnostics

Every error and warning Publicizer raises carries a `PUBxxxx` code, so it can be found, filtered, and suppressed the way MSBuild's own diagnostics are. Codes are defined in `src/Publicizer/DiagnosticCode.cs`; `DiagnosticCodeTests` fails if that file and the table below disagree in either direction.

A code names a **failure class**, not a message. The malformed-`Type` spellings all report `PUB1005` and differ only in text, so suppressing one suppresses them all.

## Numbering

Codes are banded by what raises them, not by theme — a theme moves as features do, an emitter does not. Within a band, a new code takes the next free number.

| Band | Raised by |
|---|---|
| `PUB1xxx` | Parsing and validating `Publicize`/`DoNotPublicize` items |
| `PUB2xxx` | Task execution and I/O |
| `PUB3xxx` | The outcome of publicizing an assembly |
| `PUB4xxx` | The MSBuild targets, rather than the task |
| `PUB9xxx` | Reserved for a future analyzer or BuildCheck surface |

A code is permanent **once it has appeared in a release**: a retired diagnostic keeps its number rather than freeing it for reuse, because consumers key `NoWarn` on it. Before its first release a code is still soft, and may be renumbered or dropped outright.

## Codes

| Code | Severity | Raised when |
|---|---|---|
| `PUB1001` | Error | An item mixes the colon form with `Namespace`/`Type` metadata. |
| `PUB1002` | Error | An item sets a member-level qualifier the structured syntax reserves but does not implement yet (`Field`, `Method`, `Property`, `Event`, `Accessor`, `Parameters`). |
| `PUB1003` | Error | An item sets `IncludeSubNamespaces` or `IncludeTypeContents`. A scope's descent is unconditional today and cannot be narrowed. |
| `PUB1004` | Error | A scope's `Namespace` is not a plain dotted namespace name. |
| `PUB1005` | Error | A scope's `Type` is malformed — a backtick or `+`, unbalanced braces, an empty name segment, or an empty or nested type argument list. |
| `PUB1006` | Error | A scope sets `MemberPattern`, which only the bare-assembly item accepts. |
| `PUB1007` | Error | A `DoNotPublicize` scope sets `IncludeVirtualMembers` or `IncludeCompilerGeneratedMembers`. A deny scope has no sweep for a filter to apply to. |
| `PUB2001` | Error | `OutputDirectory` is not a usable directory path. |
| `PUB2002` | Error | The log file named by `PublicizerLogFilePath` could not be created. |
| `PUB3001` | Warning | An assembly is `DoNotPublicize`d as a whole while `Publicize` scopes name part of it. The scopes are more specific and win. |
| `PUB3002` | Warning | An assembly was marked for publicization, but nothing in it was publicized. |
| `PUB4001` | Warning | `PublicizerRuntimeStrategies` enables neither `Unsafe` nor `IgnoresAccessChecksTo`, so publicized members compile but fail their visibility check at run time. Suppress it if the referenced assembly is already public at run time. |

## Suppressing a warning

`NoWarn` and `WarningsAsErrors` route `PUBxxxx` warnings the same way they route compiler warnings:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);PUB3002</NoWarn>
  <WarningsAsErrors>$(WarningsAsErrors);PUB3001</WarningsAsErrors>
</PropertyGroup>
```

That works because `Microsoft.Common.CurrentVersion.targets` folds `NoWarn` into `MSBuildWarningsAsMessages` and `WarningsAsErrors` into `MSBuildWarningsAsErrors`. Either `MSBuild`-prefixed property can be set directly instead, alongside `MSBuildWarningsNotAsErrors` to exempt a code from a blanket `MSBuildTreatWarningsAsErrors`.

Errors cannot be suppressed. Each one names an item the task refuses to guess at, and continuing would publicize something other than what was asked for.
