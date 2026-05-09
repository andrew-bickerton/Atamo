# 0004. Initial project layout

- **Status:** Accepted
- **Date:** 2026-05-09

## Contents

- [Context](#context)
- [Decision](#decision)
- [Alternatives considered](#alternatives-considered)
- [Consequences](#consequences)
- [References](#references)

## Context

The repository has reached the point where design is settled enough for code to start landing. The first physical decisions — how many `.csproj` files exist, what they are named, what depends on what — are about to be made. Those decisions are unusually load-bearing because they shape every subsequent change: every new type has to live somewhere, and "somewhere" is established by whatever the first scaffolding commit creates.

Two pressures push toward making this a deliberate decision rather than an accident:

- The 2015 design committed to a layout (`Atamo.Hub`, `Atamo.SDK`, `Atamo.Service`, `Atamo.Persistence`, `Atamo.Agents.Samples`) that no longer matches the 2026 architecture. The Source/Agent role split, the inbox model, and the swap-point philosophy each pull on the layout differently than they did then. Picking up the 2015 layout out of inertia would be wrong.
- ATAMO's swap-point story (every layer below the public API is replaceable) requires a project topology that lets adapter packages depend only on stable contracts, not on default implementations. That requirement is structural, not aesthetic: get the topology wrong and every adapter package transitively pulls in things it should not need.

This ADR resolves the "Project layout and solution structure" entry in [`../design/open-questions.md`](../design/open-questions.md). It does **not** resolve later-stage decisions (where adapter packages live, what the standalone-host project looks like, how samples are organised); those become forced when the corresponding work begins.

## Decision

The repository's initial source layout is:

```
Atamo.sln
Directory.Build.props
Directory.Packages.props
.editorconfig

src/
  Atamo.Abstractions/          interfaces, message records, contracts. No logic.
    Atamo.Abstractions.csproj
  Atamo/                       default implementations + fluent builder API.
    Atamo.csproj               references Atamo.Abstractions

tests/
  Atamo.Tests/                 unit tests for the core library (xUnit).
    Atamo.Tests.csproj         references Atamo
```

Two source projects, one test project. No samples, no standalone host, no companion packages, no adapter packages — yet. Each of those is added when its forcing function arrives, not before.

**Naming convention:** `Atamo` is the canonical NuGet handle and the package most consumers reference. Adjacent packages — `Atamo.Abstractions` now, `Atamo.Inbox.RabbitMQ` / `Atamo.Persistence.Marten` / `Atamo.Governor.OpenTelemetry` / `Atamo.Agents.Common` / `Atamo.Server` later — are all `Atamo.<Concern>`. No `Atamo.Hub`, `Atamo.Core`, or `Atamo.SDK`.

**Cross-cutting infrastructure:**

- `Directory.Build.props` at repo root sets `TargetFramework=net9.0`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `LangVersion=latest`. Settings live in one place; per-project `.csproj` files stay minimal.
- `Directory.Packages.props` enables central package management (`<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`). All NuGet versions are declared once, centrally; per-project `<PackageReference>` entries omit version attributes. This is the modern .NET default and prevents version drift between projects.
- `.editorconfig` carries the C# style rules.

**Test framework:** xUnit. Matches the dominant .NET-native convention and the existing CI workflow's `dotnet test` invocation.

**What is deliberately deferred** (and why):

| Item | Reason for deferral |
|---|---|
| `samples/` directory | The email-ingest tutorial is the v0 forcing function ([guide 1](../guides/01-async-email-ingest.md)). Its runnable counterpart is substantial work that earns its own milestone. A "hello-agent" placeholder before that would just be code-shaped noise. (Note: at the time this ADR was accepted, the tutorial layout placed all samples under `docs/design/first-sample.md`; the curriculum was later restructured into `docs/guides/`. The decision the ADR records — defer the runnable sample — stands.) |
| `Atamo.Hosting` (separate registration glue package) | Splittable later. v0 has one default for every interface; carving registration into its own package is premature infrastructure. |
| `Atamo.Server` (standalone HTTP/gRPC host) | Per [`host.md`](../design/components/host.md), the standalone host adds no primitives the embedded library does not have. Wait until the embedded API is stable enough that a thin wrapper is genuinely thin. |
| `Atamo.Agents.Common` companion package | Companion package, not core. Create when there is an actual reusable pattern to extract — not before. The placeholder for it lives in the [non-goals of `vision.md`](../design/vision.md#non-goals) and in agent.md's note on dedupe wrappers. |
| Adapter projects (`Atamo.Inbox.RabbitMQ`, `Atamo.Persistence.Marten`, `Atamo.Governor.OpenTelemetry`, etc.) | Downstream of having stable inbox / persistence / Governor contracts. v0 ships only the in-process defaults. |
| `Atamo.IntegrationTests`, `Atamo.SampleApp.Tests` | No integration or sample to test yet. A premature split is busywork. |
| Solution folders for grouping | Nice-to-have once there are >5 projects. Two source projects and one test project do not need folders. |

## Alternatives considered

**Single `Atamo` package containing both abstractions and default implementations** (the MediatR shape). Rejected because the swap-point story is structurally weaker. Adapter packages — `Atamo.Inbox.RabbitMQ`, `Atamo.Persistence.Marten`, etc. — should depend only on contracts, not on the default implementations they replace. A single package forces adapters to either pull in everything (wasteful and creates dependency cycles in spirit) or for ATAMO to ship abstractions twice (once embedded, once extracted later under pain). The Microsoft.Extensions.* shape is the conservative, well-trodden path here.

**Full split into `Atamo.Abstractions` + `Atamo.Hub` + `Atamo.Hosting` + `Atamo.Persistence`** (akin to the 2015 plan plus a Hosting carve-out). Rejected for v0 because there is one default implementation per interface and one builder API; splitting them across multiple packages adds project-reference ceremony without delivering swap-point benefit yet. The split can be re-introduced later if a second registration model or a second hub implementation lands. Premature carving is the more common mistake than late carving.

**Keep the 2015 names (`Atamo.Hub`, `Atamo.SDK`).** Rejected. `Atamo.Hub` makes the canonical package's name an implementation detail — leaving "what is the package called when the hub is just one thing inside it?" — and `Atamo.SDK` is redundant with the modern .NET pattern of `Atamo.Abstractions`. ADR 0001 already retired the surrounding 2015 vocabulary; the project names should follow.

**No central package management; per-project versions.** Rejected. Central package management is the modern .NET default for multi-project repos, prevents silent version drift, and costs only the small habit of editing `Directory.Packages.props` when adding a dependency. The trade is unambiguously favourable.

**MSTest or NUnit instead of xUnit.** Considered briefly. xUnit is the dominant .NET-native choice in 2026 and matches the existing CI workflow's expectations. No reason to deviate without a specific need.

## Consequences

**Easier:**

- The first PR that introduces a contract knows where it lives: `Atamo.Abstractions`. The first PR that introduces a default implementation knows where it lives: `Atamo`. There is no per-PR debate about project boundaries.
- Adapter packages, when they arrive, depend only on `Atamo.Abstractions` and have a clear seam.
- The CI workflow ([`.github/workflows/dotnet-ci.yml`](../../.github/workflows/dotnet-ci.yml)) needs no changes — it already invokes `dotnet restore`/`build`/`test` generically against whatever solution is present.
- The repository can advertise itself accurately on first impression: a real `dotnet restore` on `Atamo.sln` succeeds, even before the first interface lands.

**Harder:**

- Every type living in `Atamo` that turns out to need to be in `Atamo.Abstractions` (or vice versa) requires a small refactor when it is moved. This is mitigated by the discipline that `Atamo.Abstractions` contains _only_ contracts and message records — anything with non-trivial logic belongs in `Atamo`.
- Central package management means that adding a NuGet dependency requires editing two files (`Directory.Packages.props` and the `.csproj`). This is a small ergonomic cost and the standard 2026 pattern.
- The two-project split means anyone who only wants the contracts (e.g. an agent author writing against `Atamo.Abstractions` from a separate codebase) needs to know which package to reference. Documentation will need to make this clear.

**New questions opened:**

- **What lives in `Atamo.Abstractions` exactly.** The boundary between "contract" and "default implementation" is generally clear (interfaces, records, attributes, exception types live in Abstractions; everything else lives in `Atamo`) but edge cases will arise. To be settled case-by-case as types are introduced.
- **When the next layout decision is forced.** This ADR explicitly defers `samples/`, `Atamo.Server`, `Atamo.Hosting`, adapter packages, and `Atamo.Agents.Common`. Each becomes a follow-up ADR (or a brief amendment to this one) when the corresponding work begins.
- **Multi-targeting.** v0 targets `net9.0` only. Whether to multi-target older frameworks (`net8.0`?) for broader consumer reach is undecided; probably no, on the grounds that "core knows nothing about its use cases" and the audience is developers willing to upgrade. Worth re-examining if a real consumer asks.

## References

- [Project layout open question](../design/open-questions.md) (the section this ADR resolved; now removed from that list).
- [`docs/design/architecture.md`](../design/architecture.md) — swap-point table that motivates the abstractions/default split.
- [`docs/design/principles.md`](../design/principles.md) — "sensible defaults, swappable layers" pulls toward the contract/implementation split.
- [`docs/design/vision.md`](../design/vision.md) — "what success looks like" section that this layout serves.
- [`docs/design/components/host.md`](../design/components/host.md) — confirms standalone host is a thin wrapper, justifying its deferral.
- [ADR 0001](0001-rename-2015-vocabulary.md) — retired the 2015 component names that the project names also leave behind.
- Microsoft.Extensions.* package family — the convention this layout follows.
- Wolverine, MediatR — adjacent .NET libraries whose layout choices informed the alternatives considered.
