# Contributing

Thanks for your interest in improving Publicizer.

## Building and testing

The repo targets the .NET SDK pinned in `global.json`.

```sh
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

CI runs the same build and tests on Ubuntu and Windows.

## Pull requests

- Keep changes focused; one logical change per PR.
- Every PR must carry exactly one `semver:*` label — `semver:major`,
  `semver:minor`, or `semver:patch` — describing the largest release impact
  of the change. CI fails without it. When in doubt, use `semver:patch`.
- Make sure the build and tests pass before marking the PR ready.

## Reporting bugs

Open an issue with a minimal repro: the assembly/member you're publicizing,
your `Publicize` item configuration, and the observed vs. expected behavior.
