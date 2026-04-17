# AGENTS.md

Compact orientation for AI coding agents. Every line answers: "would an agent miss this without help?"

---

## Repo Purpose

Nightly GitHub Action that runs two .NET coverage tools — **Coverlet** and **Microsoft Testing Platform (MTP) CodeCoverage** — against the [Humanizer](https://github.com/Humanizr/Humanizer) OSS project, compares the Cobertura XML output, and publishes results to GitHub Pages.

---

## Layout

```
runners/          # Shell scripts that drive each coverage tool
  common.sh       # Sourced library (not called directly)
  coverlet.sh     # Runs Coverlet, emits results/coverlet.cobertura.xml
  mtp.sh          # Runs MTP, emits results/mtp.cobertura.xml
src/CoverageCompare/  # .NET 10 console app — parses XMLs, emits docs/data/latest.json
  Program.cs          # Entry point (arg parsing, JSON I/O)
  CoberturaParser.cs  # Cobertura XML → model
  ReportBuilder.cs    # Merges run into history, builds OutputFile
  Models/             # Record/class types only
.github/workflows/nightly.yml  # The whole pipeline
docs/             # GitHub Pages root (docs/data/latest.json is the data file)
versions.json     # Tracks last-known NuGet versions; skip guard for the workflow
```

---

## SDK Pinning

- **This repo's `global.json`** pins SDK `10.0.100` (`rollForward: latestFeature`). It governs `CoverageCompare` only.
- **Humanizer's own `global.json`** governs the test runs. Both `coverlet.sh` and `mtp.sh` `cd` into the Humanizer clone before any `dotnet` call so Humanizer's SDK wins. Never invoke `dotnet` against Humanizer from the repo root.

---

## Central Package Management + Lock Files

- `Directory.Packages.props` — **all versions are declared here**. Never add a `Version` attribute to a `<PackageReference>` in a `.csproj`.
- `Directory.Build.props` sets `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` globally. After any package add/update, regenerate lock files:
  ```bash
  dotnet restore --force-evaluate
  ```
  CI will fail with a lock-file mismatch otherwise.

- `CoverageCompare` has **no NuGet dependencies** (BCL only), so its lock file is minimal.

---

## Building & Running CoverageCompare

```bash
# Build
dotnet build src/CoverageCompare

# Run (all 6 args required)
dotnet run --project src/CoverageCompare -- \
  results/coverlet.cobertura.xml \
  results/mtp.cobertura.xml \
  docs/data/latest.json \
  <coverlet-version> \
  <mtp-version> \
  <humanizer-git-sha>
```

- If `docs/data/latest.json` already exists, the run is **appended** to its `history` array (max 90 entries).
- Output directory is created automatically.

---

## Running the Shell Scripts Manually

```bash
# Clone Humanizer first
git clone --depth=1 --branch main https://github.com/Humanizr/Humanizer.git humanizer
mkdir -p results

bash runners/coverlet.sh humanizer [coverlet-version] [output-xml]
bash runners/mtp.sh      humanizer [mtp-version]      [output-xml]
```

Version and output path args are optional; both scripts will resolve the latest NuGet version and default to `results/*.cobertura.xml` if omitted.

---

## Key Quirks

**Nerdbank workaround.** Humanizer uses Nerdbank.GitVersioning. `--depth=1` clones break it. `common.sh:disable_nerdbank()` deletes all `version.json` files in the clone, and `-p:DisableGitVersionTask=true` is passed at build time.

**Dynamic package injection.** The runner scripts mutate Humanizer's `.csproj` at runtime (`dotnet add/remove package`) before restoring. Each script also strips the competing tool's package (coverlet removes MTP; MTP removes coverlet).

**Coverlet output has a timestamped filename.** It is found via glob `*.cobertura.*.xml` under the Humanizer dir, not a fixed path.

**MTP output lands under `artifacts/`.** MTP writes to `artifacts/bin/Humanizer.Tests/<config>/TestResults/mtp-coverage.cobertura.xml` — not `bin/Release`. The script searches the entire Humanizer dir for the filename.

**History is append-only.** `docs/data/latest.json` grows a `history` array with every run. It is committed back to the repo by the `compare` job.

---

## Workflow Job Graph

```
prepare  →  run-coverlet  ─┐
         →  run-mtp        ├→  compare  →  deploy-pages
```

- `run-coverlet` and `run-mtp` run in **parallel**, each on a fresh runner with its own Humanizer clone.
- Results are exchanged via GitHub Actions artifacts (`coverlet-result`, `mtp-result`).
- `compare` downloads both artifacts, runs `CoverageCompare`, commits `versions.json` + `docs/data/latest.json`, and uploads `docs/` as the Pages artifact.
- All jobs after `prepare` are gated on `needs.prepare.outputs.changed == 'true'`. On a scheduled run where neither NuGet package version has changed, the entire pipeline is skipped.

---

## Solution File

The repo uses **`codecoverage-showdown.slnx`** (new XML format). The old `.sln` was deleted. Only `src/CoverageCompare` is in the solution — the `temp/humanizer` tree is never part of this solution.
