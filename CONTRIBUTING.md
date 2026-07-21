# Contributing

Thanks for your interest in improving Publicizer.

## Proposing a change

Before writing code, please **start a [Discussion](https://github.com/krafs/Publicizer/discussions)**
in the Ideas category describing what you'd like to change and why. We'll sort
out the approach there; once it's agreed, it gets promoted to a tracked issue
and you're clear to open a PR. This keeps direction aligned before anyone
invests in an implementation.

Concrete bugs can skip straight to a bug-report issue.

## Building and testing

The repo targets the .NET SDK pinned in `global.json`.

```sh
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

CI runs the same build and tests on Ubuntu and Windows.

## Pull requests

- For anything beyond a small fix, make sure the change was discussed first
  (see above) — unsolicited PRs that change behavior may not be accepted.
- Keep changes focused; one logical change per PR.
- Every PR must carry exactly one `semver:*` label — `semver:major`,
  `semver:minor`, or `semver:patch` — describing the largest release impact
  of the change. CI fails without it. When in doubt, use `semver:patch`.
- Make sure the build and tests pass before marking the PR ready.

## Reporting bugs

Open an issue with a minimal repro: the assembly/member you're publicizing,
your `Publicize` item configuration, and the observed vs. expected behavior.
