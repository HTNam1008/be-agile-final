# Shared project rules

## SharedKernel

Only stable primitives such as Entity, AggregateRoot, Result, Error and Money.

## Application.Abstractions

Cross-module interfaces such as command/query handlers, current user, clock, unit of work and model contributor.

## Infrastructure.Shared

Technical concerns only: middleware, authentication scheme registration, authorization policies, structured request logging, CORS, health checks and resilient HTTP configuration.

## Contracts

Only global transport primitives. Business DTOs remain in each module's `.Contracts` project.

## Review gate

A type may move to Shared only when at least two modules need the same abstraction and the type contains no business decision. `CommonHelper`, generic repositories and shared business status enums are prohibited.
