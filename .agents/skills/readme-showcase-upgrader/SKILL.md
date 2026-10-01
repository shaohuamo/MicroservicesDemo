---
name: readme-showcase-upgrader
description: Edit this repository's root README while keeping README.md and README.en.md aligned, or upgrade its presentation with evidence-based structure, badges, and visuals. Use for focused README edits, readability improvements, showcase redesigns, and GitHub README benchmarking; research exemplars only for broad redesign or benchmarking requests.
---

# README Showcase Upgrader

Keep this repository's root READMEs accurate, aligned, and easy to read. For broader presentation work, make them credible and portfolio-ready without turning them into marketing fluff.

## What this skill does

1. Read the affected README sections and inspect the repository for the facts needed to edit them accurately.
2. For a broad redesign or a request to benchmark other projects, research strong GitHub repositories in the same domain or stack and extract reusable patterns:
   - layout and section order
   - badge grouping and styling
   - architecture visualization patterns
   - screenshot/image presentation
   - concise technical copywriting tone
3. Make the requested README change at the appropriate scale and keep the Chinese and English root READMEs aligned.
4. Explain the meaningful changes briefly.

## When to use this skill

Use this skill when the user wants any of the following:

- make a focused change to the root `README.md`
- improve readability or break up long README paragraphs
- improve or modernize a project README
- make a repository look more professional or industry-standard
- add or reorganize badges
- benchmark a README against top GitHub projects
- improve architecture diagrams, screenshots, or image embedding
- turn observability or performance artifacts into cleaner README sections
- strengthen the repository as a portfolio or showcase project

Do not use this skill for documentation files other than the root READMEs or generic prose editing unrelated to repository documentation.

## Working style

### 1. Start with the local repository

Read the relevant sections of `README.md` and `README.en.md` first. For larger changes, inspect the repository for evidence you can safely use:

- backend, frontend, infrastructure, and test dependencies
- architecture diagrams and screenshots
- observability assets such as Jaeger, Grafana, Prometheus, or logging screenshots
- deployment or quick-start commands
- documented service topology and communication patterns

Treat the repository as the source of truth. Research should improve presentation, not replace reality.

### 2. Research exemplar repositories for broad redesigns

Do this when the user requests a substantial presentation upgrade or comparison with public projects. For a focused edit, work from the local repository without turning the task into a showcase rewrite.

Use GitHub MCP repository search to find 3-6 high-signal public repositories that match the project's domain, architecture, or primary stack.

Prefer repositories that are:

- well-starred or widely respected
- obviously polished in their README structure
- close to the user's stack or audience
- rich in screenshots, diagrams, or onboarding sections

Then use GitHub MCP file retrieval to read each candidate repository's `README.md`.

Focus on patterns, not copying. Never lift branded phrasing, distinctive headlines, or unique copy from exemplar projects.

### 3. Extract patterns before a broad rewrite

Pull out the parts that actually improve reader understanding:

- how the README opens
- where badges are placed and how they are grouped
- whether the architecture appears before or after highlights
- how screenshots are introduced and captioned
- how quick-start steps are kept short and scannable
- how engineering credibility is established without overselling

If a pattern is flashy but does not help the user understand the project faster, skip it.

## Editing guidance

### Focused edits and language sync

For a focused request, change only the relevant sections. When changing the root `README.md`, update corresponding content in the root `README.en.md` in the same task. Translate meaning rather than wording: preserve facts, commands, links, images, and level of detail while writing natural English.

Keep corresponding sections and quick-navigation links aligned. If a heading changes, check the matching anchor in each language. Inspect the relevant diff in both files and preserve unrelated edits. Follow an explicit request to change only one language.

### Paragraph readability

Avoid long, dense paragraphs in both READMEs. Give each paragraph one main idea. Use numbered steps for a sequence, bullets for parallel points, or a table when readers need to compare values. Keep prose natural: do not split every sentence into its own item or add bold labels and punctuation just to create a list.

## Showcase rewrite guidance

Apply the following presentation guidance when the user asks for a broad README redesign. Keep focused edits confined to their requested sections.

### README opening

Lead with:

1. project name
2. one clear value-oriented summary
3. a compact badge block derived from real dependencies
4. a short highlights section or capability summary

The reader should understand the stack and the project's purpose within the first screen.

### Badge strategy

Generate badges from actual technologies found in the repository. Group them by concern when that helps scanning, such as:

- Backend
- Frontend
- Infrastructure
- Observability
- Testing

Use Shields.io style badges. Favor consistency over novelty. Do not add badges for tools that are not actually present.

### Section structure

A strong default order is:

1. Title and one-line summary
2. Badge block
3. Highlights
4. Architecture or system overview
5. Tech stack
6. Core capabilities
7. Repository structure
8. Quick start
9. Screenshots or walkthrough
10. Testing, validation, or operational notes

Adapt this order to the repository instead of forcing it rigidly.

### Architecture visuals

If diagrams or screenshots already exist, present them clearly. Prefer GitHub-friendly HTML when the user wants higher visual control:

```html
<p align="center">
  <img src="images/ComponentsDiagram.png" alt="System architecture" width="900" />
</p>
```

Use:

- meaningful alt text
- centered layout for major diagrams
- clear captions or surrounding context
- stable relative paths

Avoid cluttering the README with too many oversized screenshots in a row.

### Technical copywriting tone

Write like a strong engineer, not a landing page marketer.

Aim for language that is:

- concise
- concrete
- evidence-backed
- easy to scan

Prefer:

- "Uses Ocelot + Consul for gateway routing and dynamic discovery"
- "Publishes product events through RabbitMQ for async processing"

Avoid:

- vague hype
- inflated claims
- invented performance wins
- generic adjectives without technical substance

### Performance and observability case studies

If the repository includes Jaeger traces, Grafana dashboards, logs, or other telemetry evidence, turn them into a short proof-oriented section.

Good patterns:

- explain what a trace demonstrates
- connect instrumentation to an engineering outcome
- describe bottlenecks or optimization choices only when supported by evidence

Bad patterns:

- inventing latency numbers
- claiming throughput gains with no source
- calling the system production-grade without proof

When the evidence is visual only, use cautious wording such as:

- "Jaeger traces show the request path across the gateway, services, Redis, and RabbitMQ."
- "The screenshots demonstrate end-to-end trace correlation across the stack."

## Output format

Default behavior:

1. Edit the relevant README sections directly if the repository is writable and the user asked for improvement; reserve a full rewrite for a broad redesign request.
2. Preserve valuable existing material unless it is redundant or clearly weaker than the new structure.
3. Keep `README.md` and `README.en.md` aligned as described above unless the user explicitly requests a single-language change.
4. After editing, provide a short rationale that explains the meaningful upgrades.

If the user asks for comparison first, provide a compact before/after or pattern comparison, then the rewrite.

## Guardrails

- Do not plagiarize exemplar README copy.
- Do not add technologies that are not in the repository.
- Do not claim metrics, scale, reliability, or performance improvements without evidence.
- Do not replace the project's actual personality with generic boilerplate.
- Do not overcomplicate the README with decorative sections that do not improve comprehension.

## Checklist

Before finishing any root README edit, check that the change:

- reflects the real stack and preserves technical details
- keeps paragraphs easy to scan without mechanical formatting
- aligns corresponding content and navigation in both root READMEs, unless the user requested one language only

For a showcase redesign, also check that the READMEs:

- have a readable opening screen and consistent badge styling
- present diagrams and screenshots clearly
- keep setup instructions easy to follow
- sound technically credible without exaggeration
