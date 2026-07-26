#!/usr/bin/env node
//
// Summarize a Cobertura coverage report into a small Markdown table.
//
// Finds the first coverage.cobertura.xml under the given directory (default:
// coverage), reads the aggregate counts from the root <coverage> element, and
// appends a table to GITHUB_STEP_SUMMARY so the numbers show on the run page.
// Also prints to stdout so the raw job log carries them.
//
// Numbers cover the unit tests only. The E2E suite drives the task through a
// separate `dotnet build` process, which the in-process collector cannot see.
//
// Usage: node scripts/coverage-summary.mjs [coverage-dir]

import { readFileSync, appendFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

const root = process.argv[2] ?? 'coverage';

function findReport(dir) {
  for (const entry of readdirSync(dir)) {
    const p = join(dir, entry);
    if (statSync(p).isDirectory()) {
      const found = findReport(p);
      if (found) return found;
    } else if (entry === 'coverage.cobertura.xml') {
      return p;
    }
  }
  return null;
}

const report = findReport(root);
if (!report) {
  console.error(`No coverage.cobertura.xml found under ${root}`);
  process.exit(1);
}

const xml = readFileSync(report, 'utf8');
const attr = (name) => {
  const m = xml.match(new RegExp(`<coverage[^>]*\\b${name}="([^"]*)"`));
  return m ? Number(m[1]) : NaN;
};

const pct = (covered, valid) =>
  valid > 0 ? `${((covered / valid) * 100).toFixed(1)}% (${covered}/${valid})` : 'n/a';

const md = [
  '## Coverage',
  '',
  '| Metric | Coverage |',
  '| --- | --- |',
  `| Lines | ${pct(attr('lines-covered'), attr('lines-valid'))} |`,
  `| Branches | ${pct(attr('branches-covered'), attr('branches-valid'))} |`,
  '',
  '<sub>Unit tests only — the E2E suite builds in a separate process and is not instrumented.</sub>',
  '',
].join('\n');

if (process.env.GITHUB_STEP_SUMMARY) {
  appendFileSync(process.env.GITHUB_STEP_SUMMARY, `${md}\n`);
}
console.log(md);
