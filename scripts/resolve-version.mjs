#!/usr/bin/env node
//
// Resolve the next release version from PR semver labels.
//
// With OVERRIDE set, validates and uses it verbatim. Otherwise finds the
// latest stable vX.Y.Z tag, lists the PRs merged into main since that tag,
// reads each PR's semver:{major,minor,patch} label, and bumps from the highest.
//
// Every merged PR is expected to carry exactly one semver label -- the PR
// Labels gate enforces this, so this script trusts it rather than re-checking.
// Release-note noise (dependabot, docs) is filtered separately in
// .github/release.yml, not here, so bumps and notes stay decoupled.
//
// Env:
//   OVERRIDE  optional version override, no leading v (e.g. 2.4.0)
//   GH_TOKEN  token for `gh` (required unless OVERRIDE is set)
//
// Emits `version` to GITHUB_OUTPUT and a note to GITHUB_STEP_SUMMARY when those
// are set, and always prints the resolved version to stdout. Run locally from a
// full clone to preview: node scripts/resolve-version.mjs

import { execFileSync } from "node:child_process";
import { appendFileSync } from "node:fs";

const SEMVER_RE = /^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$/;
const RANKS = { "semver:patch": 1, "semver:minor": 2, "semver:major": 3 };
const BUMPS = { 1: "patch", 2: "minor", 3: "major" };

const die = (msg) => {
  console.log(`::error::${msg}`);
  process.exit(1);
};

const git = (...args) => execFileSync("git", args, { encoding: "utf8" }).trim();
const gh = (...args) => execFileSync("gh", args, { encoding: "utf8" }).trim();

const override = process.env.OVERRIDE ?? "";
let version;
let source;

if (override) {
  if (!SEMVER_RE.test(override)) die(`Invalid version override: ${override}`);
  version = override;
  source = "manual override";
} else {
  const baseTag = git("tag", "--list", "v*", "--sort=-v:refname")
    .split("\n")
    .find((t) => /^v\d+\.\d+\.\d+$/.test(t));
  if (!baseTag) {
    die("No version tag found. Set the version input to seed the first release.");
  }
  console.error(`Base tag: ${baseTag}`);

  // PRs merged into main after the tag's commit. One query, JSON in hand --
  // no walking commit subjects for "(#123)", so merge style doesn't matter.
  const since = git("log", "-1", "--format=%cI", baseTag);
  const prs = JSON.parse(
    gh("pr", "list",
      "--state", "merged",
      "--base", "main",
      "--search", `merged:>=${since}`,
      "--limit", "500",
      "--json", "labels"),
  );
  if (prs.length === 0) die(`No merged PRs since ${baseTag}. Nothing to release.`);

  const rank = Math.max(
    0,
    ...prs.flatMap((pr) => pr.labels.map((l) => RANKS[l.name] ?? 0)),
  );
  if (rank === 0) die(`No semver-labeled PRs since ${baseTag}.`);

  const bump = BUMPS[rank];
  const [major, minor, patch] = baseTag.slice(1).split(".").map(Number);
  version =
    bump === "major" ? `${major + 1}.0.0`
    : bump === "minor" ? `${major}.${minor + 1}.0`
    : `${major}.${minor}.${patch + 1}`;
  source = `resolved \`${bump}\` bump from \`${baseTag}\``;
  console.error(`Resolved ${bump} bump: ${baseTag} -> v${version}`);
}

let tagExists = true;
try {
  execFileSync("git", ["rev-parse", "--verify", "--quiet", `refs/tags/v${version}`], {
    stdio: "ignore",
  });
} catch {
  tagExists = false; // rev-parse exits non-zero when the tag is absent
}
if (tagExists) die(`Tag v${version} already exists.`);

if (process.env.GITHUB_OUTPUT) {
  appendFileSync(process.env.GITHUB_OUTPUT, `version=${version}\n`);
}
if (process.env.GITHUB_STEP_SUMMARY) {
  appendFileSync(
    process.env.GITHUB_STEP_SUMMARY,
    `## Release v${version}\n- Source: ${source}\n`,
  );
}

console.log(version);
