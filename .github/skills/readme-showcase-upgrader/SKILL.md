---
name: readme-showcase-upgrader
description: Upgrade a software project's README into a polished showcase by researching strong GitHub examples, extracting layout, badge, visual, and copywriting patterns, and applying them to the local README. Use this whenever the user wants README optimization, better project presentation, portfolio-ready documentation, stronger badges or screenshots, or asks to benchmark their README against top GitHub repositories, even if they do not mention a skill.
---

# README Showcase Upgrader

Turn a technically solid repository into a README that looks credible, readable, and portfolio-ready without turning it into marketing fluff.

## What this skill does

1. Inspect the local repository to understand the real tech stack, architecture, assets, and evidence available.
2. Research strong GitHub repositories in the same domain or stack.
3. Extract reusable patterns from those README files:
   - layout and section order
   - badge grouping and styling
   - architecture visualization patterns
   - screenshot/image presentation
   - concise technical copywriting tone
4. Rewrite the local `README.md` directly by default, then explain the meaningful improvements briefly.

## When to use this skill

Use this skill when the user wants any of the following:

- improve or modernize a project README
- make a repository look more professional or industry-standard
- add or reorganize badges
- benchmark a README against top GitHub projects
- improve architecture diagrams, screenshots, or image embedding
- turn observability or performance artifacts into cleaner README sections
- strengthen the repository as a portfolio or showcase project

Do not use this skill for generic prose editing that is unrelated to repository documentation.

## Working style

### 1. Start with the local repository

Read the local `README.md` first. Then inspect the repository for evidence you can safely use:

- backend, frontend, infrastructure, and test dependencies
- architecture diagrams and screenshots
- observability assets such as Jaeger, Grafana, Prometheus, or logging screenshots
- deployment or quick-start commands
- documented service topology and communication patterns

Treat the repository as the source of truth. Research should improve presentation, not replace reality.

### 2. Research exemplar repositories

Use GitHub MCP repository search to find 3-6 high-signal public repositories that match the project's domain, architecture, or primary stack.

Prefer repositories that are:

- well-starred or widely respected
- obviously polished in their README structure
- close to the user's stack or audience
- rich in screenshots, diagrams, or onboarding sections

Then use GitHub MCP file retrieval to read each candidate repository's `README.md`.

Focus on patterns, not copying. Never lift branded phrasing, distinctive headlines, or unique copy from exemplar projects.

### 3. Extract patterns before rewriting

Pull out the parts that actually improve reader understanding:

- how the README opens
- where badges are placed and how they are grouped
- whether the architecture appears before or after highlights
- how screenshots are introduced and captioned
- how quick-start steps are kept short and scannable
- how engineering credibility is established without overselling

If a pattern is flashy but does not help the user understand the project faster, skip it.

## Rewrite guidance

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

1. Rewrite `README.md` directly if the repository is writable and the user asked for improvement.
2. Preserve valuable existing material unless it is redundant or clearly weaker than the new structure.
3. Preserve the repository's language strategy. If the README is bilingual, keep it bilingual unless the user asks to simplify it.
4. After editing, provide a short rationale that explains the meaningful upgrades.

If the user asks for comparison first, provide a compact before/after or pattern comparison, then the rewrite.

## Guardrails

- Do not plagiarize exemplar README copy.
- Do not add technologies that are not in the repository.
- Do not claim metrics, scale, reliability, or performance improvements without evidence.
- Do not replace the project's actual personality with generic boilerplate.
- Do not overcomplicate the README with decorative sections that do not improve comprehension.

## Checklist

Before finishing, make sure the rewritten README:

- reflects the real stack
- has a readable opening screen
- uses consistent badge styling
- presents diagrams and screenshots clearly
- keeps setup instructions easy to follow
- sounds technically credible
- upgrades the project's showcase quality without exaggeration
