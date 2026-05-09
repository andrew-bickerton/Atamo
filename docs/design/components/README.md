# Components

This directory holds one page per core ATAMO abstraction. Each page covers what the component does, how it interacts with the rest of the system, and what is still open about its design. The high-level overview that connects them all lives in [`../architecture.md`](../architecture.md).

## Contents

- [Hub](hub.md) — the routing engine.
- [Source](source.md) — the role for components that inject messages into the hub.
- [Agent](agent.md) — the role for components that pull from inboxes and process messages.
- [Inbox](inbox.md) — the per-agent durable queue.
- [Routing rule provider](routing.md) — the extensibility point for routing logic.
- [Governor](governor.md) — the audit, policy, and telemetry layer.
- [Host](host.md) — the application that contains the hub.

## Components not yet on their own page

Two abstractions appear in the architecture but do not yet have their own page, because they are simple enough that a paragraph in [`../architecture.md`](../architecture.md) covers them adequately:

- **Message** — the unit of work. Immutable; carries a correlation ID, a principal, a type/key, and a payload.
- **Principal** — the identity on whose behalf a message is processed. Carries credentials and quotas. Tenants and groups are built on principals.

Each will get its own page when there is enough substance to justify one. Adding pages prematurely produces noise; deferring them until they earn their place keeps the documentation honest.

## Conventions

Each component page follows the same structure:

- **Contents** — table of contents with anchor links.
- **Overview** — what the component is, in two or three sentences.
- **Responsibilities (or contract)** — what the component does, what it does not do.
- **Relationships** — how it interacts with adjacent components.
- **Open questions** — what is still undecided about this component, with cross-references to [`../open-questions.md`](../open-questions.md) where relevant.
- **References** — links to related component pages, ADRs, and external sources.

Pages should stay focused on the component they describe. Cross-references to other component pages are encouraged; duplicating content from other pages is not. When a topic spans multiple components (e.g. the relationship between Inbox and Governor), the discussion lives on whichever page it is most natural to and the other page links to it.
