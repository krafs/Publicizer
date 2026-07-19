#!/usr/bin/env bash
#
# Resolve the next release version from PR semver labels.
#
# With OVERRIDE set, validates and uses it verbatim. Otherwise finds the
# latest stable vX.Y.Z tag, collects the PRs squash-merged since then, reads
# each PR's semver:{major,minor,patch,none} label, and bumps from the highest.
#
# Aborts if any PR since the base tag is unlabeled, or if every PR is
# semver:none (nothing to release).
#
# PR discovery relies on the trailing "(#123)" that GitHub's *squash* merge
# writes into the commit subject. Merge/rebase merges won't be found, so the
# repo is expected to squash-merge into main.
#
# Env:
#   OVERRIDE  optional version override, no leading v (e.g. 2.4.0)
#   GH_TOKEN  token for `gh pr view` (required unless OVERRIDE is set)
#
# Emits `version` to GITHUB_OUTPUT and a note to GITHUB_STEP_SUMMARY when those
# are set, and always prints the resolved version to stdout. Run locally from a
# full clone to preview: OVERRIDE= ./scripts/resolve-version.sh

set -euo pipefail

OVERRIDE="${OVERRIDE:-}"

semver_re='^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$'
base_tag=""
bump=""

if [[ -n "$OVERRIDE" ]]; then
  [[ "$OVERRIDE" =~ $semver_re ]] || { echo "::error::Invalid version override: $OVERRIDE"; exit 1; }
  version="$OVERRIDE"
else
  base_tag="$(git tag --list 'v*' --sort=-v:refname | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | head -n1 || true)"
  if [[ -z "$base_tag" ]]; then
    echo "::error::No version tag found. Set the version input to seed the first release."
    exit 1
  fi
  echo "Base tag: $base_tag" >&2

  subjects="$(git log "$base_tag"..HEAD --format=%s)"
  mapfile -t prs < <(printf '%s\n' "$subjects" | sed -nE 's/.*\(#([0-9]+)\)$/\1/p' | sort -un)

  if [[ ${#prs[@]} -eq 0 ]]; then
    if [[ "$(git rev-list --count "$base_tag"..HEAD)" -eq 0 ]]; then
      echo "::error::No commits since $base_tag. Nothing to release."
    else
      echo "::error::Commits exist since $base_tag but no squash-merged PRs were found."
    fi
    exit 1
  fi

  rank_of() {
    case "$1" in
      semver:major) echo 3 ;;
      semver:minor) echo 2 ;;
      semver:patch) echo 1 ;;
      semver:none)  echo 0 ;;
      *)            echo -1 ;;
    esac
  }

  overall=0
  unlabeled=()
  for pr in "${prs[@]}"; do
    pr_rank=-1
    while IFS= read -r label; do
      r=$(rank_of "$label")
      if [[ $r -gt $pr_rank ]]; then pr_rank=$r; fi
    done < <(gh pr view "$pr" --json labels --jq '.labels[].name')
    if [[ $pr_rank -lt 0 ]]; then
      unlabeled+=("$pr")
    elif [[ $pr_rank -gt $overall ]]; then
      overall=$pr_rank
    fi
  done

  if [[ ${#unlabeled[@]} -gt 0 ]]; then
    echo "::error::PRs since $base_tag missing a semver label: ${unlabeled[*]}"
    exit 1
  fi

  case "$overall" in
    3) bump=major ;;
    2) bump=minor ;;
    1) bump=patch ;;
    *) echo "::error::No version-bumping changes since $base_tag (all PRs are semver:none)."; exit 1 ;;
  esac

  IFS='.' read -r major minor patch <<< "${base_tag#v}"
  case "$bump" in
    major) major=$((major + 1)); minor=0; patch=0 ;;
    minor) minor=$((minor + 1)); patch=0 ;;
    patch) patch=$((patch + 1)) ;;
  esac
  version="$major.$minor.$patch"
  echo "Resolved $bump bump: $base_tag -> v$version" >&2
fi

if git rev-parse "v$version" >/dev/null 2>&1; then
  echo "::error::Tag v$version already exists."
  exit 1
fi

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  echo "version=$version" >> "$GITHUB_OUTPUT"
fi

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  {
    echo "## Release v$version"
    if [[ -n "$OVERRIDE" ]]; then
      echo "- Source: manual override"
    else
      echo "- Source: resolved \`$bump\` bump from \`$base_tag\`"
    fi
  } >> "$GITHUB_STEP_SUMMARY"
fi

echo "$version"
