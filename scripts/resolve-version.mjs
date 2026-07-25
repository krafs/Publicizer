#!/usr/bin/env node
//
// Resolve the next release version from PR semver labels.
//
// With OVERRIDE set, validates and uses it verbatim. Otherwise finds the latest
// stable vX.Y.Z tag, reads the semver:{major,minor,patch} label on every PR
// released since it, and bumps from the highest.
//
// This is the only place the label requirement is enforced. A PR-time check
// can't be: labels arrive after the PR opens, and contributors can't set them.
// What earns a line in the notes is decided separately in .github/release.yml,
// so bumps and notes stay decoupled.
//
// Env:
//   OVERRIDE  optional version override, no leading v (e.g. 2.4.0)
//   GH_TOKEN  token for `gh` (required unless OVERRIDE is set)
//
// Emits `version` and `base-tag` to GITHUB_OUTPUT and a note to
// GITHUB_STEP_SUMMARY, and prints the version to stdout. Preview locally from a
// full clone: node scripts/resolve-version.mjs

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

// Emitted as `base-tag` so the notes are bounded by the same tag as the bump.
const baseTag = git("tag", "--list", "v*", "--sort=-v:refname")
  .split("\n")
  .find((t) => /^v\d+\.\d+\.\d+$/.test(t));

if (override) {
  if (!SEMVER_RE.test(override)) die(`Invalid version override: ${override}`);
  version = override;
  source = "manual override";
} else {
  if (!baseTag) {
    die("No version tag found. Set the version input to seed the first release.");
  }
  console.error(`Base tag: ${baseTag}`);

  // Merge dates only narrow the search; ancestry decides what the tag contains.
  // `merged:>=` is inclusive, so the tag's own PR comes back -- already released.
  const since = git("log", "-1", "--format=%cI", baseTag);
  const released = new Set(git("rev-list", baseTag).split("\n"));
  const prs = JSON.parse(
    gh("pr", "list",
      "--state", "merged",
      "--base", "main",
      "--search", `merged:>=${since}`,
      "--limit", "500",
      "--json", "number,title,labels,mergeCommit"),
  ).filter((pr) => !released.has(pr.mergeCommit?.oid));
  if (prs.length === 0) die(`No merged PRs since ${baseTag}. Nothing to release.`);

  const rankOf = (pr) => Math.max(0, ...pr.labels.map((l) => RANKS[l.name] ?? 0));

  const unlabeled = prs.filter((pr) => rankOf(pr) === 0);
  if (unlabeled.length > 0) {
    console.error(unlabeled.map((pr) => `  #${pr.number} ${pr.title}`).join("\n"));
    die(`${unlabeled.length} merged PR(s) since ${baseTag} lack a semver label. Label them and re-run.`);
  }

  const rank = Math.max(...prs.map(rankOf));

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
  appendFileSync(process.env.GITHUB_OUTPUT, `version=${version}\nbase-tag=${baseTag ?? ""}\n`);
}
if (process.env.GITHUB_STEP_SUMMARY) {
  appendFileSync(
    process.env.GITHUB_STEP_SUMMARY,
    `## Release v${version}\n- Source: ${source}\n`,
  );
}

console.log(version);
