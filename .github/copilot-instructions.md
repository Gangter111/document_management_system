# Repository Instructions

This repository is a Vietnamese enterprise document management system. Follow `AGENTS.md` as the source of truth.

## Core Boundaries

- Preserve Clean Architecture, WPF MVVM boundaries, and the existing OCR/autofill path.
- Preserve the deterministic semantic review workflow.
- `ReviewEvidenceResolver` is the sole semantic ordering authority.
- Runtime coordination manages freshness, cancellation, stale-completion rejection, and invalidation only.
- Diagnostics, replay, audit, diff, and observability are observational only.
- Overlay traversal consumes resolver-normalized order and must not re-rank evidence.

## Document Intelligence Rules

The current review path is:

```text
PDF/OCR/MinerU
-> DocumentStructure
-> SemanticExtractionPipeline
-> Evidence/Provenance
-> Confidence/Rejection reasoning
-> ReviewEvidenceResolver
-> Overlay Review UI
```

Use lightweight deterministic heuristics only. Real-world tuning must be corpus-driven and additive.

Preserve:

- provenance for raw text, normalized text, extraction source, reject source, semantic role, and diagnostics.
- primary/supporting/contextual evidence roles.
- deterministic ordering, traversal, replay, audit snapshots, and focus restoration.
- human-in-the-loop review authority.

Prefer empty or low-confidence values over unsupported promotion.

## Explicit Non-Goals

Do not introduce:

- graph engines.
- adaptive ranking.
- ML orchestration.
- LLM extraction or ranking.
- fuzzy semantic inference.
- semantic cache hierarchies.
- replay/event sourcing systems.
- reactive frameworks.
- telemetry or analytics pipelines.
- token/span-level authority when only bbox-level evidence is available.

## Important Extraction Guards

- DOB context such as `sinh ngay`, `ngay sinh`, or `DOB` must never become issue date.
- Issuer evidence is top-left administrative organization evidence, not national headers, signer roles, body text, or recipients.
- Signer name is primary evidence; signer role/title is supporting evidence.
- Repeated headers/footers are demoted with diagnostics, not removed.
- Focus restoration must never become coordinate-only.
