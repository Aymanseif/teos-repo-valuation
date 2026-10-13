# TEOS Sovereign Repo Valuator

GitHub Action that values a repository as an asset: a **deterministic score 0–100**,
an **estimated dollar range**, and a **decision band** (`ALERT` when the repo looks
notable, `LOG` when it's watch-worthy, `IGNORE` otherwise).

- **100% deterministic** — the same repository always produces the same score.
  No LLM, no randomness, no remote scoring service.
- The arithmetic is in `src/Teos.RepoValuation/RepoValuationEngine.cs` (~100 lines,
  plainly auditable).

## Usage

```yaml
- uses: Aymanseif/teos-repo-valuation@v1.2.3
  id: valuation
  with:
    github-token: ${{ secrets.GITHUB_TOKEN }}

- run: |
    echo "Score:      ${{ steps.valuation.outputs.score }}"
    echo "Valuation:  ${{ steps.valuation.outputs.valuation }}"
    echo "Decision:   ${{ steps.valuation.outputs.decision }}"
```

The action runs a **deterministic smoke valuation** by default (byte-stable output,
no network dependency), so any required checks still pass reliably, and writes the
three declared outputs.

### Inputs

| Input          | Required | Default           | Description        |
| -------------- | -------- | ----------------- | ------------------ |
| `github-token` | no       | `${{ github.token }}` | `GITHUB_TOKEN` (used for higher API rate limits) |

### Outputs

| Output      | Description                        | Example            |
| ----------- | ---------------------------------- | ------------------ |
| `score`     | TEOS Score 0–100                   | `90`               |
| `valuation` | Estimated dollar range             | `$193-$442`        |
| `decision`  | `ALERT` / `LOG` / `IGNORE`         | `ALERT`            |

## How the score works

`Valuation: Score=90/100 | $193-$442 | $317 | ALERT` is built from public repo
metadata: stars, repository size (KB ⇒ estimated LOC), language count, freshness
(last update), and license presence. See the engine source for the exact factors.

## Run locally

```bash
# deterministic demo (the exact same output the action produces)
dotnet run --project src/Teos.RepoValuation/Teos.RepoValuation.csproj -- smoke

# live valuation of any public repo (needs a token for rate limits)
dotnet run --project src/Teos.RepoValuation/Teos.RepoValuation.csproj -- --repo-url https://github.com/actions/checkout
```

## License

Action license to be confirmed by the repository owner.