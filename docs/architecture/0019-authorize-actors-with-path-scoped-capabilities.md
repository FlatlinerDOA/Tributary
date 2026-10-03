# 0019. Authorize actors with path-scoped capabilities

- Status: Proposed
- Recorded: 2026-10-03
- Decision state in code: Planned
- Depends on:
  - [ADR-0013](0013-sign-with-recoverable-identity-keys.md): capabilities are issued
    and signed by actor keys.
  - [ADR-0018](0018-select-paths-with-one-pattern-language.md): resource patterns and
    containment.
- Supersedes: None
- Superseded by: None
- Provenance: Proposed by an agent and awaiting human review.

## Context

Humans and AI agents edit projects side by side. Unlimited write access is unsafe
when several agents run autonomously, and there is no server to check permissions.
Authorisation must work offline and between peers, and be checkable by anyone who
receives a change.

Points to consider:

- Revoking a capability while peers are offline is hard.
- Agents need limits on compute as well as on what they may change.

## Decision

Authorize every action by every actor, human or agent, with a delegated
capability, UCAN-style:

- A capability grants an **ability** over a **path pattern**, optionally limited by
  caveats such as expiry, branch and compute budget.
- Capabilities are signed by the granting actor's key and can be delegated only
  with equal or narrower scope (pattern containment).
- **Coverage rule:** an action at path P is authorized only if the capability's
  pattern covers the whole subtree under P. An action at `/` needs root-level
  rights.
- Each ADR that introduces a kind of action defines the ability it requires.

## Alternatives considered

### Role-based access control lists

They are familiar. They need an authority to evaluate them, and expressing
delegation chains is clumsy.

### Macaroons or Biscuit tokens

Both are mature options that allow offline attenuation. Biscuit uses a Datalog
policy language, which is more expressive and more complex. Both are still worth
evaluating against UCAN.

### Unrestricted agents confined to branches

This is simple. Long-running maintenance agents cannot act at all.

## Consequences

### Positive

- The same check applies to every actor. Blast radius is limited.
- Narrowing a delegation is checked mechanically.

### Negative and trade-offs

- Capability checks are needed for every action, including actions received from
  peers.
- Revocation is only eventually consistent.

## Evidence

Available:

- [UCAN specification](https://ucan.xyz).

Required before acceptance:

- [ ] Compare UCAN, Biscuit and macaroons on offline attenuation, revocation and
      .NET support.
- [ ] Benchmark the cost of a capability check per action.

## Fitness functions

- Test: an action outside the capability's pattern is rejected.
- Test: an action at `/` is rejected for an actor holding only `/tracks/**`.
- Test: a delegation wider than its parent capability is rejected.

## Review triggers

- The UCAN specification changes in an incompatible way, or a standard alternative
  appears.
