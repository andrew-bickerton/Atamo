# Recipes

Recipes are short, goal-oriented guides showing how to achieve a specific outcome with ATAMO. Each one answers a question of the form "I have this problem — how do I solve it?"

## Contents

- [How recipes differ from samples](#how-recipes-differ-from-samples)
- [Recipe index](#recipe-index)
- [Conventions](#conventions)
- [Contributing recipes](#contributing-recipes)

## How recipes differ from samples

Samples are complete artefacts. The first sample, for instance, is a working email triage assistant — you can clone it, run it, and use it. Samples calibrate the *substrate*: each one exercises a coherent set of properties end-to-end and pressure-tests the abstractions.

Recipes are focused snippets. Each one shows a single technique in isolation, prescriptively, in two minutes of reading. They calibrate the *developer*: a developer with a real problem reads the index, finds the recipe that matches their problem, and applies the technique without having to derive it from architecture documents.

A useful test: if a developer says "how do I do X with ATAMO?", the answer should be either a recipe or "you can't, and here's why." Recipes are the practical layer between the design documentation and a working application.

## Recipe index

Recipes are organised by what the developer is trying to do, not by what part of ATAMO they are using.

### Persistence and querying

- [Persist messages of a given type](persist-messages.md)
- [Query previously persisted messages](query-persisted-messages.md)

### Future recipes

The following recipes are planned but not yet written. Their position in the index is provisional — additions and reorganisation are expected as the catalogue grows.

- React to messages from an external system (webhook, queue, file system)
- Emit messages from an existing application without making it a Source
- Add an agent that runs on a different machine
- Implement a request-response flow with timeout
- Route messages based on content rather than type
- Aggregate metadata from a Governor stream

## Conventions

Each recipe follows the same structure:

- **Title** — names the goal in plain language. _"Persist messages of a given type"_ rather than _"Use the MessageStore pattern."_
- **When you'd want this** — one short paragraph framing the situation. Helps a reader skimming the index decide if the recipe applies to them.
- **The technique** — a code sketch (or a few) and a brief explanation of what each piece does.
- **Variations** — common adjustments (different stores, different filters, different scaling concerns) that don't warrant their own recipe but matter in practice.
- **See also** — links to related recipes, relevant component pages, and any ADRs that constrain how the technique works.

Code in recipes is illustrative, not committed. The exact API surface will settle as ATAMO is implemented; recipes will be updated to match. A recipe should still be useful as a *pattern* even if specific method names shift.

Recipes are short by design. If a recipe is growing past two pages, it probably wants to be split into two recipes — one per goal — or replaced by a sample.

## Contributing recipes

A new recipe is worth writing when:

- A developer has asked "how do I do X?" and the answer is non-obvious from the existing documentation.
- A pattern has appeared in two or more samples or production deployments and is worth naming.
- A common pitfall has a clean solution that is easy to miss.

A new recipe is _not_ worth writing for:

- Single-line answers that fit better as a note on a component page.
- Speculative patterns that haven't been validated in real use.
- Anything that is really a feature request rather than a usage technique.

Use [`template.md`](template.md) as the starting point for new recipes.
