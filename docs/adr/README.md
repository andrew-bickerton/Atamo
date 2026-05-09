# Architecture Decision Records

This directory holds ATAMO's Architecture Decision Records (ADRs). An ADR captures a single significant decision: what was decided, what alternatives were considered, why this option was chosen, and what the consequences are. ADRs are how design decisions become traceable rather than tribal.

## Contents

- [When to write an ADR](#when-to-write-an-adr)
- [Format](#format)
- [Lifecycle](#lifecycle)
- [Index](#index)
- [Further reading](#further-reading)

## When to write an ADR

Write an ADR when:

- A decision is being made that will be expensive to reverse — public API shape, persistence model, naming of core concepts, choice of a foundational dependency.
- A decision resolves an entry in [`../design/open-questions.md`](../design/open-questions.md).
- A decision contradicts or refines something previously settled.
- Future contributors will reasonably ask "why was this done this way?" and the answer is non-obvious.

Do _not_ write an ADR for routine implementation choices, code-style decisions, or reversible bugfixes. The signal-to-noise ratio of the ADR set matters; a directory full of trivia loses the reader.

## Format

ADRs are markdown files named `NNNN-short-slug.md`, where `NNNN` is a zero-padded sequence number. Numbers are assigned in order of acceptance and never reused, even if an ADR is later superseded.

Each ADR follows the template in [`template.md`](template.md). The structure is:

- **Title** — a noun phrase describing the decision, not a verb phrase. _"Source and Agent as roles"_ rather than _"Decide that Source and Agent should be roles."_
- **Status** — one of: Proposed, Accepted, Superseded by NNNN, Deprecated.
- **Context** — the situation that forced the decision. What was unclear, what was at stake, what alternatives were on the table.
- **Decision** — the choice made, stated unambiguously.
- **Alternatives considered** — the other options on the table and why each was rejected.
- **Consequences** — what becomes easier, what becomes harder, what new questions are now open.
- **References** — links to relevant design docs, code, issues, external articles, prior ADRs.

ADRs are immutable once accepted. To change a decision, write a new ADR that supersedes the old one and update the old ADR's status to "Superseded by NNNN."

## Lifecycle

1. **Proposed.** ADR is drafted, usually in a pull request, and discussed.
2. **Accepted.** ADR is merged. The decision is now in force; the codebase and other docs should reflect it.
3. **Superseded.** A later ADR replaces this one. The original stays in the repository for historical context; its Status is updated.
4. **Deprecated.** The decision no longer applies, but no replacement was needed (e.g. the concern itself went away).

## Index

| Number | Title | Status |
|---|---|---|
| [0001](0001-rename-2015-vocabulary.md) | Rename 2015 component vocabulary | Accepted |
| [0002](0002-source-and-agent-as-roles.md) | Source and Agent as roles, not types | Accepted |
| [0003](0003-per-agent-inboxes.md) | Per-agent inboxes with industry-standard semantics | Accepted |

New ADRs should be added to this index in order.

## Further reading

The ADR concept comes from Michael Nygard's 2011 post _Documenting Architecture Decisions_. The format used here is a lightly adapted version of that original — short enough to write quickly, structured enough to scan later.
