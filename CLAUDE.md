# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository status

ATAMO is **early design with initial scaffolding in place**. The 2026 reboot of an earlier 2015 effort.

The solution layout (per [ADR 0004](docs/adr/0004-initial-project-layout.md)):

- [`src/Atamo.Abstractions/`](src/Atamo.Abstractions/) — interfaces, message records, contracts. No logic yet.
- [`src/Atamo/`](src/Atamo/) — default implementations + fluent builder API. No logic yet; references `Atamo.Abstractions`.
- [`tests/Atamo.Tests/`](tests/Atamo.Tests/) — xUnit. Currently holds a single passing smoke test (`SmokeTests.TestInfrastructure_Runs`) so `dotnet test` has something to run.
- [`samples/Atamo.Samples.Hello/`](samples/Atamo.Samples.Hello/) — minimal console app referenced by [.vscode/launch.json](.vscode/launch.json). Scope is intentionally limited to verifying the toolchain (F5 in VS Code, `dotnet run`); it is **not** a usage sample. The v0 usage sample remains the email-triage scenario in `docs/design/first-sample.md`. ADR 0004 originally deferred all samples; this one was added later as a tooling-verification target only and should be removed or supplanted when the email-triage sample lands.

Cross-cutting infrastructure at the repo root:

- [`Directory.Build.props`](Directory.Build.props) — single source of truth for `TargetFramework=net9.0`, nullable, implicit usings, `TreatWarningsAsErrors=true`. Per-project `.csproj` files stay minimal.
- [`Directory.Packages.props`](Directory.Packages.props) — central package management. All NuGet versions live here; `<PackageReference>` entries omit `Version=`.
- [`NuGet.config`](NuGet.config) — declares `nuget.org` as the only feed, so restore is reproducible regardless of the developer's global config.
- [`.editorconfig`](.editorconfig) — C# style rules (file-scoped namespaces, usings outside namespace, etc.).

Most of the work is still design docs and ADRs. Code beyond the scaffolding has not landed yet; the design docs are the authoritative spec for what it should look like.

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
