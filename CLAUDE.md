# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository status

ATAMO is in **pure design phase**. There is no production code yet:

- `src/` is empty — no `.csproj` or `.sln` files exist.
- `tests/Atamo.Tests/` is an empty directory placeholder.
- The CI pipeline (`.github/workflows/dotnet-ci.yml`) targets .NET 9 and runs `dotnet restore` / `build` / `test`, but those commands have nothing to operate on until a project lands.

This is a 2026 reboot of an earlier 2015 effort. Most of the work right now is design docs and ADRs; once code starts landing, the design docs are the authoritative spec for what the code should look like.

## Authoritative documentation

When in doubt about *what* to build, the order of authority is:

1. **`docs/adr/`** — Architecture Decision Records. Immutable once accepted. ADRs supersede other docs where they conflict.
2. **`docs/design/`** — current design (architecture, principles, components, samples, open questions). The high-level overview is `docs/design/architecture.md`; one-page-per-abstraction detail lives under `docs/design/components/`.
3. **`docs/recipes/`** — short, goal-oriented how-to guides.
4. **`docs/README.md`** — project-facing overview using the 2026 vocabulary.

**Stale sources — do not treat as authoritative:**

- **`docs/OldVersion/`** is the 2015 documentation kept for historical reference. The vocabulary, architecture, and component list there have been superseded.
- The repository-root **`README.md`** (Hub / Controller / EventProvider / ConfigurationProvider component list) uses the 2015 vocabulary that ADR 0001 renames. If editing it, align with the 2026 vocabulary below or with `docs/README.md`.

## Vocabulary (ADR 0001)

Use these names in design discussion, code, and docs. The 2015 → 2026 mapping:

| 2015 | 2026 |
|---|---|
| Controller | Host |
| EventProvider | Source |
| EventType / ActionType | (collapsed into Message) |
| ConfigurationProvider | Routing rule provider |
| ConfigurationRule | Routing rule |

Retained: **Hub**, **Governor**. New in 2026: **Agent** (a *role*, not a type), **Principal**, **Message**.

Source and Agent are roles, not types — a single component can be registered for both (ADR 0002). Every agent has its own durable inbox; dispatch is enqueue, not function call (ADR 0003).

## Design principles to apply when writing code

These are taken from `docs/design/principles.md` and they shape concrete choices:

- **Core knows nothing about its use cases.** No LLM, email, HTTP, or DB concepts in the core. Those exist only as agents and providers built on the public API. If a use case can't be expressed as a consumer of the public surface, the abstraction is leaking.
- **Substrate stores metadata; consumers store content.** The Governor records IDs, types, principals, routing decisions, timestamps, causal chains — never payloads. Content persistence is implemented as an agent (see `docs/recipes/persist-messages.md`).
- **Every agent contract assumes remote, even in-process.** No shared state, no synchronous callbacks into hub internals. Direct function-call dispatch is the temptation to refuse — the inbox model is the structural commitment that prevents it.
- **Compose, don't subsume.** Use existing tools rather than reimplementing: Polly (retries), OpenTelemetry (telemetry), MailKit (email), Marten / EF Core (persistence), RabbitMQ / NATS / SQS / Service Bus (industrial inboxes), Wolverine / MassTransit (transport).
- **Sensible defaults, swappable layers.** The default for each swap point (transport, inbox, persistence, credentials, agent runtime, governor sink, routing rule provider, host model) is in-process / SQLite-backed; every default is behind an interface and the registration API treats default and alternatives identically. See the swap-point table in `docs/design/architecture.md`.
- **Multi-tenancy is structural, not a future feature.** `Principal` is in the core from day one; "the user" is never a global.
- **2015 interface conventions are not preserved.** No `IHubControl` / `IHubReceiver` / `ITelemetry`-style names. Aim for current .NET idioms — `record` types for messages, `IAsyncEnumerable<T>` for streams, fluent / minimal-API-style registration, rigorous cancellation tokens.

## ADR workflow

- Write an ADR for decisions that are expensive to reverse, resolve an open question, or contradict prior settled work. Don't write ADRs for routine implementation choices.
- Filename: `NNNN-short-slug.md`, zero-padded, never reused.
- Use `docs/adr/template.md` as the starting point.
- ADRs are immutable once accepted. To change a decision, write a new ADR that supersedes the old one and update the old ADR's Status field.
- Add new ADRs to the index in `docs/adr/README.md`.

## Build and test commands (once code exists)

The CI workflow drives the canonical commands:

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=./TestResults/
```

Target framework is **.NET 9.0** (`actions/setup-dotnet@v4` with `dotnet-version: '9.0.x'`).

CI runs on push to `main`, `master`, and `trial-new-setup`, and on pull requests targeting `main` or `master`. Note the active branch is currently `trial-new-setup`; `master` is the long-lived main branch for PRs.
