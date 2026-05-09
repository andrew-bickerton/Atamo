# Guides

Build-on-each-other tutorials that show how to solve real problems with ATAMO. Each guide adds a capability to the application built in the previous guide. Together they walk a developer from "I want incoming emails to land in my app's UI and a database" to "I have a multi-agent triage system with human-in-the-loop review and a live audit dashboard."

## Contents

- [How guides differ from recipes and samples](#how-guides-differ-from-recipes-and-samples)
- [The curriculum](#the-curriculum)
- [Conventions](#conventions)

## How guides differ from recipes and samples

- **Guides** (this directory) are linear tutorials. Each one builds on the last; the narrative arc is "your application starts simple and gains capabilities one guide at a time." Read in order if you are new to ATAMO.
- **[Recipes](../recipes/)** are short focused snippets. Each one answers a single question — "how do I persist messages of a given type?" — without surrounding application context. Reach for these when you have a working setup and need a specific technique.
- **Samples** (under [`samples/`](../../samples/)) are the runnable counterparts. Each guide will eventually have a matching `samples/Atamo.Samples.<slug>/` project once that guide's runnable artefact is built. The current `samples/Atamo.Samples.Hello/` exists only to verify the toolchain (F5 in VS Code, `dotnet run`) and is not a usage sample.

## The curriculum

| Guide | What you'll build | What ATAMO concepts it introduces |
|---|---|---|
| [Guide 1 — Receive emails into a GUI and a database](01-async-email-ingest.md) | A desktop or service app that surfaces inbound emails to a UI and persists them, with no coupling between the two consumers. | Source, Agent, message types, routing rules, fan-out. |
| [Guide 2 — Add LLM triage and outbound replies](02-add-llm-triage.md) | The application from guide 1 plus a local-LLM agent that triages each email and an SMTP agent that sends replies. | Response messages as first-class messages, recursion protection, correlation. |
| [Guide 3 — Add a history-and-live review form](03-add-history-and-live-feed.md) | A web UI that opens onto a history of recent traffic and stays open as a live feed of new messages. | Governor as queryable metadata, history-then-live pattern, standalone host. |
| [Guide 4 — Add a human reviewer to the loop](04-add-disconnected-human-review.md) | A human-in-the-loop review step for emails the LLM flags. The reviewer's decision flows through the system as a normal message. | Disconnected agents, human-time inboxes, lease extension, competing consumers. |

If you only have time for one, read **guide 1** — everything else builds on the patterns it establishes.

## Conventions

Each guide follows the same structure:

- **When you'd want this** — the problem in one paragraph, so a reader skimming the index can tell whether the guide applies.
- **What you'll build** — the components and the result.
- **The walkthrough** — incremental steps with code sketches.
- **Why this approach** — a short subsection on which ATAMO concepts are being exercised and why this shape was chosen.
- **What's not in this guide** — the things deliberately out of scope, with forward-references where applicable.
- **Definition of done** — what "working" looks like.
- **Next up** — the guide that picks up from here.

Code in guides is illustrative. The exact API surface will settle as ATAMO is implemented; guides will be updated to match. A guide should still be useful as a _pattern_ even if specific method names shift.
