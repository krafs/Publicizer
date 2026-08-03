# Publicization semantics

This document describes what the publicization engine actually does, member by member and rule by rule. It is not user documentation — the README is the user-facing contract; this covers the implementation's observable behavior, including corners the README doesn't mention.

It does two jobs, and they call for different kinds of writing:

- **The colon form is characterized.** What is recorded here is behavior as it is, accidents included, because it is frozen: real builds depend on it and it cannot change. Sections about it state what happens and cite the test that pins it. They do not argue.
- **The structured form is specified.** It is unreleased, so its semantics are still being chosen rather than discovered. Sections about it may record *why* a rule is the way it is — but only where the reasoning is not recoverable from the rule itself, typically an asymmetry between the two forms that would otherwise read as a bug and get "fixed".

Design rationale that fits neither — why an implementation is shaped a certain way, what a change cost — belongs in the commit that made it.

Every claim below is pinned by a test. Test names refer to `src/Publicizer.Tests/PublicizeAssemblyCharacterizationTests.cs` unless stated otherwise. The engine itself lives in `src/Publicizer/PublicizeAssemblies.cs` (traversal and edits), `src/Publicizer/PublicizeItemParser.cs` (which items mean what), `src/Publicizer/AssemblyPlan.cs` and `src/Publicizer/TypePlan.cs` (which target matches what), and `src/Publicizer/AssemblyEditor.cs` (what publicizing does).

## Targets

A target is a `Publicize` or `DoNotPublicize` item. It comes in a **colon form**, where the whole target is packed into `Include`, and a **structured form**, where `Include` is the bare assembly name and each qualifier is its own metadata. `PublicizeItemParser` reads both into the same model; there is no second code path.

The two cannot be combined on one item — a colon spec carrying `Namespace` or `Type` metadata is a build error (`StructuredTargetTests.ColonFormCombinedWithStructuredMetadata_IsRejected`), because honoring one and dropping the other would publicize something the author did not ask for.

### The colon form

Two shapes, distinguished by the presence of a colon:

- `AssemblyName` — the assembly form.
- `AssemblyName:MemberName` — the member form.

The split happens at the **first** colon (`PublicizeItemParser.TryApply`). Everything before it is the assembly name; everything after it, including any further colons, is the member name (`GetPublicizerAssemblyContextsTests.MemberSpec_SplitsOnFirstColonOnly`).

Assemblies are identified by reference file name without extension, compared against the `Filename` metadata of each `ReferencePath`. Lookup is an ordinal dictionary lookup, so it is **case-sensitive** (`GetPublicizerAssemblyContextsTests.AssemblyNames_AreCaseSensitive`). A reference not named by any target is skipped untouched.

### Member names

Member names are matched by **exact string equality** — despite the field being named `PublicizeMemberPatterns`, there is no globbing or wildcard matching in the member form. The strings compared against are:

| Kind | String |
|---|---|
| Type | dnlib's `TypeDef.ReflectionFullName` |
| Field | `{ReflectionFullName}.{FieldName}` |
| Method | `{ReflectionFullName}.{MethodName}` |
| Property | `{ReflectionFullName}.{PropertyName}` |

This has direct consequences for the syntax users must write:

- Nested types use `+`: `Fixture.Shapes+Inner` (`NestedMember_AlsoPublicizesEnclosingType`).
- Generic types carry arity, backtick included: ``Fixture.GenericHolder`1.GenericField`` (`SingleMember_GenericField_MatchesArityMangledName`).
- Constructors are `.ctor`, producing the doubled dot in `Fixture.Shapes..ctor` (`SingleMember_Constructor`). Static constructors are `.cctor`.
- Types in the global namespace have no prefix at all: `GlobalType.someField` (`SignatureClosureTests.GlobalNamespaceType_IsNamedWithoutNamespacePrefix`, issue #14).
- Methods have no parameter list, so a target names *all* overloads at once. This is the single biggest limitation of the current model and the main motivation for a real parser.
- Because names are compared without regard to member kind, one target string can match a type, a field, a method and a property simultaneously if they happen to share a name.

The comparison is not literally a lookup of the concatenated name. `AssemblyPlan` decomposes each target once, up front, into every `(type, member)` pair it could denote, so deciding a member is a per-type set lookup rather than a rebuilt string. The target is genuinely ambiguous — `A.B.C` may name a type, or member `C` of type `A.B`, and the syntax offers no way to say which — so it is indexed under *every* split point rather than resolved to one reading. That is equivalent to the string comparison it describes, including the doubled dot of `Fixture.Shapes..ctor`, which a split at the last dot would get wrong.

### The structured form

`Namespace` and `Type` metadata on a bare-assembly `Include` name a **scope**: a namespace, or a type and everything in it. Scopes are recursive by default and narrow as qualifiers are added. Tests are in `StructuredTargetTests`.

- `Namespace="A.B"` covers `A.B` and every namespace under it, on segment boundaries — `A.B.C` is inside it, `A.BX` is not (`NamespaceScope_IsRecursiveOnSegmentBoundaries`).
- `Type="Outer.Inner"` names a nested type: **dots separate nested types, and the namespace goes in `Namespace`**. This is the point of the form — it is what removes the namespace-vs-nested-type ambiguity the colon form cannot express. A `+` in `Type` is an error pointing at `.`.
- A type scope reaches the nested types inside it (`TypeScope_CoversNestedTypes`).
- `Type` with no `Namespace` is a type in the global namespace (`TypeScope_WithoutNamespace_IsTheGlobalNamespace`).
- A namespace scope always names a non-empty namespace, because omitting `Namespace` entirely would be the bare `Include="Asm"` item, which is the frozen assembly form. Nothing is lost by this: recursion means the global namespace contains every namespace, so a recursive global scope and the whole assembly are the same set of types — the assembly form already *is* that scope. What no scope can express is a *non-recursive* namespace, and that is uniform: `Namespace="A"` cannot mean "A but not A.B" either.
- Generic arity is written `MyType{T1,T2}` and lowered to `MyType`` `2``. Only the count is read; the names inside the braces mean nothing until `Parameters` lands, and a nested argument list is an error rather than a guess at which commas to count. A backtick in `Type` is an error, deliberately: accepting both spellings would leave overload targeting to reconcile them later.
- Member-level qualifiers (`Field`, `Method`, `Property`, `Event`, `Accessor`, `Parameters`) are reserved and **rejected**, not ignored, so a target written against the eventual syntax fails loudly rather than silently sweeping a whole type (`MemberQualifiers_AreRejectedUntilTheyAreImplemented`).

Malformed items are reported as build errors, and every item is parsed regardless so one bad target does not hide the rest (`RejectedItems_AreAllReported_NotJustTheFirst`).

#### Naming a type means different things in the two forms

This is a deliberate divergence, pinned by `TypeScope_SweepsMembers_UnlikeTheColonForm`:

| Target | Effect |
|---|---|
| `Include="Asm:N.T"` | Publicizes `T`'s own accessibility. Members untouched. |
| `Include="Asm" Namespace="N" Type="T"` | Publicizes `T` **and every member in it**. |

Both readings are supported permanently. The colon form's is frozen behavior; the structured form's follows recursive-by-default, and is what retires the old wart that "all members of one type" was expressible only as a regex.

#### Scope-level filters

`IncludeVirtualMembers`, `IncludeCompilerGeneratedMembers` and `MemberPattern` can sit on any scope, and a scope inherits whatever it does not set from the assembly (`ScopeFilters_OverrideTheAssemblySweep_AndInheritWhenAbsent`, `ScopeMemberPattern_AppliesOnlyInsideTheScope`). This is the one place the two forms differ in capability rather than spelling: on the colon form these are still assembly-only, and still last-wins across duplicate items.

All three are rejected on a `DoNotPublicize` scope (`ScopeFiltersOnDoNotPublicize_AreRejected`). The booleans have no defensible reading — `IncludeVirtualMembers="false"` on a deny scope would mean "do not deny the virtuals", a double negative whose misreading publicizes more than the author asked for. `MemberPattern` does have one, "deny only the members it matches", but that makes a scope a per-member rule rather than all-or-nothing for a type, which the single-winner resolution below cannot express; it is rejected as not-yet-supported rather than as nonsense.

Naming a *scope* leaves the filters in force. Naming an individual *member* still bypasses them, as it always has — so sweeping a type does not silently publicize its compiler-generated event backing fields, which is the CS0229 collision `IncludeCompilerGeneratedMembers` exists to prevent (issue #9).

### Events are not a member kind

The engine iterates types, properties, methods and fields. It never iterates `TypeDef.Events`. An event therefore cannot be targeted as an event.

It works anyway, by coincidence: a field-like event's compiler-generated backing field has the same name as the event, so `DoNotPublicize` on the event name matches the *field* and excludes it (`DoNotPublicizeEvent_ByName_ExcludesBackingField`). This is the documented workaround for issue #141, and it works only for field-like events — not for events with explicit `add`/`remove` accessors.

### Assembly-form metadata

Three metadata attributes are read, and **only from assembly-form items** (`PublicizeItemParser.ApplyAssemblyForm`):

| Metadata | Default | Effect |
|---|---|---|
| `IncludeCompilerGeneratedMembers` | `true` | When false, skip anything carrying `[CompilerGenerated]` |
| `IncludeVirtualMembers` | `true` | When false, skip virtual methods (and virtual property accessors) |
| `MemberPattern` | none | Regex; a member is only publicized if the pattern matches its name string |

Defaults apply when the metadata is absent *or unparseable* — `bool.TryParse` failure falls back to `true` rather than erroring (`TaskItemExtensions.cs:12-30`, `TaskItemExtensionsTests.IncludeCompilerGeneratedMembers_GarbageMetadata_DefaultsToTrue`), so `IncludeVirtualMembers="yes"` silently means `true`.

Putting this metadata on a member-form item has no effect; it is read but never stored. `DoNotPublicize` items never read metadata at all. This asymmetry is a known wart — it was raised while designing the "publicize a whole type" feature (issue #100), and is why that feature shipped as assembly-level `MemberPattern` rather than a type-level `IncludeMembers`. Consequently **the only way to publicize all members of one type is a regex** anchored on the type name.

When several assembly-form `Publicize` items name the same assembly, the metadata of each **overwrites** the previous — last item wins, with no merge and no diagnostic. The overwrite is unconditional per field, so a later item that sets none of the metadata resets all of it to defaults, silently discarding an earlier item's `MemberPattern` (`GetPublicizerAssemblyContextsTests.DuplicateAssemblyWidePublicizes_LastMetadataWins`).

The regex is applied to the same name strings listed above, and it is applied to types as well as members. It is unanchored, so `MemberPattern="Protected"` matches anywhere in the name (`WholeAssembly_WithMemberRegexPattern`).

## Precedence

For each field, method and property the engine walks a fixed decision ladder and stops at the first rule that applies (`TypePlan.DecideMember`, with the type's own tail in `TypePlan.DecideType`):

1. **Accessor of an excluded property** (methods only) — skip.
2. **`DoNotPublicize` names this member exactly** — skip.
3. **`Publicize` names this member exactly** — publicize, ignoring every filter.
4. **`DoNotPublicize` names the declaring type** (colon form) — skip.
5. **The narrowest scope covering this type** — publicize or skip according to that scope, subject to the compiler-generated, regex and virtual filters resolved for it.
6. Otherwise — skip.

Rung 5 is where the assembly-wide sweep now lives, alongside namespace and type scopes. `AssemblyPlan.Resolve` picks the scope: a type scope beats a namespace scope, a longer namespace beats the namespace enclosing it, and between equally narrow scopes `DoNotPublicize` wins over `Publicize`, otherwise the later item wins (`TypeScope_BeatsAnEnclosingNamespaceScope`, `InnermostNamespaceScope_Wins`, `DoNotPublicizeScope_BeatsPublicizeScope_AtEqualSpecificity`). With no structured items in play this collapses to the old two rungs — `DoNotPublicize` on the assembly, then `Publicize` on the assembly — which is why the characterization suite is unchanged.

Note that rung 4 sits *above* every scope: a colon-form `DoNotPublicize` naming a type excludes it no matter how specific a structured `Publicize` scope covering it is. This is the one place the two forms interact, and the colon form wins because its behavior is frozen (`ColonFormDoNotPublicizeType_BeatsAnyStructuredScope`).

An assembly-wide `DoNotPublicize` does **not** get that precedence. It is the loosest scope there is, so any scope naming part of the assembly is more specific and carves an exception out of it (`AssemblyDoNotPublicize_IsCarvedOutByAScope`). This makes "deny the assembly except namespace N" expressible, and it is the same carve-out rung 3 has always given a colon-form member target over an assembly deny (`AssemblyPlanTests.ForType_NamedTargetSurvivesDoNotPublicizeAssembly`). Routing both through one lattice is the point: the alternative was a veto that answered the same question — may a narrower allow-target override an assembly-wide deny? — with `yes` for the colon form and `no` for a scope of identical specificity, so the outcome turned on which syntax the author happened to use.

The hazard this leaves is composition, not widening: the deny and the scope are often authored in different `.props` files, so the override can be a surprise to whoever wrote the deny. That is reported rather than prevented — `TryGetPublicizerAssemblyContexts` warns when a `Publicize` scope overrides an assembly-wide deny (`ScopeCarvingOutOfAnAssemblyDeny_Warns`). Note the hazard is not new and is not confined to scopes: a colon-form member target reopens a shared deny the same way, and always has, without a diagnostic.

Consequences worth naming explicitly:

- **`DoNotPublicize` beats `Publicize` at the same specificity.** Naming a member in both excludes it (`MemberInBothPublicizeAndDoNotPublicize_DoNotPublicizeWins`).
- **Specific beats general.** An explicit member `Publicize` overrides a type-wide or assembly-wide exclusion (`ExplicitMemberPublicize_BeatsDoNotPublicizeType`). This is what makes the README's "publicize specific ignored members" pattern work.
- **Rule 3 bypasses all three filters, by design.** An explicitly named member is publicized even if it is compiler-generated, even if it is virtual, and even if `MemberPattern` doesn't match it — the ladder reports such a hit as `PublicizeDecision.Explicit`, and the walk then calls `AssemblyEditor.PublicizeProperty`/`PublicizeMethod` with `includeVirtual: true` rather than the assembly's setting (`ExplicitMemberPublicize_IgnoresIncludeVirtualMembers`). This is the documented escape hatch: the README's "you can still publicize specific ignored members by specifying them explicitly" pattern depends on it. Naming a member is treated as an unambiguous opt-in that outranks every blanket filter, so **a rewrite must preserve this** — removing it would silently un-publicize members for anyone following the documented pattern, surfacing as an unexplained CS0122.
- **Excluding a property excludes its accessors.** Properties are processed before methods; a `PublicizeDecision.DeniedExplicitly` on a property registers its getter and setter in a per-type set that the method loop consults first. So `WholeAssembly_ExceptOneProperty_LeavesAccessorsUntouched` holds. Note the ordering: because rule 1 precedes rule 3, a `DoNotPublicize` on a property beats an explicit `Publicize` on one of its accessor methods by name.
- **Filters do not compose as an OR.** With `MemberPattern` set *and* `IncludeCompilerGeneratedMembers="false"`, both must pass.

## What publicizing does

- **Field** — access bits cleared, `Public` set. Unconditional; `IncludeVirtualMembers` has no meaning for fields (`AssemblyEditor.cs:65`).
- **Method** — access bits cleared, `Public` set, unless `includeVirtual` is false and the method is virtual, in which case nothing happens (`AssemblyEditor.cs:53`).
- **Property** — the property itself has no accessibility in IL; publicizing one means publicizing its getter and setter, each subject to the virtual check (`AssemblyEditor.cs:36`).
- **Type** — visibility bits cleared, then `Public` for a top-level type or `NestedPublic` for a nested one. The engine then **walks up the declaring-type chain** and does the same to every enclosing type, because a nested type is unreachable unless all its enclosers are too (`PublicizeAssemblies.PublicizeTypeAndEnclosers`). Pinned by `PublicizeType_ByName_PublicizesTypeAndWalksUp` and `NestedMember_AlsoPublicizesEnclosingType`. The walk-up stops at an enclosing type named in `DoNotPublicize`: it is the engine's own inference, so it yields to what the user asked for by name, leaving the nested type public but unreachable (`DoNotPublicizeType_SurvivesTheWalkUpFromItsNestedTypes`).
"Named" means name equality, in either item form, and deliberately not `PublicizeScope.Covers`: a deny `Type="Deep"` sweeps `Deep.Mid` but does not name it, so the walk publicizes Mid and stops at Deep (`StructuredDoNotPublicizeTypeScope_StopsOnlyAtTheTypeItNames`). A `Namespace` scope names no type, so it never stops the walk — stopping would defeat an explicit carve-out for a type nothing could then reach (`StructuredDoNotPublicizeNamespaceScope_DoesNotStopTheWalkUp`). Making the gate uniform over coverage is the tempting refactor and breaks both.

Each of these returns whether it actually changed anything, so publicizing an already-public member reports no modification.

### Types are publicized implicitly

A type is publicized if **any** member in it was publicized, before the type's own rules are consulted. This short-circuit lives in the walk, not in the matcher. Only if no member was publicized does the engine evaluate the type against the ladder above.

Because the implicit case short-circuits, it outranks the type's own exclusions:

- A type named in `DoNotPublicize` still becomes public if an explicitly named member of it was publicized — though its other members stay untouched (`ExplicitMemberPublicize_BeatsDoNotPublicizeType`).
- A type that the `MemberPattern` regex rejects still becomes public if one of its members matched.

This is defensible — a publicized member is useless in an inaccessible type — but it means "do not publicize this type" does not guarantee the type's accessibility is preserved.

## Whole-assembly behavior

`Publicize Include="MyAssembly"` with no member targets publicizes every type and every member subject to the filters (`WholeAssembly_Defaults`). Notable interactions:

- `IncludeVirtualMembers="false"` leaves virtual methods and virtual property accessors alone but does not affect fields or types (`WholeAssembly_ExcludingVirtualMembers`).
- `IncludeCompilerGeneratedMembers="false"` skips anything with `[CompilerGenerated]`, which is what keeps event backing fields private and avoids the CS0229 collision that motivated the flag in issue #9 (`WholeAssembly_ExcludingCompilerGeneratedMembers`, `EventBackingField_WholeAssemblyDefault_BecomesPublic_TheCollision`, `EventBackingField_ExcludingCompilerGenerated_StaysPrivate`). The check looks only for that one attribute by full name; it does not recognise other compiler conventions such as `<>`-mangled names.
- `DoNotPublicize Include="MyAssembly"` suppresses the whole-assembly sweep but does not suppress explicit member targets, nor structured scopes naming part of the assembly (`DoNotPublicizeAssembly_PublicizesNothing` covers the sweep; rule 3 and the precedence section above cover the two exceptions).

## Diagnostics

The engine is quiet by design, which the rewrite intends to change:

- A target that matches nothing produces **no diagnostic at all** (`PublicizeTarget_MatchesNothing_PublicizesNothingAndReturnsFalse`). Typos in a member name are silent.
- If an assembly is targeted but nothing in it was publicized, a warning is logged and the reference is left pointing at the original assembly (`PublicizeAssemblies.cs:118`).
- A `DoNotPublicize` naming an assembly that no `Publicize` mentions creates a context anyway, so the assembly is processed, publicizes nothing, and warns.
- Everything else is informational logging, optionally mirrored to `PublicizerLogFilePath`.

## Outside the decision tree

For completeness, behavior that surrounds publicization but isn't part of the matching rules:

- **Caching.** Output goes to `{OutputDirectory}/{assembly}.{hash}/{assembly}.dll`, where the hash covers the input assembly bytes, the resolved context, and Publicizer's own informational version (`Hasher.cs`). If that path exists, the assembly is not reprocessed. Changing any target therefore changes the path rather than invalidating in place.
- **XML documentation** next to the input assembly is copied alongside the output (`PublicizeAssemblies.cs:138-147`).
- **Writer options** set `KeepOldMaxStack`, because writing some assemblies fails otherwise (issue #42).
- **Reference swapping.** Processed references are reported on `ReferencePathsToDelete`/`ReferencePathsToAdd`, with metadata copied to the new item, and the targets file performs the substitution.

## Publicization does not close over dependencies

Publicization changes the accessibility of the members it matches by name, and nothing else. It never inspects a member's signature. The only transitive rule in the engine is the declaring-type walk-up for nested types (`PublicizeAssemblies.PublicizeTypeAndEnclosers`).

So a member can become public and still be unusable:

- A method whose **parameter** type is internal or private becomes public but stays uncallable — the caller cannot name a value to pass (`SignatureClosureTests.PublicizingMethod_DoesNotPublicizeItsParameterTypes`).
- A method whose **return** type is inaccessible is survivable via `var`, but the type still can't be named in a field, a cast, or a generic argument (`PublicizingMethod_DoesNotPublicizeItsReturnType`).
- A **field** of an inaccessible type has the same problem (`PublicizingField_DoesNotPublicizeItsFieldType`).
- Generic constraints, base types and interfaces are equally untouched.

This has gone largely unreported because **whole-assembly publicization closes over everything by accident** — if every type is public, no signature can reference an inaccessible one (`WholeAssembly_ClosesOverSignatureTypesByAccident`). The gap only bites targeted publicization, which is the minority use case today and the mode a structured-matcher rewrite is meant to make attractive. Anything that makes targeting more precise makes this more visible.

Whether to close over signatures automatically is a genuine design question, not an obvious fix: the transitive closure of a single method can drag in a large share of an assembly, and silently publicizing types the user didn't name is its own surprise. Options worth weighing are doing nothing (status quo), warning when a publicized member's signature references a type that stays inaccessible, or an opt-in closure mode.

## Type-identity scenarios that are out of scope

These are recurring support questions where the semantics are working as designed and the problem lies outside the decision tree. Documented so a rewrite doesn't mistake them for bugs to fix.

- **The publicized assembly is a compile-time substitute only.** The runtime loads the *original*. Everything in the README's "Quirks" section follows from this, and so does the override mismatch that `IncludeVirtualMembers` exists to work around (issue #72 proposed rewriting subclass overrides to compensate; declined as more ambitious than it's worth).
- **Reference assemblies publicize fine but decompile poorly**, because method bodies are already stripped before Publicizer sees them (issue #63).
- **Targets are matched against the assembly on disk that the project actually references**, which may not be the one the user is reading in a decompiler. Issue #175 is entirely this: `Dictionary` fields named `_buckets` vs `buckets` because the reference package was built from .NET Core rather than Framework. No engine change can help; a diagnostic naming the resolved path might.
- **Runtime-implementation assemblies can't be targeted unless the project references them.** `System.Private.CoreLib` is reached through `System.Runtime`, which is what the project references and therefore all that can be publicized (issue #101; the reporter's workaround was to author a stub reference assembly of the same name).
- **`IgnoresAccessChecksToAttribute` can collide** with another definition in the compilation, e.g. MonoMod.Utils, producing CS0436 (issue #163). This is the runtime strategy, not publicization.

## Known-questionable behavior

Collected here as rewrite input. None of these are bugs the current tests fail on; they are the places where the current model and a reasonable model diverge.

1. Method targets cannot select an overload.
2. Member names are matched by exact string, and the member kind is not part of the match, so a single target may hit several unrelated members.
3. Events cannot be targeted; the backing-field name collision is the only handle, and it doesn't exist for events with explicit accessors.
4. Explicit member targets bypass `IncludeVirtualMembers` intentionally, but because a target names *all* overloads at once, a target aimed at a non-virtual overload also publicizes a virtual one sharing the name — reintroducing the override mismatch the filter exists to prevent, without the user having opted into it for that member. This is a consequence of (1), not of the escape hatch itself, and overload-precise targeting would remove it.
5. Assembly-form metadata is last-wins across duplicate items, without a diagnostic.
6. Unparseable boolean metadata falls back to `true` instead of failing.
7. A no-match target is silent.
8. Assembly name matching is case-sensitive, which is surprising on Windows.
9. `DoNotPublicize` on a type does not prevent that type from being made public via an explicitly targeted member.
10. `DoNotPublicize` on a type does not extend to its nested types; each nested type has its own reflection name and must be named separately (`DoNotPublicizeType_DoesNotExtendToNestedTypes`). Publicizing those nested types no longer drags the excluded encloser public via the walk-up (`DoNotPublicizeType_SurvivesTheWalkUpFromItsNestedTypes`).
11. Publicization does not close over signature types, so targeted publicization can produce public-but-unusable members.
12. ~~Per-item options only exist on the assembly form, so "all members of this type" is only expressible as a regex.~~ Fixed for the structured form: a `Type` scope sweeps the type and carries its own filters. Still true of the colon form.
