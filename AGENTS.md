# Agent Guidelines

## 1. Keep Architecture and Code in Sync

Any structural change — to projects, communication patterns, serialization, project layout, or key concepts — **must** be reflected in `ARCHITECTURE.md`. Likewise, if the architecture document is updated to describe a new design, the code must be updated to match.

This applies to:

- Adding, removing, or renaming projects in the solution.
- Changing the transport, serialization format, or subject conventions.
- Introducing new key concepts (beyond Entity, Component, Behaviour).
- Modifying the code generation strategy or project boundaries.
- Changing build settings, target framework, or tooling.

When making a structural change, update `ARCHITECTURE.md` **in the same changeset** — not as a follow-up.
