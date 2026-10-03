# Architecture decision log

## Purpose
This directory records architecturally significant decisions for the project. 
Each accepted record is intended to describe one decision, its context, alternatives 
considered, consequences, evidence, and conditions that should cause a review.

## Index

| ADR | Status | Recorded | Decision |
|----:|--------|---------:|----------|
| [0000](0000-record-architecture-decisions.md) | Accepted | 2026-10-03 | Record architecturally significant decisions in version-controlled Markdown. |

## Governance

- Use [the template](template.md) for new decisions.
- One ADR records one architecturally significant decision.
- Accepted records are append-only except for status and clearly dated notes.
- A replacement decision must create a new ADR and link both directions with `Supersedes` and `Superseded by`.
- Evidence should point to implementation, tests, protocol captures, or published project documentation. Do not claim hardware safety from code structure alone.
- A decision that cannot be checked automatically should state the manual or hardware validation required.