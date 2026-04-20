# Coverage Showdown

A nightly comparison of two .NET code coverage tools — **Coverlet** and **Microsoft Testing Platform (MTP)** — run against the [Humanizer](https://github.com/Humanizr/Humanizer) open-source project.

**Live results: https://purekrome.github.io/codecoverage-showdown**

Results are published to GitHub Pages.

---

## How the comparison works

1. **Clone** — Humanizer's `main` branch is cloned fresh (`--depth=1`) on each run.

2. **Run each tool independently** — Coverlet and MTP each get their own clean runner. The scripts in `runners/` inject the relevant NuGet package into Humanizer's test project at runtime, remove the competing tool's package, and run the test suite via `dotnet run`.

3. **Cobertura output** — Both tools are configured to emit [Cobertura XML](https://cobertura.github.io/cobertura/). This is the common format that makes the comparison possible.

4. **Parse and compare** — The `src/CoverageCompare` .NET console app reads both XML files and extracts line rate, branch rate, and method rate at the overall and per-package level.

5. **Output** — Two files are written to `docs/data/`:
   - `latest.json` — the most recent run (summary + per-package breakdown)
   - `history.json` — all runs appended over time (up to 90 entries), used to draw the trend chart

6. **Publish** — `docs/` is deployed to GitHub Pages as a static site. The page fetches both JSON files at load time — no backend required.

---

## What the numbers mean

- **Line rate** — percentage of executable lines hit by at least one test
- **Branch rate** — percentage of branches (if/else, switch arms, ternaries) covered
- **Method rate** — percentage of methods entered by at least one test

Differences between the two tools can reflect instrumentation strategy, how each tool handles compiler-generated code, and what each tool includes or excludes by default — not necessarily a bug in either.

---

## How CoverageCompare calculates the results

`src/CoverageCompare` is a .NET 10 console app. It does no test running — it only reads the two Cobertura XML files that the shell scripts produce and turns them into JSON.

### Parsing (CoberturaParser)

Cobertura XML has a well-defined structure. The parser reads three values from the `<coverage>` root element:

- `line-rate` and `branch-rate` are top-level attributes on `<coverage>` — e.g. `line-rate="0.9099"`
- `method-rate` is not a standard Cobertura attribute, so it is derived by averaging the `line-rate` of every `<method>` element across the whole file

Per-package rates are read the same way from each `<package>` element.

### Comparison (ReportBuilder)

The two parsed reports are aligned by package name. For each package that appears in either report a `PackageComparison` row is produced. If a package only appears in one tool's output the other side shows `null` (rendered as `-` in the UI).

**Example** — given these two Cobertura files:

```
coverlet.cobertura.xml          mtp.cobertura.xml
  line-rate="0.9052"              line-rate="0.9099"
  branch-rate="0.8342"            branch-rate="0.8568"
  <package name="Humanizer"       <package name="Humanizer"
    line-rate="0.9052"              line-rate="0.9099"
    branch-rate="0.8342" />         branch-rate="0.8568" />
```

The output in `latest.json` becomes:

```json
{
  "latest": {
    "coverlet": { "lineRate": 0.9052, "branchRate": 0.8342 },
    "mtp":      { "lineRate": 0.9099, "branchRate": 0.8568 }
  },
  "packageComparisons": [
    {
      "name": "Humanizer",
      "coverletLine": 0.9052, "coverletBranch": 0.8342,
      "mtpLine":      0.9099, "mtpBranch":      0.8568
    }
  ]
}
```

The delta shown in the UI (`+0.47pp` line, `+2.26pp` branch) is calculated in the browser as `(mtp - coverlet) * 100`.

### History (history.json)

Every run prepends a `RunEntry` to `history.json` (newest first, capped at 90 entries). This file is separate from `latest.json` so the dashboard can load current results quickly and fetch the trend data independently.

---

## Running locally

```bash
# Clone Humanizer
git clone --depth=1 --branch main https://github.com/Humanizr/Humanizer.git humanizer
mkdir -p results

# Run each tool
bash runners/coverlet.sh humanizer
bash runners/mtp.sh      humanizer

# Compare and generate JSON + Atom feed
dotnet run --project src/CoverageCompare -- \
  results/coverlet.cobertura.xml \
  results/mtp.cobertura.xml \
  docs/data/latest.json \
  docs/data/history.json \
  docs/feed.xml \
  <coverlet-version> \
  <mtp-version> \
  <humanizer-sha>

# Serve the site locally
dotnet tool install --global dotnet-serve  # once
dotnet serve --directory docs/ --port 8080
```


---

## Optional: Pin Humanizer to a specific commit in CI (manual runs)

The workflow exposes an optional manual input `humanizer_commit` (visible when you click "Run workflow") that allows you to specify a commit SHA to check out in the Humanizer repo for that run. If you leave the field empty, the workflow falls back to cloning `main` shallowly — same as before.

Why use the input:
- Convenience: paste a commit SHA in the Run workflow UI and run the pipeline against that exact snapshot.
- Explicitness: the run's event payload includes the input and the jobs echo the value so reviewers can see which commit was used.

How it works in the workflow:
- The workflow defines a `workflow_dispatch` input named `humanizer_commit`.
- Jobs map that input into the `HUMANIZER_COMMIT` environment variable. If the input is empty, the workflow now also supports an optional repository Actions variable `vars.HUMANIZER_COMMIT` as a fallback (useful if you want a repo-level default).
- The clone steps echo the effective `HUMANIZER_COMMIT`. When set, the clone logic tries progressive shallow strategies to avoid a full clone:
  1. depth=1 clone of main and check if the SHA is present there
  2. fetch --depth=50 for that SHA and check again
  3. full clone fallback and checkout
  If the commit still cannot be found the job fails early with a clear error.

Where to set it (examples):
- Manual run (recommended for one-off checks): GitHub → Actions → Nightly Coverage Showdown → Run workflow → paste a SHA into the "humanizer_commit" field.
- Repository default (optional fallback): Settings → Actions → Variables → Add `HUMANIZER_COMMIT` — when present the workflow uses this value if the manual input is left empty.

Run workflow UI example (textual):

1. Open the repository on GitHub and go to Actions → Nightly Coverage Showdown.
2. Click "Run workflow".
3. In the "humanizer_commit" input box paste the commit SHA you want to test (e.g. `2a5f3b7`). Leave it empty to use main or a repo-level default.
4. Click the green "Run workflow" button.

Security note: `workflow_dispatch` inputs are not secrets — do not use this for sensitive values.

---

## Staying up to date

### Atom feed

An [Atom feed](https://purekrome.github.io/codecoverage-showdown/feed.xml) is published alongside the dashboard. Subscribe to it in any feed reader (e.g. Feedly, NetNewsWire, Reeder) to get notified whenever a new comparison run completes.

The feed is only updated when a new run actually happens — i.e. when at least one of the two NuGet packages releases a new version. If nothing changes upstream, the feed is unchanged.

### Dashboard toggle preference

The dashboard has a **⇄ toggle** that swaps which tool is treated as the baseline when calculating deltas (default: Coverlet → MTP). Your preference is saved in `localStorage` under the key `showdown-perspective` so it persists across page loads. Clearing site data resets it to the default.

---

## Repo layout

```
runners/                  # Shell scripts that drive each tool
  common.sh               # Shared library (version resolution, SDK install, Nerdbank workaround)
  coverlet.sh             # Runs Coverlet, emits results/coverlet.cobertura.xml
  mtp.sh                  # Runs MTP, emits results/mtp.cobertura.xml
src/CoverageCompare/      # .NET 10 console app — parses XMLs, writes JSON
docs/                     # GitHub Pages root
  index.html              # The comparison dashboard
  feed.xml                # Atom feed (one entry per run)
  data/
    latest.json           # Latest run summary
    history.json          # Append-only run history (trend chart data)
.github/workflows/
  nightly.yml             # The full pipeline
versions.json             # Last-known NuGet versions (skip guard)
```
